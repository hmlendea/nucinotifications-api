# Deployment and Operations Guide

This document covers deployment, operational procedures, and runtime considerations for the NuciNotifications API.

---

## Deployment Model

**Artifact:** Self-contained, single-file executable per platform/architecture
**Runtime:** .NET 10.0 (no separate runtime installation required)
**Process Model:** Single ASP.NET Core process (Kestrel)
**State:** In-memory only (configuration, logger, SMTP client); optional local log file

---

## Build and Publish

### Prerequisites
- .NET 10.0 SDK
- Git

### Build from Source
```bash
git clone https://github.com/hmlendea/nucinotifications-api.git
cd nucinotifications-api
dotnet restore
dotnet build --no-restore -c Release
```

### Publish Self-Contained Executables
```bash
# Linux x64
dotnet publish NuciNotifications.API/NuciNotifications.API.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/linux-x64

# Linux ARM64
dotnet publish NuciNotifications.API/NuciNotifications.API.csproj -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true -o ./publish/linux-arm64

# macOS x64
dotnet publish NuciNotifications.API/NuciNotifications.API.csproj -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/osx-x64

# macOS ARM64
dotnet publish NuciNotifications.API/NuciNotifications.API.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o ./publish/osx-arm64

# Windows x64
dotnet publish NuciNotifications.API/NuciNotifications.API.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/win-x64

# Windows ARM64
dotnet publish NuciNotifications.API/NuciNotifications.API.csproj -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=true -o ./publish/win-arm64
```

### Output Structure
```
publish/{rid}/
├── NuciNotifications.API          # Executable (no extension on Unix)
├── NuciNotifications.API.exe      # Windows executable
├── appsettings.json               # Copied from project (PreserveNewest)
└── *.pdb                          # Debug symbols (if not trimmed)
```

---

## Release Process

The repository includes a release script that delegates to an external deployment script:

```bash
bash ./release.sh 1.2.3
```

**What release.sh does:**
1. Downloads `https://raw.githubusercontent.com/hmlendea/deployment-scripts/master/release/dotnet/10.0.sh`
2. Pipes it directly to `bash` with version argument
3. The external script handles packaging, signing, GitHub Release creation, etc.

**⚠️ Security Note:** The script downloads and executes remote code. Review the external script before running in production environments.

**Release Artifacts:** Published to GitHub Releases as platform-specific archives.

---

## Runtime Requirements

| Requirement | Details |
|-------------|---------|
| OS | Linux (x64, ARM64), macOS (x64, ARM64), Windows (x64, ARM64) |
| Network (Inbound) | HTTP/HTTPS port for Kestrel (operator-configured) |
| Network (Outbound) | TCP to SMTP host:port (default 587) with TLS |
| File System | Write access to log file directory (if file logging enabled) |
| Secrets | API key, SMTP password via environment variables or secret provider |
| Time | Accurate system clock (for TLS, logging timestamps) |

---

## Configuration for Deployment

### Required Settings (Must Be Provided)
```bash
# API key for endpoint authorisation
SecuritySettings__ApiKey=<strong-random-key>

# SMTP connection
SmtpSettings__Host=smtp.example.com
SmtpSettings__Port=587
SmtpSettings__Username=notifier@example.com
SmtpSettings__Password=<smtp-password>
```

### Optional Settings (Have Defaults)
```bash
SmtpSettings__SenderName="Production Notifier"
SmtpSettings__MaximumAttempts=3
SmtpSettings__DelayBetweenAttemptsInSeconds=5
NuciLoggerSettings__LogFilePath=/var/log/nucinotifications.log
NuciLoggerSettings__IsFileOutputEnabled=true
```

### Inbound Port Configuration
Configure Kestrel via standard ASP.NET Core mechanisms:

**Environment Variable:**
```bash
ASPNETCORE_URLS="http://+:8080;https://+:8081"
```

**appsettings.json (Kestrel section):**
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://+:8080" },
      "Https": { "Url": "https://+:8081" }
    }
  }
}
```

**Reverse Proxy (Recommended for Production):**
```
Client → [TLS Termination] → Reverse Proxy (nginx/Traefik/Caddy) → Kestrel (HTTP only)
```

---

## Running the Service

### Direct Execution
```bash
# Linux/macOS
./NuciNotifications.API

# Windows
.\NuciNotifications.API.exe
```

### As systemd Service (Linux)
```ini
# /etc/systemd/system/nucinotifications.service
[Unit]
Description=NuciNotifications API
After=network.target

