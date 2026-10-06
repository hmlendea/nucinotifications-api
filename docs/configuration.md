# Configuration Reference

This document provides complete configuration details for the NuciNotifications API.

---

## Configuration Sources

The application uses the standard ASP.NET Core configuration pipeline via `Host.CreateDefaultBuilder()`:

1. **appsettings.json** — Base configuration (copied to output directory)
2. **appsettings.{Environment}.json** — Environment-specific overrides
3. **Environment variables** — Using `__` as section separator (e.g., `SecuritySettings__ApiKey`)
4. **Command-line arguments** — Passed at process start
5. **User secrets** — Development only (`dotnet user-secrets`)
6. **Azure Key Vault / other providers** — If configured by operator

**Provider precedence:** Later sources override earlier ones.

---

## appsettings.json Structure

```json
{
  "securitySettings": {
    "apiKey": "[[NUCINOTIFICATIONS_API_KEY]]"
  },
  "smtpSettings": {
    "host": "[[SMTP_HOST]]",
    "port": "[[SMTP_PORT]]",
    "username": "[[SMTP_USERNAME]]",
    "password": "[[SMTP_PASSWORD]]",
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

**Note:** The repository contains substitution tokens (`[[...]]`), not operational values. Operators must replace these via environment variables, secret providers, or deployment-time transformation.

---

## SecuritySettings

**Configuration Section:** `securitySettings`

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| apiKey | string | **Yes** | — | API key for `Authorization: Bearer <key>` header validation |

**Environment Variable:** `SecuritySettings__ApiKey`

**Validation:** None — missing/empty key causes all requests to fail authorisation at request time.

**Security:** Must be injected via protected secret provider in production. Never commit real keys.

---

## SmtpSettings

**Configuration Section:** `smtpSettings`

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| host | string | **Yes** | — | SMTP server hostname |
| port | int | No | 587 | SMTP server port |
| username | string | **Yes** | — | SMTP username (also used as sender email address) |
| password | string | **Yes** | — | SMTP password |
| senderName | string | No | "Notifier" | Display name when request omits `sender` field |
| maximumAttempts | int | No | 3 | **Retries after initial attempt** (3 = 4 total sends) |
| delayBetweenAttemptsInSeconds | int | No | 5 | Delay between retry attempts |

**Environment Variables:**
- `SmtpSettings__Host`
- `SmtpSettings__Port`
- `SmtpSettings__Username`
- `SmtpSettings__Password`
- `SmtpSettings__SenderName`
- `SmtpSettings__MaximumAttempts`
- `SmtpSettings__DelayBetweenAttemptsInSeconds`

**Behavioural Notes:**

### maximumAttempts Semantics
```
maximumAttempts = 3 (default)
    ├── Initial attempt (always executed)
    ├── Retry 1 (after timeout)
    ├── Retry 2 (after timeout)
    └── Retry 3 (after timeout)
    = 4 total SMTP Send calls maximum
