# Component Reference

This document provides detailed specifications for each component in the NuciNotifications API repository.

---

## 1. Program.cs — Application Entry Point

**Location:** `NuciNotifications.API/Program.cs`

**Purpose:** Constructs the generic host and configures the web host defaults.

**Implementation:**
```csharp
public static void Main(string[] args)
    => CreateHostBuilder(args).Build().Run();

public static IHostBuilder CreateHostBuilder(string[] args) => Host
    .CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
```

**Behaviour:**
- Uses `Host.CreateDefaultBuilder` — loads configuration from `appsettings.json`, environment variables, command-line args, user secrets, etc.
- Selects `Startup` as the composition root via `UseStartup<Startup>()`
- No custom configuration, logging, or service registration occurs here

**Dependencies:** `Microsoft.Extensions.Hosting`, `Microsoft.AspNetCore.Hosting`

**Lifetime:** Process entry point; executes once at startup

---

## 2. Startup.cs — Composition Root & Middleware Pipeline

**Location:** `NuciNotifications.API/Startup.cs`

**Purpose:** Registers services and configures the HTTP request pipeline.

**Constructor:**
```csharp
public Startup(IConfiguration configuration)
{
    public IConfiguration Configuration => configuration;
}
```

**ConfigureServices — Service Registration:**
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddControllers();

    services
        .AddConfigurations(Configuration)      // Binds SecuritySettings, SmtpSettings, NuciLoggerSettings
        .AddNuciApiScannerProtection()         // NuciAPI middleware: scanner protection
        .AddNuciApiReplayProtection()          // NuciAPI middleware: replay protection
        .AddCustomServices();                  // Registers ILogger, ISmtpClient, IEmailService as singletons
}
```

**Configure — Middleware Pipeline (in order):**
```csharp
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    app.UseNuciApiExceptionHandling();        // 1. Exception translation (outermost)
    app.UseNuciApiScannerProtection();        // 2. Scanner protection
    app.UseNuciApiReplayProtection();         // 3. Replay protection
    app.UseNuciApiRequestLogging();           // 4. Request logging

    if (env.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();      // Development-only detailed errors
    }

    app.UseHttpsRedirection();                // 5. HTTPS redirection
    app.UseDefaultFiles();                    // 6. Default files (index.html)
    app.UseStaticFiles();                     // 7. Static files
    app.UseRouting();                         // 8. Routing
    app.UseAuthorization();                   // 9. Authorization

    app.UseEndpoints(endpoints => endpoints.MapControllers()); // 10. Controller endpoints
}
```

**Middleware Order Significance:**
- Exception handling is outermost — catches all downstream exceptions
- Security middleware (scanner, replay) runs before routing
- Request logging runs after security but before routing
- HTTPS redirection, static files, routing, authorization follow standard ASP.NET Core order

**Dependencies:** `NuciAPI.Middleware.*`, `NuciNotifications.API.ServiceCollectionExtensions`

**Lifetime:** Singleton (one per host process)

---

## 3. ServiceCollectionExtensions.cs — DI Registration

**Location:** `NuciNotifications.API/ServiceCollectionExtensions.cs`

**Purpose:** Extension methods for registering application services and configuration binding.

### AddConfigurations
```csharp
public static IServiceCollection AddConfigurations(
    this IServiceCollection services,
    IConfiguration configuration)
{
    SecuritySettings securitySettings = new();
    SmtpSettings smtpSettings = new();

    configuration.Bind(nameof(SecuritySettings), securitySettings);
    configuration.Bind(nameof(SmtpSettings), smtpSettings);

    return services
        .AddSingleton(securitySettings)
        .AddSingleton(smtpSettings)
        .AddNuciLoggerSettings(configuration);
}
```

**Behaviour:**
- Creates new instances of `SecuritySettings` and `SmtpSettings`
- Binds configuration sections by name (`"SecuritySettings"`, `"SmtpSettings"`)
- Registers both as **singletons** — same instance for entire process lifetime
- Delegates NuciLog settings to `AddNuciLoggerSettings` (from NuciLog package)
- **No validation** of bound values — missing/invalid values surface at request time

### AddCustomServices
```csharp
public static IServiceCollection AddCustomServices(this IServiceCollection services) => services
    .AddSingleton<ILogger, NuciLogger>()
    .AddSingleton<ISmtpClient, SmtpClientWrapper>()
    .AddSingleton<IEmailService, EmailService>();