[Service]
Type=notify
ExecStart=/opt/nucinotifications/NuciNotifications.API
WorkingDirectory=/opt/nucinotifications
Environment=SecuritySettings__ApiKey=sk-prod-xyz
Environment=SmtpSettings__Host=smtp.example.com
Environment=SmtpSettings__Port=587
Environment=SmtpSettings__Username=notifier@example.com
Environment=SmtpSettings__Password=secret
Environment=NuciLoggerSettings__LogFilePath=/var/log/nucinotifications.log
Environment=ASPNETCORE_URLS=http://localhost:8080
Restart=on-failure
RestartSec=5
StandardOutput=journal
StandardError=journal
SyslogIdentifier=nucinotifications

[Install]
WantedBy=multi-user.target
```

```bash
systemctl daemon-reload
systemctl enable --now nucinotifications
```

### As Docker Container
```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["NuciNotifications.API/NuciNotifications.API.csproj", "NuciNotifications.API/"]
RUN dotnet restore "NuciNotifications.API/NuciNotifications.API.csproj"
COPY . .
WORKDIR "/src/NuciNotifications.API"
RUN dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["./NuciNotifications.API"]
```

```bash
docker build -t nucinotifications-api:latest .
docker run -d \
  -e SecuritySettings__ApiKey=sk-prod-xyz \
  -e SmtpSettings__Host=smtp.example.com \
  -e SmtpSettings__Port=587 \
  -e SmtpSettings__Username=notifier@example.com \
  -e SmtpSettings__Password=secret \
  -e NuciLoggerSettings__LogFilePath=/app/logs/nucinotifications.log \
  -v nucinotifications-logs:/app/logs \
  -p 8080:8080 \
  nucinotifications-api:latest
```

---

## Operational Procedures

### Health Checks
**No built-in health endpoint.** Implement external monitoring:
- HTTP probe to `POST /Email` with valid API key (expects 200/400/401)
- Process liveness via systemd/Docker/k8s
- Log file existence and freshness

### Log Management
**Default:** `logfile.log` in process working directory
**Format:** Structured NuciLog records (JSON-like)
**Rotation:** **Not implemented** — operator must configure logrotate or equivalent

**Example logrotate config:**
```conf
/var/log/nucinotifications.log {
    daily
    rotate 30
    compress
    delaycompress
    missingok
    notifempty
    create 640 nucinotifications nucinotifications
    sharedscripts
    postrotate
        systemctl reload nucinotifications > /dev/null 2>&1 || true
    endscript
}
```

### Secret Rotation
1. Update secret in secret provider (Vault, Kubernetes secret, etc.)
2. Restart service to pick up new values (configuration is snapshot at startup)
3. No graceful key rotation — in-flight requests use old key until restart

### SMTP Credential Rotation
Same as secret rotation — requires restart.

### Configuration Changes
All configuration changes require **process restart**. No hot reload.

---

## Monitoring and Observability

### Key Metrics to Monitor (External)
| Metric | Source | Alert Threshold |
|--------|--------|-----------------|
| Process uptime | systemd/Docker/k8s | < 99.9% |
| HTTP 5xx rate | Reverse proxy logs | > 1% |
| Request latency (p99) | Reverse proxy logs | > 5s |
| Log file growth | File system | > 1GB/day |
| Disk space | File system | < 10% free |
| SMTP connectivity | Synthetic test | Failure |

### Log Analysis
**Structured fields available:**
- `Operation: SendEmail`
- `Status: Started | Success | Failure`
- `SenderAddress, SenderName, Recipient, Subject`
- `Attempt` (on timeout warnings)
- `Exception` (on errors)

**Example queries:**
```bash
# Count successful deliveries
grep '"Status":"Success"' /var/log/nucinotifications.log | wc -l

# Count timeout retries
grep '"Status":"Failure"' /var/log/nucinotifications.log | grep '"Attempt"' | wc -l