```

### Timeout Detection
The retry logic only triggers on `SmtpException` where `Message` contains:
- `"timed out"` (lowercase)
- `"timeout"` (lowercase)
- `"Timeout"` (capitalised)

Other `SmtpException` messages (auth failure, rejected, etc.) are **not retried** — logged as error and rethrown immediately.

### Port and TLS
- Default port 587 (submission port with STARTTLS)
- `EnableSsl = true` hardcoded in `SmtpClientWrapper`
- Operator must ensure SMTP server supports TLS on configured port

---

## NuciLoggerSettings

**Configuration Section:** `nuciLoggerSettings`

Managed by `NuciLog` package via `AddNuciLoggerSettings()`.

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| logFilePath | string | No | "logfile.log" | Path for file log output (relative to working directory) |
| isFileOutputEnabled | bool | No | true | Enables/disables default file logger |

**Environment Variables:**
- `NuciLoggerSettings__LogFilePath`
- `NuciLoggerSettings__IsFileOutputEnabled`

**Behaviour:**
- When `isFileOutputEnabled: true`, structured logs written to `logfile.log` in process working directory
- File permissions, rotation, retention, and aggregation are **operator responsibilities**
- Alternative NuciLog destinations can be configured via package-specific settings (not documented here)

---

## Complete Environment Variable Reference

| Setting | Environment Variable | Example |
|---------|---------------------|---------|
| API Key | `SecuritySettings__ApiKey` | `SecuritySettings__ApiKey=sk-prod-abc123` |
| SMTP Host | `SmtpSettings__Host` | `SmtpSettings__Host=mail.example.com` |
| SMTP Port | `SmtpSettings__Port` | `SmtpSettings__Port=587` |
| SMTP Username | `SmtpSettings__Username` | `SmtpSettings__Username=notifier@example.com` |
| SMTP Password | `SmtpSettings__Password` | `SmtpSettings__Password=secret123` |
| SMTP Sender Name | `SmtpSettings__SenderName` | `SmtpSettings__SenderName="My App"` |
| Max Attempts | `SmtpSettings__MaximumAttempts` | `SmtpSettings__MaximumAttempts=5` |
| Retry Delay | `SmtpSettings__DelayBetweenAttemptsInSeconds` | `SmtpSettings__DelayBetweenAttemptsInSeconds=10` |
| Log File Path | `NuciLoggerSettings__LogFilePath` | `NuciLoggerSettings__LogFilePath=/var/log/nucinotifications.log` |
| File Logging Enabled | `NuciLoggerSettings__IsFileOutputEnabled` | `NuciLoggerSettings__IsFileOutputEnabled=false` |

---

## Configuration Binding Behaviour

**Location:** `ServiceCollectionExtensions.cs` → `AddConfigurations()`

```csharp
SecuritySettings securitySettings = new();
SmtpSettings smtpSettings = new();

configuration.Bind(nameof(SecuritySettings), securitySettings);
configuration.Bind(nameof(SmtpSettings), smtpSettings);

return services
    .AddSingleton(securitySettings)
    .AddSingleton(smtpSettings)
    .AddNuciLoggerSettings(configuration);
```

**Key Characteristics:**

1. **Snapshot binding** — Values read once at startup; runtime configuration changes **not observed** without process restart
2. **No validation** — Missing required fields, invalid formats, out-of-range numbers, or unresolved tokens (`[[...]]`) are not detected at startup
3. **Case-insensitive** — Section and property names matched case-insensitively
4. **Simple types only** — No complex object graphs, collections, or custom converters
5. **Singleton lifetime** — Same instance injected everywhere; no scoped or transient configuration

---

## Startup Validation Gaps

The application **does not** perform explicit validation at startup for:

| Gap | Consequence |
|-----|-------------|
| Missing `SecuritySettings.ApiKey` | All requests return 401 at request time |
| Missing `SmtpSettings.Host` | `SmtpClient` construction fails at first request |
| Missing `SmtpSettings.Username` | SMTP auth fails at first request |
| Missing `SmtpSettings.Password` | SMTP auth fails at first request |
| Invalid `SmtpSettings.Port` (e.g., 0, negative, >65535) | `SmtpClient` throws at first request |
| Invalid `MaximumAttempts` (negative) | Retry logic behaves unexpectedly |
| Invalid `DelayBetweenAttemptsInSeconds` (negative) | `Thread.Sleep` throws `ArgumentOutOfRangeException` |
| Unresolved substitution tokens (`[[...]]`) | Treated as literal values; SMTP auth fails |
| File log path permissions | NuciLog fails silently or throws at first log write |

**Recommendation:** Operators should implement pre-start validation in deployment scripts or container entrypoints.

---

## Secret Management

**Required Secrets:**
- `SecuritySettings.ApiKey` — API key for endpoint authorisation
- `SmtpSettings.Password` — SMTP authentication password

**Recommended Practices:**
1. Use environment variables injected by orchestration platform (Kubernetes secrets, Docker secrets, Azure Key Vault, AWS Secrets Manager, etc.)
2. Never commit real credentials to source control
3. Rotate secrets periodically; application requires restart to pick up changes
4. Use distinct API keys per environment (dev, staging, prod)
5. Use dedicated SMTP credentials per environment

**Configuration Example (Kubernetes):**
```yaml
env:
  - name: SecuritySettings__ApiKey
    valueFrom:
      secretKeyRef:
        name: nucinotifications-secrets
        key: api-key
  - name: SmtpSettings__Password
    valueFrom:
      secretKeyRef:
        name: nucinotifications-secrets
        key: smtp-password
  - name: SmtpSettings__Host
    value: "mail.prod.example.com"
  - name: SmtpSettings__Username
    value: "notifier@prod.example.com"
