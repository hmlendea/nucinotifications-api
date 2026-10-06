# Execution Flows

This document traces the complete execution paths through the NuciNotifications API, from HTTP request to SMTP delivery and response.

---

## 1. Startup Sequence

```
Main(args)
    └── CreateHostBuilder(args)
            └── Host.CreateDefaultBuilder(args)
                    ├── Loads configuration (appsettings.json, env vars, etc.)
                    ├── Configures default logging
                    └── Configures Kestrel
            └── ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>())
                    └── Startup constructor receives IConfiguration
Build()
    └── Host builds service provider, validates DI registrations
Run()
    └── Starts Kestrel, begins accepting requests
```

**Key Points:**
- Configuration bound **once** during `AddConfigurations` in `ConfigureServices`
- All custom services registered as **singletons**
- Middleware pipeline built in `Configure` — order is critical

---

## 2. HTTP Request Processing — Happy Path

```
Client: POST /Email
    │
    ▼
Kestrel receives request
    │
    ▼
Middleware Pipeline (in order):
    │
    ├── UseNuciApiExceptionHandling()
    │       └── Wraps all downstream middleware in try/catch
    │           └── Translates exceptions to HTTP responses (package-defined)
    │
    ├── UseNuciApiScannerProtection()
    │       └── Detects/blocks scanning attempts (package-defined)
    │
    ├── UseNuciApiReplayProtection()
    │       └── Prevents request replay (package-defined)
    │
    ├── UseNuciApiRequestLogging()
    │       └── Logs HTTP request metadata via NuciLog (package-defined)
    │
    ├── UseHttpsRedirection()
    │       └── Redirects HTTP to HTTPS
    │
    ├── UseDefaultFiles() / UseStaticFiles()
    │       └── Serves static content (if any)
    │
    ├── UseRouting()
    │       └── Matches request to controller route
    │
    ├── UseAuthorization()
    │       └── Authorisation policies evaluated (API key via ProcessRequest)
    │
    └── UseEndpoints(endpoints.MapControllers())
            └── Routes to EmailController.Send()
                    │
                    ▼
            EmailController.Send(request)
                    │
                    ├── Model binding: JSON → SendEmailRequest
                    │       └── [ApiController] validates [Required] fields
                    │
                    ├── NuciApiController.ProcessRequest(request, action, authorisation)
                    │       │
                    │       ├── Extracts API key from Authorization: Bearer <key>
                    │       ├── Compares with SecuritySettings.ApiKey
                    │       ├── Optionally validates X-HMAC header (NuciSecurity.HMAC)
                    │       ├── Invokes action: () => service.Send(request)
                    │       └── Returns package-defined ActionResult
                    │
                    └── EmailService.Send(request)  ◄─── See Section 3
                    │
                    ▼
            ActionResult returned to middleware
                    │
                    ▼
Middleware unwinds (exception handling catches any unhandled)
                    │
                    ▼
HTTP Response to client
```

---

## 3. EmailService.Send — Delivery Orchestration