```

**Behaviour:**
- Registers all three as **singletons**
- `NuciLogger` from NuciLog package implements `ILogger`
- `SmtpClientWrapper` wraps `System.Net.Mail.SmtpClient`
- `EmailService` orchestrates delivery and retry logic

**Dependencies:** `NuciLog`, `NuciLog.Core`, `NuciNotifications.API.Configuration`, `NuciNotifications.API.Service`

---

## 4. Configuration Objects

### SecuritySettings
**Location:** `NuciNotifications.API/Configuration/SecuritySettings.cs`

```csharp
public sealed class SecuritySettings
{
    public string ApiKey { get; set; }
}
```

**Purpose:** Holds the API key for request authorisation.

**Configuration Section:** `SecuritySettings`

**Required:** Yes — `ApiKey` must be provided

**Binding:** Simple property binding; no validation

---

### SmtpSettings
**Location:** `NuciNotifications.API/Configuration/SmtpSettings.cs`

```csharp
public sealed class SmtpSettings
{
    public string Host { get; set; }
    public int Port { get; set; } = 587;
    public string Username { get; set; }
    public string Password { get; set; }
    public string SenderName { get; set; } = "Notifier";
    public int MaximumAttempts { get; set; } = 3;
    public int DelayBetweenAttemptsInSeconds { get; set; } = 5;
}
```

**Purpose:** Configures SMTP connection, authentication, sender identity, and retry policy.

**Configuration Section:** `SmtpSettings`

| Property | Type | Default | Required | Description |
|----------|------|---------|----------|-------------|
| Host | string | — | Yes | SMTP server hostname |
| Port | int | 587 | No | SMTP server port |
| Username | string | — | Yes | SMTP username (also sender address) |
| Password | string | — | Yes | SMTP password |
| SenderName | string | "Notifier" | No | Display name when request omits sender |
| MaximumAttempts | int | 3 | No | **Retries after initial attempt** (so 3 = 4 total sends) |
| DelayBetweenAttemptsInSeconds | int | 5 | No | Delay between retry attempts |

**Binding:** Simple property binding; no validation of ranges, formats, or required fields

---

## 5. EmailController — HTTP Boundary

**Location:** `NuciNotifications.API/Controllers/EmailsController.cs`

```csharp
[Route("[controller]")]
[ApiController]
public class EmailController(
    IEmailService service,
    SecuritySettings securitySettings) : NuciApiController
{
    private readonly NuciApiAuthorisation authorisation = NuciApiAuthorisation.ApiKey(securitySettings.ApiKey);

    [HttpPost]
    public ActionResult Send([FromBody] SendEmailRequest request)
        => ProcessRequest(
            request,
            () => service.Send(request),
            authorisation);
}
```

**Route:** `POST /Email` (controller name = "Email")

**Inheritance:** `NuciApiController` (from `NuciAPI.Controllers` package)

**Authorisation:** API-key policy constructed from `SecuritySettings.ApiKey`

**Request Processing:**
1. ASP.NET Core model-binds JSON to `SendEmailRequest`
2. `[ApiController]` triggers automatic model validation (data annotations)
3. `ProcessRequest` (from base class) executes:
   - Validates API key from `Authorization: Bearer <key>` header
   - Optionally validates HMAC from `X-HMAC` header
   - Invokes the delivery action `() => service.Send(request)`
   - Translates exceptions to HTTP responses via NuciAPI exception middleware

**Response:** Package-defined `ActionResult` (success or error shape from NuciAPI)

**Dependencies:** `NuciAPI.Controllers`, `NuciNotifications.API.Service.IEmailService`, `NuciNotifications.API.Configuration.SecuritySettings`

**Lifetime:** Per-request (controller activated by ASP.NET Core for each request)

---

## 6. SendEmailRequest — Request Contract

**Location:** `NuciNotifications.API/Requests/SendEmailRequest.cs`

```csharp
public sealed class SendEmailRequest : NuciApiRequest
{
    [HmacOrder(1)]
    public string Sender { get; set; }

