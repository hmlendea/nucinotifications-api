# Testing Reference

This document describes the test architecture, coverage, and verification strategies for the NuciNotifications API.

---

## Test Project Structure

```
NuciNotifications.API.UnitTests/
├── NuciNotifications.API.UnitTests.csproj
└── Service/
    └── EmailServiceTests.cs
```

**Framework:** NUnit 4.6.1 + Moq 4.20.72
**Test Runner:** NUnit3TestAdapter 6.3.0 + Microsoft.NET.Test.Sdk 18.10.1
**Target Framework:** net10.0

---

## Test Coverage Summary

| Component | Coverage | Test File |
|-----------|----------|-----------|
| EmailService.Send() — basic delivery | ✅ 7 tests | EmailServiceTests.cs |
| EmailService.Send() — sender display name resolution | ✅ 4 tests | EmailServiceTests.cs |
| EmailService.Send() — timeout retry logic | ✅ 4 tests | EmailServiceTests.cs |
| EmailService.Send() — timeout detection variants | ✅ 2 tests | EmailServiceTests.cs |
| EmailService.Send() — non-timeout exception handling | ✅ 2 tests | EmailServiceTests.cs |
| EmailController / HTTP layer | ❌ 0 tests | — |
| Middleware pipeline | ❌ 0 tests | — |
| Configuration binding | ❌ 0 tests | — |
| SmtpClientWrapper / live SMTP | ❌ 0 tests | — |
| HMAC signing/verification | ❌ 0 tests | — |
| Concurrency / shared SmtpClient | ❌ 0 tests | — |
| End-to-end integration | ❌ 0 tests | — |

**Total Tests:** 19

---

## EmailServiceTests — Detailed Test Catalogue

### Setup
```csharp
[SetUp]
public void SetUp()
{
    mockSmtpClient = new Mock<ISmtpClient>();
    mockLogger = new Mock<ILogger>();
    emailService = BuildEmailService(BuildSmtpSettings());
}

private EmailService BuildEmailService(SmtpSettings smtpSettings)
    => new(smtpSettings, mockSmtpClient.Object, mockLogger.Object);

private static SmtpSettings BuildSmtpSettings()
    => new()
    {
        Host = "mail.nucilandia.ro",
        Port = 587,
        Username = "notifier@nucilandia.ro",
        Password = "testpassword",
        SenderName = "Nucilandia Notifier",
        MaximumAttempts = 3,
        DelayBetweenAttemptsInSeconds = 0  // Zero delay for fast tests
    };

private static SendEmailRequest BuildSendEmailRequest()
    => new()
    {
        Sender = "Solaire of Astora",
        Recipient = "vasile.ciupitu@gmail.com",
        Subject = "Praise the Sun!",
        Body = "Would you like to join me on a jolly co-op adventure?"
    };
```

---

### Basic Delivery Tests (7 tests)

| Test | Verifies |
|------|----------|
| `GivenValidRequest_WhenSendIsCalled_ThenSmtpClientSendIsCalledOnce` | `ISmtpClient.Send` invoked exactly once |
| `GivenValidRequest_WhenSendIsCalled_ThenLogsStarted` | `logger.Info(Started)` called once |
| `GivenValidRequest_WhenSendIsCalled_ThenLogsSuccess` | `logger.Info(Success)` called once |
| `GivenValidRequest_WhenSendIsCalled_ThenEmailFromAddressMatchesSettingsUsername` | `MailMessage.From.Address == settings.Username` |
| `GivenValidRequest_WhenSendIsCalled_ThenEmailRecipientMatchesRequest` | `MailMessage.To[0].Address == request.Recipient` |
| `GivenValidRequest_WhenSendIsCalled_ThenEmailSubjectMatchesRequest` | `MailMessage.Subject == request.Subject` |
| `GivenValidRequest_WhenSendIsCalled_ThenEmailBodyMatchesRequest` | `MailMessage.Body == request.Body` |