```
EmailService.Send(request)
    │
    ├── Resolve sender display name
    │       ├── senderName = settings.SenderName
    │       └── if (!string.IsNullOrWhiteSpace(request.Sender)) senderName = request.Sender
    │
    ├── Prepare log metadata
    │       └── logInfos = [
    │               SenderAddress: settings.Username,
    │               SenderName: senderName,
    │               Recipient: request.Recipient,
    │               Subject: request.Subject
    │           ]
    │
    ├── Log Started
    │       └── logger.Info(MyOperation.SendEmail, OperationStatus.Started, logInfos)
    │
    ├── Construct MailMessage
    │       ├── using MailMessage message = new(
    │       │       settings.Username,      // from address
    │       │       request.Recipient,      // to address
    │       │       request.Subject,        // subject
    │       │       request.Body)           // body (plain text)
    │       └── message.From = new(settings.Username, senderName)  // override display name
    │
    ├── Try SMTP submission
    │       └── smtpClient.Send(message)  ◄─── ISmtpClient → SmtpClientWrapper → System.Net.Mail
    │
    ├── On Success:
    │       └── logger.Info(MyOperation.SendEmail, OperationStatus.Success, logInfos)
    │       └── Return to controller
    │
    ├── On SmtpException with timeout message:
    │       │
    │       ├── Check: exception.Message.Contains("timed out") ||
    │       │         exception.Message.Contains("timeout") ||
    │       │         exception.Message.Contains("Timeout")
    │       │
    │       ├── Log Warning with attempt number
    │       │       └── logger.Warn(MyOperation.SendEmail, OperationStatus.Failure, logInfos,
    │       │           new LogInfo(MyLogInfoKey.Attempt, settings.MaximumAttempts - attemptsLeft + 1))
    │       │
    │       ├── If attemptsLeft <= 0:
    │       │       └── throw TimeoutException("Failed to send...", exception)
    │       │
    │       ├── Else:
    │       │       ├── Thread.Sleep(settings.DelayBetweenAttemptsInSeconds * 1000)
    │       │       └── Send(request, attemptsLeft - 1)  ◄─── RECURSIVE CALL
    │       │
    │       └── (Recursion continues until success or attempts exhausted)
    │
    └── On Other Exception:
            ├── logger.Error(MyOperation.SendEmail, OperationStatus.Failure, exception, logInfos)
            └── throw  ◄─── Rethrows original exception
```

**Retry Flow Detail:**

```
Initial call: Send(request, MaximumAttempts=3)
    │
    ├── Attempt 1 (attemptsLeft=3): timeout
    │       ├── Log Warn: Attempt=1
    │       ├── Sleep 5s
    │       └── Send(request, 2)
    │
    ├── Attempt 2 (attemptsLeft=2): timeout
    │       ├── Log Warn: Attempt=2
    │       ├── Sleep 5s
    │       └── Send(request, 1)
    │
    ├── Attempt 3 (attemptsLeft=1): timeout
    │       ├── Log Warn: Attempt=3
    │       ├── Sleep 5s
    │       └── Send(request, 0)
    │
    ├── Attempt 4 (attemptsLeft=0): timeout
    │       ├── Log Warn: Attempt=4
    │       └── Throw TimeoutException
    │
    └── Total: 4 SMTP sends (1 initial + 3 retries)
```

**Important:** `MaximumAttempts` = number of **retries after initial attempt**, not total attempts.

---

## 4. SmtpClientWrapper — SMTP Submission

```
SmtpClientWrapper.Send(message)
    │
    └── smtpClient.Send(message)  ◄─── System.Net.Mail.SmtpClient
            │
            ├── Connects to settings.Host:settings.Port
            ├── Negotiates TLS (EnableSsl=true)
            ├── Authenticates with NetworkCredential(settings.Username, settings.Password)
            ├── Sends message (MAIL FROM, RCPT TO, DATA)
            ├── Waits for server response (Timeout = 200,000 ms)
            └── Returns on success or throws SmtpException
```

**Timeout Behaviour:**
- `SmtpClient.Timeout = 200_000` ms applies to **each** SMTP operation (connect, auth, send)
- Timeout throws `SmtpException` with message containing "timed out"/"timeout"/"Timeout"
- `EmailService` catches and retries based on message text

---

## 5. Error Paths

### 5.1 API Key Missing/Invalid
```
Client: POST /Email (no Authorization header or wrong key)
    │
    ▼
NuciApiController.ProcessRequest
    │
    ├── Extracts API key from header
    ├── Compares with SecuritySettings.ApiKey
    └── On mismatch: Returns 401/403 (package-defined)
```

### 5.2 Model Validation Failure
```
Client: POST /Email { "recipient": "invalid" }  // missing subject, body
    │
    ▼
[ApiController] automatic validation
    │
    ├── Checks [Required] on Recipient, Subject, Body
    └── On failure: Returns 400 with validation errors (package-defined)
```