    [Required]
    [HmacOrder(2)]
    public string Recipient { get; set; }

    [Required]
    [HmacOrder(5)]
    public string Subject { get; set; }

    [Required]
    [HmacOrder(6)]
    public string Body { get; set; }
}
```

**Inheritance:** `NuciApiRequest` (from `NuciAPI.Requests`) — provides HMAC support

**Fields:**

| Field | Type | Required | HMAC Order | Description |
|-------|------|----------|------------|-------------|
| Sender | string | No | 1 | Display name for sender; empty/omitted uses `SmtpSettings.SenderName` |
| Recipient | string | Yes | 2 | Recipient email address |
| Subject | string | Yes | 5 | Message subject |
| Body | string | Yes | 6 | Plain-text message body |

**Validation:** Data annotations (`[Required]`) enforced by `[ApiController]`

**HMAC Ordering:** Field order values (1, 2, 5, 6) are compatibility-sensitive for signed clients

**Dependencies:** `NuciAPI.Requests`, `NuciSecurity.HMAC`, `System.ComponentModel.DataAnnotations`

---

## 7. EmailService — Application Orchestration

**Location:** `NuciNotifications.API/Service/EmailService.cs`

```csharp
public class EmailService(
    SmtpSettings settings,
    ISmtpClient smtpClient,
    ILogger logger) : IEmailService
{
    private static int MillisecondsPerSecond => 1_000;

    public void Send(SendEmailRequest request)
        => Send(request, settings.MaximumAttempts);

    private void Send(SendEmailRequest request, int attemptsLeft)
    {
        string senderName = settings.SenderName;
        if (!string.IsNullOrWhiteSpace(request.Sender))
        {
            senderName = request.Sender;
        }

        IEnumerable<LogInfo> logInfos = [
            new(MyLogInfoKey.SenderAddress, settings.Username),
            new(MyLogInfoKey.SenderName, senderName),
            new(MyLogInfoKey.Recipient, request.Recipient),
            new(MyLogInfoKey.Subject, request.Subject)
        ];

        logger.Info(MyOperation.SendEmail, OperationStatus.Started, logInfos);

        using MailMessage message = new(
            settings.Username,
            request.Recipient,
            request.Subject,
            request.Body);
        message.From = new(settings.Username, senderName);

        try
        {
            smtpClient.Send(message);
            logger.Info(MyOperation.SendEmail, OperationStatus.Success, logInfos);
        }
        catch (SmtpException exception) when (
            exception.Message.Contains("timed out") ||
            exception.Message.Contains("timeout") ||
            exception.Message.Contains("Timeout"))
        {
            logger.Warn(
                MyOperation.SendEmail,
                OperationStatus.Failure,
                logInfos,
                new LogInfo(MyLogInfoKey.Attempt, settings.MaximumAttempts - attemptsLeft + 1));

            if (attemptsLeft <= 0)
            {
                throw new TimeoutException(
                    "Failed to send the e-mail notification after the maximum number of attempts.",
                    exception);
            }

            Thread.Sleep(settings.DelayBetweenAttemptsInSeconds * MillisecondsPerSecond);
            Send(request, attemptsLeft - 1);
        }
        catch (Exception exception)
        {
            logger.Error(MyOperation.SendEmail, OperationStatus.Failure, exception, logInfos);
            throw;
        }
    }
}
```

**Responsibilities:**
1. **Sender display name resolution** — request `Sender` overrides configured `SenderName` if non-empty
2. **Message construction** — creates `MailMessage` with:
   - From: `settings.Username` (address) + resolved `senderName` (display name)
   - To: `request.Recipient`
   - Subject: `request.Subject`
   - Body: `request.Body` (plain text)
3. **Delivery logging** — emits structured logs via NuciLog:
   - `Started` — before SMTP submission
   - `Success` — after successful submission
   - `Failure` (Warn) — on recognised timeout, includes attempt number
   - `Failure` (Error) — on other exceptions, includes exception
4. **Retry policy** — recursive retry on recognised timeout exceptions:
   - Recognises timeout by checking `SmtpException.Message` for "timed out", "timeout", "Timeout" (case-sensitive substrings)
   - Retries up to `MaximumAttempts` times **after initial attempt**
   - Blocks thread with `Thread.Sleep` during delay
   - Throws `TimeoutException` when retries exhausted
5. **Exception propagation** — non-timeout exceptions logged and rethrown unchanged

**Log Metadata (excludes body and credentials):**
- `SenderAddress` — `settings.Username`
- `SenderName` — resolved display name
- `Recipient` — `request.Recipient`
- `Subject` — `request.Subject`
- `Attempt` — attempt number (on timeout warnings)

**Dependencies:** `SmtpSettings`, `ISmtpClient`, `ILogger` (NuciLog), `MyLogInfoKey`, `MyOperation`, `System.Net.Mail`, `System.Threading`

**Lifetime:** Singleton (registered in DI)

**Concurrency:** Not thread-safe for shared `SmtpClient` — see `SmtpClientWrapper`

---

## 8. IEmailService — Application Contract

**Location:** `NuciNotifications.API/Service/IEmailService.cs`

```csharp
public interface IEmailService
{
    void Send(SendEmailRequest request);
}
```

**Purpose:** Abstraction for email delivery orchestration; enables test substitution.

**Implementation:** `EmailService`

**Consumers:** `EmailController`

---

## 9. ISmtpClient — Transport Port

**Location:** `NuciNotifications.API/Service/ISmtpClient.cs`

```csharp
public interface ISmtpClient
{
    void Send(MailMessage message);
}
```

**Purpose:** Isolates application logic from concrete SMTP implementation; enables test substitution.

**Implementation:** `SmtpClientWrapper`

**Consumers:** `EmailService`

---

## 10. SmtpClientWrapper — SMTP Integration Adapter

**Location:** `NuciNotifications.API/Service/SmtpClientWrapper.cs`

```csharp
public sealed class SmtpClientWrapper(SmtpSettings settings) : ISmtpClient
{
    private readonly SmtpClient smtpClient = BuildSmtpClient(settings);