**Key Assertions:**
- Uses `mockSmtpClient.Verify(x => x.Send(It.Is<MailMessage>(m => ...)), Times.Once)`
- Uses `mockLogger.Verify(x => x.Info(It.Is<Operation>(op => op.Name == "SendEmail"), ...), Times.Once)`
- Verifies message construction, not just invocation

---

### Sender Display Name Resolution Tests (4 tests)

| Test | Scenario | Expected Display Name |
|------|----------|----------------------|
| `GivenRequestWithNullSender_WhenSendIsCalled_ThenEmailDisplayNameMatchesSettingsSenderName` | `request.Sender = null` | `settings.SenderName` ("Nucilandia Notifier") |
| `GivenRequestWithEmptySender_WhenSendIsCalled_ThenEmailDisplayNameMatchesSettingsSenderName` | `request.Sender = ""` | `settings.SenderName` |
| `GivenRequestWithWhitespaceSender_WhenSendIsCalled_ThenEmailDisplayNameMatchesSettingsSenderName` | `request.Sender = "   "` | `settings.SenderName` |
| `GivenRequestWithSender_WhenSendIsCalled_ThenEmailDisplayNameMatchesRequestSender` | `request.Sender = "Solaire of Astora"` | `"Solaire of Astora"` |

**Logic Under Test:**
```csharp
string senderName = settings.SenderName;
if (!string.IsNullOrWhiteSpace(request.Sender))
{
    senderName = request.Sender;
}
message.From = new(settings.Username, senderName);
```

---

### Timeout Retry Tests (4 tests)

| Test | Scenario | Verifies |
|------|----------|----------|
| `GivenTimedOutSmtpException_WhenMaximumAttemptsNotExceeded_ThenRetriesSending` | `MaximumAttempts=1`, timeout on first call | `Send` called exactly 2 times (initial + 1 retry) |
| `GivenTimedOutSmtpException_WhenMaximumAttemptsExceeded_ThenThrowsTimeoutException` | `MaximumAttempts=0`, timeout on first call | `TimeoutException` thrown |
| `GivenTimedOutSmtpException_WhenMaximumAttemptsExceeded_ThenLogsWarning` | `MaximumAttempts=0`, timeout on first call | `logger.Warn` called once with attempt metadata |
| `GivenSmtpExceptionWithLowercaseTimeoutMessage_WhenMaximumAttemptsExceeded_ThenThrowsTimeoutException` | Exception message: "SMTP timeout occurred" | Recognised as timeout, throws `TimeoutException` |

**Retry Logic Under Test:**
```csharp
catch (SmtpException exception) when (
    exception.Message.Contains("timed out") ||
    exception.Message.Contains("timeout") ||
    exception.Message.Contains("Timeout"))
{
    logger.Warn(..., new LogInfo(MyLogInfoKey.Attempt, settings.MaximumAttempts - attemptsLeft + 1));
    if (attemptsLeft <= 0) throw new TimeoutException(..., exception);
    Thread.Sleep(settings.DelayBetweenAttemptsInSeconds * MillisecondsPerSecond);
    Send(request, attemptsLeft - 1);
}
```

---

### Timeout Detection Variant Tests (2 tests)

| Test | Exception Message | Recognised as Timeout? |
|------|-------------------|------------------------|
| `GivenSmtpExceptionWithLowercaseTimeoutMessage_WhenMaximumAttemptsExceeded_ThenThrowsTimeoutException` | "SMTP timeout occurred" | ✅ Yes |
| `GivenSmtpExceptionWithCapitalisedTimeoutMessage_WhenMaximumAttemptsExceeded_ThenThrowsTimeoutException` | "Timeout connecting to server" | ✅ Yes |

**Detection Logic:**
```csharp
exception.Message.Contains("timed out") ||
exception.Message.Contains("timeout") ||
exception.Message.Contains("Timeout")
```

**Note:** Case-sensitive substring matching. "TIMEOUT" (all caps) would NOT be recognised.