### 5.3 SMTP Timeout — Retries Exhausted
```
EmailService.Send()
    │
    ├── All 4 attempts timeout
    │
    └── Throws TimeoutException
            │
            ▼
NuciApiController.ProcessRequest catches exception
    │
    ▼
NuciApiExceptionHandling middleware
    │
    └── Translates to HTTP 5xx (package-defined)
```

### 5.4 SMTP Non-Timeout Failure (e.g., auth failure, rejected)
```
EmailService.Send()
    │
    ├── SmtpException: "Authentication failed"
    │
    ├── Logs Error with exception
    │
    └── Rethrows SmtpException
            │
            ▼
NuciApiController.ProcessRequest catches exception
    │
    ▼
NuciApiExceptionHandling middleware
    │
    └── Translates to HTTP 5xx (package-defined)
```

### 5.5 General Exception (e.g., network unreachable)
```
EmailService.Send()
    │
    ├── InvalidOperationException: "Unexpected failure"
    │
    ├── Logs Error with exception
    │
    └── Rethrows InvalidOperationException
            │
            ▼
NuciApiController.ProcessRequest catches exception
    │
    ▼
NuciApiExceptionHandling middleware
    │
    └── Translates to HTTP 5xx (package-defined)
```

---

## 6. Logging Flow

```
EmailService.Send()
    │
    ├── logger.Info(Started)  ──► NuciLogger  ──► Configured destination (file: logfile.log)
    │
    ├── logger.Info(Success)  ──► NuciLogger  ──► Configured destination
    │
    ├── logger.Warn(Failure, Attempt=N)  ──► NuciLogger  ──► Configured destination
    │
    └── logger.Error(Failure, Exception)  ──► NuciLogger  ──► Configured destination

NuciAPI Request Logging (middleware)
    │
    └── Logs HTTP request metadata (method, path, status, duration, etc.)
            ──► NuciLogger  ──► Configured destination
```

**Log Record Structure (NuciLog):**
```
Operation: SendEmail
Status: Started | Success | Failure
Metadata:
    SenderAddress: notifier@nucilandia.ro
    SenderName: Notifier (or request sender)
    Recipient: alex@example.com
    Subject: Delivery complete
    Attempt: 1 (only on timeout warnings)
Exception: (only on error logs)
Timestamp: (added by NuciLog)
```

---

## 7. Configuration Flow

```
appsettings.json (copied to output)
    │
    ▼
Host.CreateDefaultBuilder()
    │
    ├── Adds appsettings.json provider
    ├── Adds environment variables (SecuritySettings__ApiKey, SmtpSettings__Host, etc.)
    ├── Adds command-line args
    └── Adds user secrets (Development)
    │
    ▼
Startup.ConfigureServices
    │
    ├── services.AddConfigurations(Configuration)
    │       │
    │       ├── new SecuritySettings()
    │       │       └── Configuration.Bind("SecuritySettings", securitySettings)
    │       │       └── services.AddSingleton(securitySettings)
    │       │
    │       ├── new SmtpSettings()
    │       │       └── Configuration.Bind("SmtpSettings", smtpSettings)
    │       │       └── services.AddSingleton(smtpSettings)
    │       │
    │       └── AddNuciLoggerSettings(configuration)  ◄─── NuciLog package
    │
    └── services.AddCustomServices()
            │
            ├── AddSingleton<ILogger, NuciLogger>()
            ├── AddSingleton<ISmtpClient, SmtpClientWrapper>()
            │       └── Constructor receives SmtpSettings (singleton)
            │       └── Builds System.Net.Mail.SmtpClient once
            │
            └── AddSingleton<IEmailService, EmailService>()
                    └── Constructor receives SmtpSettings, ISmtpClient, ILogger (all singletons)
```