    private static int SmtpTimeoutInMilliseconds => 200_000;

    public void Send(MailMessage message) => smtpClient.Send(message);

    private static SmtpClient BuildSmtpClient(SmtpSettings settings) => new(settings.Host, settings.Port)
    {
        Credentials = new NetworkCredential(settings.Username, settings.Password),
        EnableSsl = true,
        Timeout = SmtpTimeoutInMilliseconds
    };
}
```

**Behaviour:**
- Constructs **one** `System.Net.Mail.SmtpClient` instance at construction time
- Configures:
  - Host and port from `SmtpSettings`
  - Credentials (username/password) from `SmtpSettings`
  - `EnableSsl = true` — TLS required
  - `Timeout = 200_000` ms (200 seconds) — applies to each SMTP operation
- Retains the `SmtpClient` instance for the **entire process lifetime** (singleton)
- `Send` delegates directly to the wrapped client
- **No disposal** of the wrapped `SmtpClient` — relies on process termination

**Concurrency Implications:**
- Single `SmtpClient` instance shared across all concurrent requests
- `System.Net.Mail.SmtpClient` is **not thread-safe** for concurrent `Send` calls
- No repository-defined synchronisation, pooling, or connection management
- Concurrent requests may experience undefined behaviour or exceptions

**Dependencies:** `SmtpSettings`, `System.Net.Mail`, `System.Net`

**Lifetime:** Singleton (registered in DI)

---

## 11. Logging Identifiers

### MyLogInfoKey
**Location:** `NuciNotifications.API/Logging/MyLogInfoKey.cs`

```csharp
public sealed class MyLogInfoKey : LogInfoKey
{
    private MyLogInfoKey(string name) : base(name) { }

