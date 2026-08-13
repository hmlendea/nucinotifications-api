[![Donate](https://img.shields.io/badge/-%E2%99%A5%20Donate-%23ff69b4)](https://hmlendea.go.ro/funding)
[![Latest Release](https://img.shields.io/github/v/release/hmlendea/nucinotifications-api)](https://github.com/hmlendea/nucinotifications-api/releases/latest)
[![Build Status](https://github.com/hmlendea/nucinotifications-api/actions/workflows/dotnet.yml/badge.svg)](https://github.com/hmlendea/nucinotifications-api/actions/workflows/dotnet.yml)
[![License](https://img.shields.io/github/license/hmlendea/nucinotifications-api)](https://github.com/hmlendea/nucinotifications-api/blob/master/LICENSE)

# NuciNotifications API

NuciNotifications API is a compact ASP.NET Core service that accepts authorised HTTP requests and submits plain-text email through an operator-configured SMTP server. It centralises SMTP credentials so calling applications do not require direct access to them.

## 📑 Table of Contents

- [Table of Contents](#table-of-contents)
- [Capabilities](#capabilities)
- [Usage](#usage)
- [Known Limitations](#known-limitations)
- [System Requirements](#system-requirements)
- [Installation](#installation)
  - [Manual Installation](#manual-installation)
- [Configuration](#configuration)
  - [Configuration Files](#configuration-files)
  - [Settings](#settings)
  - [Reload Behaviour](#reload-behaviour)
  - [Secret Management](#secret-management)
  - [Network Ports](#network-ports)
- [Compatibility](#compatibility)
- [Integrations](#integrations)
- [Authentication and Authorisation](#authentication-and-authorisation)
- [Extensibility](#extensibility)
- [Privacy and Data](#privacy-and-data)
  - [Data Locations](#data-locations)
  - [Telemetry Controls](#telemetry-controls)
- [Development](#development)
  - [Requirements](#requirements)
  - [Setup](#setup)
  - [Build](#build)
  - [Run](#run)
  - [Test](#test)
  - [Continuous Integration](#continuous-integration)
  - [Release](#release)
  - [Dependencies](#dependencies)
- [Project Structure](#project-structure)
  - [Projects and Packages](#projects-and-packages)
  - [Directories](#directories)
- [Architecture](#architecture)
- [Deployment](#deployment)
- [Contributing](#contributing)
- [Project Engagement](#project-engagement)
- [License](#license)

## ✨ Capabilities

- Submit plain-text email through one `POST /Email` endpoint.
- Centralise SMTP credentials outside calling applications.
- Protect requests with an API key and integrate with the NuciAPI HMAC request contract.
- Configure SMTP identity, timeout retries, retry delays, and structured file logging through standard .NET configuration providers.
- Submit email through authenticated SMTP with TLS enabled.
- Record delivery attempts and outcomes without adding message bodies or credentials to application-generated delivery metadata.

## 🚀 Usage

After defining `NUCINOTIFICATIONS_URL` and `NUCINOTIFICATIONS_API_KEY` in the caller environment, submit a message with:

```bash
curl --fail-with-body \
  --request POST "${NUCINOTIFICATIONS_URL}/Email" \
  --header "Authorization: Bearer ${NUCINOTIFICATIONS_API_KEY}" \
  --header "Content-Type: application/json" \
  --data '{
    "sender": "Meridian Operations",
    "recipient": "alex@example.com",
    "subject": "Delivery complete",
    "body": "The scheduled export completed successfully."
  }'
```

The request body contains:
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `sender` | `string` | No | Display name for the sender. An empty or omitted value uses `smtpSettings.senderName`. |
| `recipient` | `string` | Yes | Recipient email address. |
| `subject` | `string` | Yes | Message subject. |
| `body` | `string` | Yes | Plain-text message body. |

A successful SMTP submission returns HTTP `200` with the standard NuciAPI success response. For signed requests, add a URL-encoded token through the `X-HMAC` header. The email fields retain HMAC order values `sender: 1`, `recipient: 2`, `subject: 5`, and `body: 6`.

## ⚠️ Known Limitations

- SMTP submission and retry delays are synchronous, so each request remains occupied until submission succeeds or fails.
- `smtpSettings.maximumAttempts` represents timeout retries after the initial submission; its default of `3` permits up to four SMTP submissions.
- A timeout does not establish whether the SMTP server accepted a preceding attempt. Retries can therefore submit the same message more than once.
- The service has no queue, delivery ledger, status endpoint, or reconciliation process. HTTP success confirms SMTP submission, not recipient delivery.
- Runtime services share one singleton SMTP client without repository-defined concurrency control.

## 🖥️ System Requirements

Published archives are self-contained and do not require a separately installed .NET runtime.

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| Linux | ARM, ARM64, or x64 environment matching a published archive | N/A |
| macOS | ARM64 or x64 environment matching a published archive | N/A |
| Windows | ARM64 or x64 environment matching a published archive | N/A |
| SMTP service | Authenticated endpoint reachable through the configured host and port with TLS support | N/A |

## 📦 Installation

[![Obtain it from GitHub](https://raw.githubusercontent.com/hmlendea/readme-assets/master/badges/stores/github.png)](https://github.com/hmlendea/nucinotifications-api/releases)

### Manual Installation

1. Download the archive for the required operating system and architecture from [GitHub Releases](https://github.com/hmlendea/nucinotifications-api/releases/latest).
2. Extract the archive into the deployment directory.
3. Configure the API key, SMTP connection, and logging destination before launching the service.
4. From the extracted directory, launch `NuciNotifications.API` on Linux or macOS, or `NuciNotifications.API.exe` on Windows.

Linux and macOS:

```bash
./NuciNotifications.API
```

Windows:

```powershell
.\NuciNotifications.API.exe
```

## ⚙️ Configuration

The service reads `appsettings.json` through the default .NET host configuration pipeline. Standard providers can override file values, including environment variables that use `__` between section and key names.

```json
{
  "securitySettings": {
    "apiKey": "<api-key>"
  },
  "smtpSettings": {
    "host": "<smtp-host>",
    "port": 587,
    "username": "<smtp-username>",
    "password": "<smtp-password>",
    "senderName": "Notifier",
    "maximumAttempts": 3,
    "delayBetweenAttemptsInSeconds": 5
  },
  "nuciLoggerSettings": {
    "logFilePath": "logfile.log",
    "isFileOutputEnabled": true
  }
}
```

Environment-variable override examples include:
- `SecuritySettings__ApiKey`
- `SmtpSettings__Host`
- `SmtpSettings__Port`
- `SmtpSettings__Username`
- `SmtpSettings__Password`
- `NuciLoggerSettings__LogFilePath`
- `NuciLoggerSettings__IsFileOutputEnabled`

The repository contains substitution tokens rather than operational credentials. Replace them through a deployment-specific configuration or secret provider.

### Configuration Files

| File | Scope | Purpose |
|------|-------|---------|
| `NuciNotifications.API/appsettings.json` | Application and published output | Defines API-key, SMTP, and NuciLog defaults. |

### Settings

The subsequent settings are recognised:
| Section | Key | Type | Default | Required | Description |
|---------|-----|------|---------|----------|-------------|
| `securitySettings` | `apiKey` | `string` | — | Yes | API key accepted by the `Authorization` header policy. |
| `smtpSettings` | `host` | `string` | — | Yes | SMTP server hostname. |
| `smtpSettings` | `port` | `integer` | `587` | No | SMTP server port. |
| `smtpSettings` | `username` | `string` | — | Yes | SMTP username and sender email address. |
| `smtpSettings` | `password` | `string` | — | Yes | SMTP password. |
| `smtpSettings` | `senderName` | `string` | `Notifier` | No | Display name used when a request omits `sender`. |
| `smtpSettings` | `maximumAttempts` | `integer` | `3` | No | Number of timeout retries after the initial submission attempt. |
| `smtpSettings` | `delayBetweenAttemptsInSeconds` | `integer` | `5` | No | Delay between timeout retries, in seconds. |
| `nuciLoggerSettings` | `logFilePath` | `string` | `logfile.log` | No | Destination for file logging. |
| `nuciLoggerSettings` | `isFileOutputEnabled` | `boolean` | `true` | No | Activates the default file logger destination. |

### Reload Behaviour

Configuration is bound into singleton settings during process construction. Restart the process to apply configuration modifications.

### Secret Management

Supply `SecuritySettings__ApiKey` and `SmtpSettings__Password` through an appropriate deployment secret provider or protected environment. Do not commit operational API keys or SMTP credentials. The application retains bound values in process memory for its lifetime.

### Network Ports

The repository does not assign a fixed inbound port; configure Kestrel or the hosting reverse proxy through standard ASP.NET Core host settings.

| Port | Protocol | Direction | Purpose | Required |
|------|----------|-----------|---------|----------|
| Operator-defined | HTTP or HTTPS | Inbound | Exposes the API through Kestrel or a reverse proxy. | Yes |
| `587` by default | SMTP with TLS | Outbound | Submits messages to the configured SMTP server. | Yes |

## 🧩 Compatibility

| Component | Supported Versions | Notes |
|-----------|--------------------|-------|
| Release archives | `linux-arm`, `linux-arm64`, `linux-x64`, `osx-arm64`, `osx-x64`, `win-arm64`, and `win-x64` | Published as self-contained .NET 10 archives. |
| Source execution | .NET 10.0 SDK | Required to restore, compile, test, or execute from source. |
| SMTP | Servers compatible with `System.Net.Mail.SmtpClient`, username/password authentication, and TLS | The client uses a 200-second timeout. |

## 🔌 Integrations

| Integration | Compatibility | Purpose | Required |
|-------------|---------------|---------|----------|
| SMTP server | Authenticated SMTP with TLS | Relays submitted plain-text email. | Yes |
| NuciAPI packages | .NET 10 package versions declared by the API project | Provide request contracts, API-key authorisation, exception handling, logging, scanner protection, and replay protection. | Yes |
| NuciLog | `NuciLog` 1.2.1 and `NuciLog.Core` 3.0.0 | Records structured request and delivery operations. | Yes |

## 🔐 Authentication and Authorisation

Every `POST /Email` request must present the configured API key through the standard `Authorization` header:

```http
Authorization: Bearer <api-key>
```

The controller removes the case-insensitive `Bearer` prefix and compares the remaining value with `securitySettings.apiKey`. The service defines no roles or scopes.

Clients using the NuciAPI HMAC flow can also provide a URL-encoded token:

```http
X-HMAC: <URL-encoded-HMAC-token>
```

The controller transfers this value to the inherited NuciAPI request contract. HMAC validation and replay-protection semantics remain defined by the pinned NuciAPI and NuciSecurity packages.

## 🧱 Extensibility

The SMTP adapter is the repository's public transport extension point. Implementations remain synchronous because the application service owns the `MailMessage` lifetime and expects submission to complete or throw before returning.

| Extension Point | Contract | Purpose |
|-----------------|----------|---------|
| SMTP transport | `ISmtpClient.Send(MailMessage)` | Substitute the SMTP adapter or provide a test transport through dependency injection. |

## 🛡️ Privacy and Data

| Data | Purpose | Storage | Retention | Optional |
|------|---------|---------|-----------|----------|
| API key and SMTP credentials | Authorise API calls and authenticate SMTP submissions. | Configuration provider and singleton process memory. | Process lifetime; provider retention is operator-defined. | No |
| Sender, recipient, subject, and body | Construct and submit an email. | Request memory and the configured SMTP server. | Request lifetime within this service; SMTP-provider retention is external. | No |
| Sender address, sender display name, recipient, subject, and outcome | Record delivery operations. | Configured NuciLog destination; `logfile.log` by default. | Operator-defined. | Yes |

The application-generated delivery metadata excludes message bodies and credentials. Request-logging conduct inside external middleware and SMTP-provider data practices remain governed by those components.

### Data Locations

| Platform or Scope | Location | Contents |
|-------------------|----------|----------|
| Runtime working directory | `./logfile.log` | Structured request and delivery records when file output is active. |

### Telemetry Controls

The repository contains no analytics or telemetry integration. To deactivate the default file logger destination, set `NuciLoggerSettings__IsFileOutputEnabled` to `false`; any alternate NuciLog destination remains operator-configured.

## 🛠️ Development

### Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Git](https://git-scm.com/)

### Setup

```bash
git clone https://github.com/hmlendea/nucinotifications-api.git
cd nucinotifications-api
dotnet restore
```

Configure non-production API-key and SMTP values before local execution. Do not commit operational credentials.

### Build

```bash
dotnet build --no-restore
```

### Run

```bash
dotnet run --project NuciNotifications.API/NuciNotifications.API.csproj
```

Kestrel uses the URLs supplied by the active ASP.NET Core host configuration.

### Test

```bash
dotnet test --no-build --verbosity normal
```

The NUnit suite verifies email construction, sender selection, logging, timeout retries, and exception propagation without contacting an SMTP server.

### Continuous Integration

The `.github/workflows/dotnet.yml` workflow restores, compiles, and tests the solution on Ubuntu for pushes and pull requests targeting `master`. Reproduce its checks with:

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build --verbosity normal
```

### Release

The repository includes `release.sh`, which delegates to the upstream deployment script used by the project maintainer.

```bash
bash ./release.sh 1.1.2
```

This script downloads and executes an external release helper from `https://raw.githubusercontent.com/hmlendea/deployment-scripts/master/release/dotnet/10.0.sh`.

**Note:** Piping into `bash` is an intensely controversial topic. Please review any external scripts before running them in your environment!

### Dependencies

| Package | Version | Scope | Purpose |
|---------|---------|-------|---------|
| `NuciAPI` | `3.5.1` | Runtime | Supplies shared API request and response contracts. |
| `NuciAPI.Controllers` | `2.3.1` | Runtime | Supplies controller processing and API-key authorisation. |
| `NuciAPI.Middleware` | `2.0.2` | Runtime | Supplies shared middleware contracts. |
| `NuciAPI.Middleware.ExceptionHandling` | `1.0.1` | Runtime | Translates unhandled request exceptions. |
| `NuciAPI.Middleware.Logging` | `1.0.1` | Runtime | Records HTTP request operations. |
| `NuciAPI.Middleware.Security` | `1.0.5` | Runtime | Supplies scanner and replay protection. |
| `NuciLog` | `1.2.1` | Runtime | Implements the configured structured logger. |
| `NuciLog.Core` | `3.0.0` | Runtime | Supplies logging contracts and operation records. |
| `NuciSecurity.HMAC` | `4.1.3` | Runtime | Defines HMAC field-order metadata. |
| `Microsoft.NET.Test.Sdk` | `18.7.0` | Test | Hosts the .NET test process. |
| `Moq` | `4.20.72` | Test | Provides test substitutes for SMTP and logging contracts. |
| `NUnit` | `4.6.1` | Test | Defines and executes the unit-test suite. |
| `NUnit3TestAdapter` | `6.2.0` | Test | Integrates NUnit discovery with the .NET test SDK. |

## 🗂️ Project Structure

The solution separates the deployable API from its automated unit tests.

### Projects and Packages

| Project | Type | Purpose |
|---------|------|---------|
| `NuciNotifications.API/NuciNotifications.API.csproj` | ASP.NET Core web application | Hosts the HTTP endpoint, SMTP integration, configuration, and logging. |
| `NuciNotifications.API.UnitTests/NuciNotifications.API.UnitTests.csproj` | NUnit test project | Verifies email delivery orchestration without external network access. |

### Directories

| Directory | Purpose |
|-----------|---------|
| `NuciNotifications.API/Configuration` | Defines API-key and SMTP settings. |
| `NuciNotifications.API/Controllers` | Defines the `POST /Email` HTTP boundary. |
| `NuciNotifications.API/Requests` | Defines the JSON and HMAC request contract. |
| `NuciNotifications.API/Service` | Implements message construction, retry policy, and SMTP adaptation. |
| `NuciNotifications.API/Logging` | Defines operation and metadata identifiers. |
| `NuciNotifications.API.UnitTests/Service` | Contains focused service unit tests. |
| `.github/workflows` | Defines continuous-integration automation. |

## 🏗️ Architecture

See the [architecture documentation](./ARCHITECTURE.md) for the system context, principal components, runtime flows, ownership boundaries, dependencies, constraints, and extension points.

## 🚢 Deployment

Deploy one self-contained release archive as an ASP.NET Core process. The operator must provide inbound HTTP or HTTPS routing, outbound connectivity to the SMTP server, protected configuration values, and write access for the configured file log destination.

The repository contains no database, queue, container manifest, service manifest, or orchestration definition. Multiple replicas submit and log independently, and the application retains no shared delivery state.

## 🤝 Contributing

You are welcome to submit any suggestion, feedback, or modification to this project.

When doing so, please:
- Maintain cross-platform compatibility
- Preserve the existing public contract unless a breaking change is intentional
- Submit focused pull requests that conform to the existing code style
- Maintain your branch synchronised with `master`
- Revise the documentation when functionality changes
- Properly test all modifications, including edge cases and error conditions
- Add tests for additional or modified functionality

## 💝 Project Engagement

Discovered a problem or have a suggestion? [Open an issue](https://github.com/hmlendea/nucinotifications-api/issues)!

If you find this project useful, consider [funding it](https://hmlendea.go.ro/funding) or starring ⭐️ it on GitHub!

[![Donate](https://raw.githubusercontent.com/hmlendea/readme-assets/master/donate_generic.png)](https://hmlendea.go.ro/funding)

## 📄 License

This project is being distributed under the `GNU General Public License v3.0`.
See [LICENSE](./LICENSE) for further information.