**Environment Variable Overrides:**
| Setting | Environment Variable |
|---------|---------------------|
| SecuritySettings.ApiKey | `SecuritySettings__ApiKey` |
| SmtpSettings.Host | `SmtpSettings__Host` |
| SmtpSettings.Port | `SmtpSettings__Port` |
| SmtpSettings.Username | `SmtpSettings__Username` |
| SmtpSettings.Password | `SmtpSettings__Password` |
| SmtpSettings.SenderName | `SmtpSettings__SenderName` |
| SmtpSettings.MaximumAttempts | `SmtpSettings__MaximumAttempts` |
| SmtpSettings.DelayBetweenAttemptsInSeconds | `SmtpSettings__DelayBetweenAttemptsInSeconds` |
| NuciLoggerSettings.LogFilePath | `NuciLoggerSettings__LogFilePath` |
| NuciLoggerSettings.IsFileOutputEnabled | `NuciLoggerSettings__IsFileOutputEnabled` |

---

## 8. Concurrency Behaviour

```
Request 1 ──────► EmailController.Send() ──────► EmailService.Send() ──────► SmtpClientWrapper.Send()
    │                                                                              │
    │                                                                              ▼
    │                                                                    System.Net.Mail.SmtpClient.Send()
    │                                                                              │
Request 2 ──────► EmailController.Send() ──────► EmailService.Send() ──────► SmtpClientWrapper.Send()
    │                                                                              │
    │                                                                              ▼
    │                                                                    System.Net.Mail.SmtpClient.Send()  ◄─── SAME INSTANCE!
    │
    ▼
... concurrent requests share the same SmtpClient instance ...
```

**Implications:**
- `System.Net.Mail.SmtpClient` is **not thread-safe** for concurrent `Send` calls
- No locking, pooling, or connection management in repository
- Concurrent requests may cause:
  - Interleaved SMTP protocol commands
  - Exceptions from `SmtpClient` internal state corruption
  - Undefined delivery behaviour
- Each `MailMessage` is properly disposed (per-request), but the client is not

---

## 9. Shutdown Sequence

```
SIGTERM / Ctrl+C
    │
    ▼
Host stops accepting new requests
    │
    ▼
In-flight requests:
    ├── EmailService.Send() may be mid-retry (Thread.Sleep)
    ├── SmtpClient.Send() may be mid-SMTP transaction
    └── No graceful shutdown handling for in-flight operations
    │
    ▼
ServiceProvider.Dispose()
    │
    ├── Disposes NuciLogger (flushes logs)
    ├── Disposes EmailService (no disposal logic)
    ├── Disposes SmtpClientWrapper (no disposal logic — SmtpClient not disposed)
    └── Disposes Settings objects
    │
    ▼
Process exits
```

**Note:** No `IHostApplicationLifetime` handling, no cancellation token propagation, no in-flight request draining.

---

## 10. HMAC Request Flow (Optional)

```
Client: POST /Email
    Headers:
        Authorization: Bearer <api-key>
        X-HMAC: <url-encoded-token>
    Body: { "sender": "...", "recipient": "...", "subject": "...", "body": "..." }
    │
    ▼
NuciApiController.ProcessRequest
    │
    ├── Validates API key
    │
    ├── If X-HMAC present:
    │       ├── Extracts token
    │       ├── Reconstructs canonical request string using HMAC order:
    │       │       Sender (1), Recipient (2), Subject (5), Body (6)
    │       ├── Computes expected HMAC with shared secret
    │       └── Compares (constant-time)
    │
    └── On HMAC mismatch: Returns 401/403 (package-defined)
```

**HMAC Order Values (from SendEmailRequest):**
| Field | Order |
|-------|-------|
| Sender | 1 |
| Recipient | 2 |
| Subject | 5 |
| Body | 6 |

**Note:** Gaps in order (3, 4 unused) are intentional for compatibility with broader NuciAPI contract.