    public static LogInfoKey Attempt => new MyLogInfoKey(nameof(Attempt));
    public static LogInfoKey SenderAddress => new MyLogInfoKey(nameof(SenderAddress));
    public static LogInfoKey SenderName => new MyLogInfoKey(nameof(SenderName));
    public static LogInfoKey Recipient => new MyLogInfoKey(nameof(Recipient));
    public static LogInfoKey Subject => new MyLogInfoKey(nameof(Subject));
}
```

**Purpose:** Strongly-typed keys for structured log metadata.

**Values Emitted by EmailService:**
| Key | Value Source |
|-----|--------------|
| Attempt | `settings.MaximumAttempts - attemptsLeft + 1` (on timeout warnings) |
| SenderAddress | `settings.Username` |
| SenderName | Resolved display name (request or settings) |
| Recipient | `request.Recipient` |
| Subject | `request.Subject` |

**Note:** Message body and credentials are **never** logged

---

### MyOperation
**Location:** `NuciNotifications.API/Logging/MyOperation.cs`

```csharp
public sealed class MyOperation : Operation
{
    private MyOperation(string name) : base(name) { }

    public static Operation SendEmail => new MyOperation(nameof(SendEmail));
}
```

**Purpose:** Identifies the `SendEmail` operation in NuciLog records.

**Status Values Used:**
- `OperationStatus.Started` — delivery attempt beginning
- `OperationStatus.Success` — SMTP submission succeeded
- `OperationStatus.Failure` — timeout (warn) or other exception (error)

---

## 12. NuciNotifications.API.UnitTests — Test Suite

**Location:** `NuciNotifications.API.UnitTests/Service/EmailServiceTests.cs`

**Framework:** NUnit 4.6.1 + Moq 4.20.72

**Test Coverage:**

| Test Category | Tests | Verifies |
|---------------|-------|----------|
| Basic delivery | 7 | SMTP called once, logs Started/Success, message fields mapped correctly |
| Sender display name | 4 | Null/empty/whitespace uses settings; explicit sender overrides |
| Timeout retry | 4 | Retries on timeout, logs attempt, throws TimeoutException when exhausted |
| Timeout detection | 2 | Recognises "timed out", "timeout", "Timeout" (case variations) |
| Non-timeout failure | 2 | Logs error, rethrows original exception (SmtpException, general Exception) |

**Test Strategy:**
- Substitutes `ISmtpClient` and `ILogger` with Moq mocks
- Verifies interactions (calls, arguments, counts) rather than end-to-end behaviour
- No network, filesystem, or real SMTP dependencies
- `DelayBetweenAttemptsInSeconds = 0` in test settings to avoid delays

**Gaps (Not Tested):**
- Controller/model binding/authorisation
- Middleware order and security features
- Configuration binding and validation
- Live SMTP integration
- Concurrency and shared SmtpClient behaviour
- HMAC signing/verification
- Logging destination configuration

---

## Component Dependency Graph

```
Program.cs
    └── Startup.cs
            ├── AddConfigurations() → SecuritySettings (singleton)
            │                           └── SmtpSettings (singleton)
            ├── AddNuciApiScannerProtection()
            ├── AddNuciApiReplayProtection()
            └── AddCustomServices()
                    ├── ILogger → NuciLogger (singleton)
                    ├── ISmtpClient → SmtpClientWrapper (singleton)
                    │                   └── SmtpSettings → System.Net.Mail.SmtpClient
                    └── IEmailService → EmailService (singleton)
                                            ├── SmtpSettings
                                            ├── ISmtpClient
                                            └── ILogger

EmailController (per-request)
    ├── SecuritySettings (singleton)
    ├── IEmailService (singleton)
    └── NuciApiController.ProcessRequest() → EmailService.Send()

EmailService.Send()
    ├── Resolves sender name
    ├── Logs Started (MyOperation.SendEmail, MyLogInfoKey.*)
    ├── Constructs MailMessage
    ├── ISmtpClient.Send() → SmtpClientWrapper → System.Net.Mail.SmtpClient
    ├── On success: Logs Success
    ├── On timeout: Logs Warn (with Attempt), Thread.Sleep, recursive Send()
    └── On other exception: Logs Error, rethrows
```