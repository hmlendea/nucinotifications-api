# NuciNotifications API — Repository Overview

## Purpose

NuciNotifications API is a compact, self-contained ASP.NET Core 10 web service that accepts authorised HTTP requests and submits plain-text email through an operator-configured SMTP server. It centralises SMTP credentials so calling applications do not require direct access to them.

## Scope

This repository contains:
- **NuciNotifications.API** — The deployable ASP.NET Core web application
- **NuciNotifications.API.UnitTests** — NUnit/Moq unit test suite verifying email delivery orchestration

## Key Capabilities

| Capability | Description |
|------------|-------------|
| Single endpoint | `POST /Email` accepts JSON with sender (optional), recipient, subject, body |
| Credential isolation | API key and SMTP credentials configured by operator, not callers |
| SMTP delivery | Authenticated TLS submission with configurable timeout (200s) |
| Retry policy | Configurable timeout retries with delay between attempts |
| Structured logging | Delivery operations logged via NuciLog (file output by default) |
| Security middleware | Scanner protection, replay protection, HMAC support via NuciAPI packages |
| Self-contained deployment | Published as single-file executables for Linux/macOS/Windows (ARM64, x64) |

## Architecture Summary

```
┌─────────────────────────────────────────────────────────────────┐
│                     ASP.NET Core Host Process                   │
├─────────────────────────────────────────────────────────────────┤
│  Middleware Pipeline                                            │
│  ├── NuciAPI Exception Handling                                 │
│  ├── NuciAPI Scanner Protection                                 │
│  ├── NuciAPI Replay Protection                                  │
│  ├── NuciAPI Request Logging                                    │
│  ├── HTTPS Redirection                                          │
│  ├── Static Files                                               │
│  ├── Routing                                                    │
│  └── Authorization                                              │
├─────────────────────────────────────────────────────────────────┤
│  EmailController (POST /Email)                                  │
│  ├── API-key authorisation (NuciApiController.ProcessRequest)  │
│  └── Delegates to IEmailService                                 │
├─────────────────────────────────────────────────────────────────┤
│  EmailService (singleton)                                       │
│  ├── Constructs MailMessage                                     │
│  ├── Selects sender display name                                │
│  ├── Logs delivery metadata (sender, recipient, subject)       │
│  ├── Invokes ISmtpClient synchronously                          │
│  └── Retries on recognised timeout exceptions                   │
├─────────────────────────────────────────────────────────────────┤
│  SmtpClientWrapper (singleton)                                  │
│  ├── Wraps System.Net.Mail.SmtpClient                           │
│  ├── Configures credentials, TLS, 200s timeout                  │
│  └── Single retained instance for process lifetime              │
└─────────────────────────────────────────────────────────────────┘
```

## External Boundaries

| Boundary | Direction | Protocol | Owner |
|----------|-----------|----------|-------|
| Calling applications | Inbound | HTTPS POST /Email | Caller |
| Configuration/secret providers | Inbound | .NET config providers | Operator |
| SMTP server | Outbound | SMTP/TLS | Operator-configured |
| Log destination | Outbound | NuciLog (file by default) | Operator-configured |

## Design Constraints

- **Synchronous delivery** — HTTP request blocks until SMTP submission completes or fails
- **No durable state** — No database, queue, or delivery ledger; success = SMTP submission without exception
- **Shared SMTP client** — Singleton `SmtpClient` without repository-defined concurrency control
- **Configuration snapshot** — Settings bound once at startup; no runtime reload or validation
- **Package-owned HTTP semantics** — Authorisation, replay, logging, exception mapping in NuciAPI packages
- **Timeout classification** — String-based detection of "timed out"/"timeout"/"Timeout" in exception messages

## Repository Structure

```
NuciNotifications.slnx
├── NuciNotifications.API/
│   ├── Configuration/          # SecuritySettings, SmtpSettings
│   ├── Controllers/            # EmailController
│   ├── Logging/                # MyLogInfoKey, MyOperation
│   ├── Requests/               # SendEmailRequest
│   ├── Service/                # EmailService, IEmailService, ISmtpClient, SmtpClientWrapper
│   ├── ServiceCollectionExtensions.cs
│   ├── Startup.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── NuciNotifications.API.csproj
├── NuciNotifications.API.UnitTests/
│   ├── Service/                # EmailServiceTests.cs
│   └── NuciNotifications.API.UnitTests.csproj
├── .github/workflows/dotnet.yml
├── ARCHITECTURE.md
├── PRIVACY.md
├── SECURITY.md
├── README.md
├── LICENSE
└── release.sh
```

## Technology Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 10.0 |
| Web Framework | ASP.NET Core |
| SMTP | System.Net.Mail.SmtpClient |
| Logging | NuciLog 1.2.1 / NuciLog.Core 3.1.0 |
| Security | NuciAPI middleware packages, NuciSecurity.HMAC 4.1.3 |
| Testing | NUnit 4.6.1, Moq 4.20.72 |
| CI | GitHub Actions (ubuntu-latest) |
| Release | Self-contained single-file executables |