```

---

## Network Ports

| Port | Protocol | Direction | Purpose | Configured By |
|------|----------|-----------|---------|---------------|
| Operator-defined | HTTP/HTTPS | Inbound | Kestrel endpoint for `POST /Email` | ASP.NET Core host config (`ASPNETCORE_URLS`, `Kestrel` config) |
| 587 (default) | SMTP/TLS | Outbound | Email submission to SMTP server | `SmtpSettings.Port` |

**Inbound:** No fixed port — configure via standard ASP.NET Core mechanisms:
- `ASPNETCORE_URLS=http://+:8080;https://+:8081`
- `Kestrel` configuration section in `appsettings.json`
- Reverse proxy (nginx, Traefik, etc.) terminating TLS

**Outbound:** Operator must ensure network connectivity from deployment environment to SMTP host:port.

---

## Reload Behaviour

**None.** Configuration is bound into singleton objects during `ConfigureServices`. Changes to configuration sources (files, environment variables, secrets) **require process restart** to take effect.

No `IOptionsMonitor`, `IOptionsSnapshot`, or `ConfigurationChangeToken` usage in the repository.

---

## Configuration Examples

### Minimal Production (Environment Variables)
```bash
export SecuritySettings__ApiKey="sk-prod-xyz789"
export SmtpSettings__Host="smtp.sendgrid.net"
export SmtpSettings__Port=587
export SmtpSettings__Username="apikey"
export SmtpSettings__Password="SG.actual-api-key"
export SmtpSettings__SenderName="Production Notifier"
export NuciLoggerSettings__LogFilePath="/var/log/nucinotifications.log"
```

### Development (User Secrets)
```bash
cd NuciNotifications.API
dotnet user-secrets set "SecuritySettings:ApiKey" "sk-dev-abc123"
dotnet user-secrets set "SmtpSettings:Host" "smtp.mailtrap.io"
dotnet user-secrets set "SmtpSettings:Port" "2525"
dotnet user-secrets set "SmtpSettings:Username" "dev-user"
dotnet user-secrets set "SmtpSettings:Password" "dev-password"
dotnet user-secrets set "NuciLoggerSettings:IsFileOutputEnabled" "false"
```

### Docker Compose
```yaml
services:
  nucinotifications:
    image: nucinotifications-api:latest
    environment:
      - SecuritySettings__ApiKey=${API_KEY}
      - SmtpSettings__Host=${SMTP_HOST}
      - SmtpSettings__Port=${SMTP_PORT}
      - SmtpSettings__Username=${SMTP_USERNAME}
      - SmtpSettings__Password=${SMTP_PASSWORD}
      - SmtpSettings__SenderName=Docker Notifier
      - SmtpSettings__MaximumAttempts=3
      - SmtpSettings__DelayBetweenAttemptsInSeconds=5
      - NuciLoggerSettings__LogFilePath=/app/logs/nucinotifications.log
      - NuciLoggerSettings__IsFileOutputEnabled=true
    volumes:
      - ./logs:/app/logs
    ports:
      - "8080:8080"
```

---

## Configuration Change Impact Matrix

| Setting Changed | Requires Restart | Affects In-Flight Requests | Notes |
|-----------------|------------------|---------------------------|-------|
| SecuritySettings.ApiKey | Yes | No (new requests use new key) | Singleton bound at startup |
| SmtpSettings.Host | Yes | No | SmtpClient constructed once |
| SmtpSettings.Port | Yes | No | SmtpClient constructed once |
| SmtpSettings.Username | Yes | No | Credentials captured at construction |
| SmtpSettings.Password | Yes | No | Credentials captured at construction |
| SmtpSettings.SenderName | Yes | No | Read per-request from singleton |
| SmtpSettings.MaximumAttempts | Yes | No | Read per-request from singleton |
| SmtpSettings.DelayBetweenAttemptsInSeconds | Yes | No | Read per-request from singleton |
| NuciLoggerSettings.LogFilePath | Yes | No | NuciLog configured at startup |
| NuciLoggerSettings.IsFileOutputEnabled | Yes | No | NuciLog configured at startup |