# Find non-timeout errors
grep '"Status":"Failure"' /var/log/nucinotifications.log | grep -v '"Attempt"' | head -20
```

---

## Troubleshooting

### Common Issues

| Symptom | Likely Cause | Resolution |
|---------|--------------|------------|
| All requests return 401 | Invalid/missing `SecuritySettings__ApiKey` | Verify env var matches caller's key |
| Requests hang, then 5xx | SMTP timeout, retries exhausted | Check SMTP host:port reachability, credentials, TLS |
| Requests return 5xx immediately | SMTP auth failure, connection refused | Verify SMTP credentials, host, port, firewall |
| No logs written | `NuciLoggerSettings__IsFileOutputEnabled=false` or permissions | Enable file logging, check directory permissions |
| Duplicate emails received | SMTP timeout but server accepted message | Reduce `MaximumAttempts`, increase `DelayBetweenAttemptsInSeconds`, implement idempotency at caller |
| High latency | SMTP server slow, retries | Monitor SMTP latency, adjust timeouts |
| Process crashes on startup | Missing required config, invalid values | Check logs for binding errors, validate all required settings |

### Debugging Steps
1. **Check process logs:** `journalctl -u nucinotifications -f` or `docker logs -f <container>`
2. **Verify configuration:** `cat /proc/<pid>/environ | tr '\0' '\n' | grep -E 'SecuritySettings|SmtpSettings'`
3. **Test SMTP connectivity:** `telnet smtp.example.com 587` or `openssl s_client -connect smtp.example.com:587 -starttls smtp`
4. **Test endpoint locally:** `curl -X POST http://localhost:8080/Email -H "Authorization: Bearer <key>" -H "Content-Type: application/json" -d '{"recipient":"test@example.com","subject":"test","body":"test"}'`
5. **Check log file:** `tail -f /var/log/nucinotifications.log`

---

## Scaling Considerations

### Horizontal Scaling
- **Stateless** — Multiple replicas can run independently
- **No shared state** — Each replica has own SMTP client, log file
- **Load balancer** — Distribute requests across replicas
- **Caveat:** Each replica logs locally; aggregate logs externally

### Vertical Scaling
- **Single-threaded SMTP** — One `SmtpClient` per process limits throughput
- **Synchronous requests** — Each request blocks a thread during SMTP + retries
- **Thread pool** — Kestrel thread pool handles concurrent requests, but SMTP is bottleneck

### Throughput Estimation
```
Single request time ≈ SMTP latency + (retries × (SMTP timeout + delay))
With defaults: ~200s timeout + 5s delay × 3 retries = up to ~615s worst case
Typical: ~100-500ms per successful delivery
Max concurrent deliveries ≈ thread pool size (but shared SmtpClient serialises)
```

**Recommendation:** For high throughput, deploy multiple replicas behind load balancer.

---

## Backup and Recovery

### What to Back Up
- **Configuration/Secrets** — In secret provider (Vault, etc.), not application
- **Log Files** — If retention required, back up log directory
- **No application state** — No database, queue, or delivery ledger

### Recovery Procedure
1. Provision new instance
2. Inject configuration/secrets
3. Start service
4. No data recovery needed (no durable state)

---

## Security Operations

### TLS Certificates
- **Inbound:** Terminate at reverse proxy or configure Kestrel with cert
- **Outbound:** SMTP `EnableSsl=true` validates server certificate by default
- **Certificate validation failures** → `SmtpException` (non-timeout, not retried)

### Network Security
- Restrict inbound to trusted sources (API callers)
- Restrict outbound to SMTP server only
- Use private networks/VPCs where possible

### Audit Trail
- Delivery logs contain recipient, subject, sender — treat as sensitive
- API key in logs only if request logging captures headers (NuciAPI middleware)
- Rotate logs per retention policy

---

## Upgrade Procedure

1. **Build new version** or download release artifact
2. **Stop service** gracefully (`systemctl stop nucinotifications`)
3. **Replace executable** and `appsettings.json` (if schema changed)
4. **Verify configuration** compatibility
5. **Start service** (`systemctl start nucinotifications`)
6. **Verify health** via test request
7. **Monitor logs** for errors

**Rollback:** Keep previous executable; swap back and restart.

---

## Known Operational Limitations

| Limitation | Impact | Mitigation |
|------------|--------|------------|
| No health endpoint | Cannot use standard liveness/readiness probes | Implement synthetic HTTP probe |
| No log rotation | Disk exhaustion risk | Configure logrotate externally |
| No config reload | Restart required for any change | Plan maintenance windows |
| Shared SmtpClient | Concurrency issues under load | Limit replicas, monitor for errors |
| No request timeout | Hung SMTP blocks thread indefinitely | Configure Kestrel request timeout, reverse proxy timeout |
| No graceful shutdown | In-flight requests terminated | Accept brief disruption during deploy |
| External release script | Supply chain risk | Audit script, pin version, or implement local release |

---

## Support Contacts

- **Repository:** https://github.com/hmlendea/nucinotifications-api
- **Issues:** https://github.com/hmlendea/nucinotifications-api/issues
- **Security:** https://github.com/hmlendea/nucinotifications-api/security/advisories
- **Funding:** https://hmlendea.go.ro/funding