---

### Non-Timeout Exception Handling Tests (2 tests)

| Test | Exception Type | Verifies |
|------|----------------|----------|
| `GivenNonTimeoutSmtpException_WhenSendFails_ThenLogsError` | `SmtpException("SMTP server rejected the message")` | `logger.Error` called once, original exception rethrown |
| `GivenNonTimeoutSmtpException_WhenSendFails_ThenRethrowsException` | Same as above | Rethrown exception is **same instance** (`Throws.Exception.SameAs`) |
| `GivenGeneralException_WhenSendFails_ThenLogsError` | `InvalidOperationException("Unexpected failure")` | `logger.Error` called once, original exception rethrown |
| `GivenGeneralException_WhenSendFails_ThenRethrowsException` | Same as above | Rethrown exception is **same instance** |

**Logic Under Test:**
```csharp
catch (Exception exception)
{
    logger.Error(MyOperation.SendEmail, OperationStatus.Failure, exception, logInfos);
    throw;  // Preserves stack trace and exception identity
}
```

---

## Test Execution

### Run All Tests
```bash
dotnet test --no-build --verbosity normal
```

### Run Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~EmailServiceTests" --verbosity normal
```

### Run Specific Test
```bash
dotnet test --filter "FullyQualifiedName~GivenValidRequest_WhenSendIsCalled_ThenSmtpClientSendIsCalledOnce" --verbosity normal
```

### CI Execution (from .github/workflows/dotnet.yml)
```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build --verbosity normal
```

---

## Test Design Patterns

### 1. Arrange-Act-Assert with Moq
```csharp
[Test]
public void GivenValidRequest_WhenSendIsCalled_ThenSmtpClientSendIsCalledOnce()
{
    // Arrange (in SetUp)
    // Act
    emailService.Send(BuildSendEmailRequest());
    // Assert
    mockSmtpClient.Verify(x => x.Send(It.IsAny<MailMessage>()), Times.Once);
}
```

### 2. Argument Matching with Constraints
```csharp
mockSmtpClient.Verify(
    x => x.Send(It.Is<MailMessage>(m =>
        m.From.Address == "notifier@nucilandia.ro")),
    Times.Once);
```

### 3. Sequence Verification for Retries
```csharp
mockSmtpClient
    .SetupSequence(x => x.Send(It.IsAny<MailMessage>()))
    .Throws(new SmtpException("Connection timed out"))
    .Throws(new SmtpException("Connection timed out"))
    .Returns(/* success */);
```

### 4. Exception Type and Identity Verification
```csharp
Assert.That(
    () => emailService.Send(BuildSendEmailRequest()),
    Throws.TypeOf<TimeoutException>());

// Verify same exception instance rethrown
Assert.That(
    () => emailService.Send(BuildSendEmailRequest()),
    Throws.Exception.SameAs(expectedException));
```

---

## Test Gaps and Limitations

### Not Tested — HTTP Layer
| Area | Missing Coverage |
|------|------------------|
| Route mapping | `POST /Email` route registration |
| Model binding | JSON → `SendEmailRequest` deserialization |
| Validation | `[Required]` attribute enforcement |
| Authorisation | API key extraction and comparison |
| HMAC | `X-HMAC` header processing |
| Response shape | Success/error `ActionResult` structure |
| Exception translation | NuciAPI middleware exception-to-HTTP mapping |

### Not Tested — Middleware Pipeline
| Middleware | Missing Coverage |
|------------|------------------|
| ExceptionHandling | Exception translation behaviour |
| ScannerProtection | Scan detection/blocking |
| ReplayProtection | Replay detection/blocking |
| RequestLogging | HTTP request log emission |
| Order sensitivity | Middleware order correctness |

### Not Tested — Configuration
| Area | Missing Coverage |
|------|------------------|
| Binding | `Configuration.Bind` for `SecuritySettings`, `SmtpSettings` |
| Validation | Required field detection at startup |
| Environment overrides | `__` separator precedence |
| Token substitution | `[[...]]` handling |

### Not Tested — SMTP Integration
| Area | Missing Coverage |
|------|------------------|
| SmtpClientWrapper | `BuildSmtpClient` configuration |
| TLS/SSL | `EnableSsl=true` behaviour |
| Timeout | 200s timeout enforcement |
| Credentials | `NetworkCredential` usage |
| Disposal | `SmtpClient` cleanup |

### Not Tested — Concurrency
| Scenario | Missing Coverage |
|----------|------------------|
| Concurrent requests | Shared `SmtpClient` thread safety |
| Connection pooling | None implemented |
| Request queuing | None implemented |

### Not Tested — Observability
| Area | Missing Coverage |
|------|------------------|
| Log destination | File output, rotation, permissions |
| Structured log schema | Metadata completeness |
| Request correlation | Trace IDs, correlation IDs |

---

## Test Quality Assessment

### Strengths
- **Isolated unit tests** — No network, filesystem, or external dependencies
- **Fast execution** — `DelayBetweenAttemptsInSeconds = 0` in test settings
- **Behaviour-focused** — Verifies interactions and side effects, not implementation details
- **Comprehensive retry coverage** — Tests retry count, logging, exception translation
- **Exception identity preservation** — Verifies `throw;` not `throw ex;`

### Weaknesses
- **No integration tests** — Cannot verify end-to-end delivery
- **No controller tests** — HTTP layer untested
- **No configuration tests** — Startup validation gaps undetected
- **No concurrency tests** — Shared `SmtpClient` risk unverified
- **String-based timeout detection** — Tests only cover 3 specific substrings

---

## Recommended Test Additions

### Priority 1: Controller Integration Tests
```csharp
// Using WebApplicationFactory<Program>
[Test]
public async Task PostEmail_WithValidApiKey_ReturnsSuccess()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.ConfigureServices(services => {
            // Replace ISmtpClient with mock
        }));
    var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", "test-key");

    // Act
    var response = await client.PostAsJsonAsync("/Email", new SendEmailRequest { ... });

    // Assert
    response.EnsureSuccessStatusCode();
}
```

### Priority 2: Configuration Binding Tests
```csharp
[Test]
public void AddConfigurations_BindsSecuritySettings_FromConfiguration()
{
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new[] { new KeyValuePair<string, string>("SecuritySettings:ApiKey", "test-key") })
        .Build();

    var services = new ServiceCollection();
    services.AddConfigurations(config);
    var provider = services.BuildServiceProvider();

    var settings = provider.GetRequiredService<SecuritySettings>();
    Assert.That(settings.ApiKey, Is.EqualTo("test-key"));
}
```

### Priority 3: Concurrency Stress Test
```csharp
[Test]
public async Task ConcurrentRequests_DoNotCorruptSmtpClient()
{
    // Requires real or thread-safe SmtpClient substitute
    // Verify no exceptions under concurrent load
}
```

### Priority 4: Timeout Detection Edge Cases
```csharp
[TestCase("TIMEOUT")]           // All caps - currently NOT recognised
[TestCase("Timed Out")]         // Mixed case - currently NOT recognised
[TestCase("connection timeout")] // Lowercase - recognised
public void TimeoutDetection_HandlesCaseVariants(string message)
{
    // Verify which variants are actually recognised
}
```

---

## Test Maintenance Guidelines

1. **Keep tests fast** — Maintain `DelayBetweenAttemptsInSeconds = 0` in test settings
2. **Test behaviour, not implementation** — Verify logs, calls, exceptions; avoid verifying private method calls
3. **Use descriptive names** — `Given{Context}_When{Action}_Then{Outcome}` pattern
4. **Isolate test data** — Each test builds its own request/settings; no shared mutable state
5. **Verify exception identity** — Use `Throws.Exception.SameAs` for rethrow verification
6. **Add tests for new behaviour** — Every bug fix or feature should include regression test