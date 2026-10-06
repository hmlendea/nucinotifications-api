# Privacy and Personal Data

This document describes how NuciNotifications API at https://github.com/hmlendea/nucinotifications-api handles personal data. It covers the application behaviour and verified integrations described below. Where the software is self-hosted, the instance operator may have separate responsibilities described below.

**Information reviewed:** 2026-10-06

## 📑 Table of Contents

- What This Document Covers
- Self-Hosted Deployments
- Data We Handle
- Processing and Use
- Storage, Retention, and Deletion
- External Processing and Integrations
- User Controls and Requests
- International Transfers
- Children
- Data Protection and Security
- Document Changes
- Contact

## 🔎 What This Document Covers

This document describes how NuciNotifications API at https://github.com/hmlendea/nucinotifications-api handles personal data. It covers the application behaviour and verified integrations described below. Where the software is self-hosted, the instance operator may have separate responsibilities described below.

## 🏠 Self-Hosted Deployments

NuciNotifications API is distributed as a self-contained ASP.NET Core application that operators deploy and operate on their own infrastructure. This document covers the data-handling behaviour of the application code as released by the project maintainers. Instance operators control their instance's configuration, local storage, logs, backups, access controls, retention, and request handling unless the project directly controls those functions.

The application sends no data from a self-hosted instance to project maintainers or external services beyond the operator-configured SMTP server. There is no telemetry, update checks, crash reports, or monitoring integrations that transmit data to the project maintainers. Operators configure the SMTP server, logging destination, and all credentials through standard .NET configuration providers.

## 📥 Data We Handle

### Data Provided to the Application

- API key for request authorisation (configured by operator)
- SMTP credentials: host, port, username, password, sender display name (configured by operator)
- Email submission request fields: optional sender display name, required recipient email address, required subject, required plain-text body (provided by calling applications per request)

### Data Generated or Collected by the Application

- Structured delivery operation records: configured sender address, sender display name, recipient address, subject, attempt number, outcome (success, timeout retry, or other failure), and timestamps (emitted to configured NuciLog destination)
- HTTP request logs from NuciAPI middleware (operator-configured destination)

### Data Received from Integrations

- No personal data is received from integrations or third parties beyond the operator-configured SMTP server, which receives the submitted email for onward delivery.

## 🧭 Processing and Use

The application processes the data described above for these verified functions:
- API-key authorisation of inbound HTTP requests — API key
- SMTP authentication and TLS-enabled message submission — SMTP credentials
- Construction and submission of plain-text email — sender display name, recipient, subject, body
- Structured logging of delivery attempts and outcomes — sender address, sender display name, recipient, subject, attempt, outcome

## 🗄️ Storage, Retention, and Deletion

| Data category | Storage location | Retention and deletion |
|---------------|------------------|------------------------|
| API key and SMTP credentials | Configuration provider and singleton process memory | Process lifetime; provider retention is operator-defined |
| Email request fields (sender, recipient, subject, body) | Request memory and the configured SMTP server | Request lifetime within this service; SMTP-provider retention is external |
| Delivery operation metadata (sender address, sender display name, recipient, subject, outcome) | Configured NuciLog destination; `logfile.log` by default | Operator-defined; no rotation policy is present in the application |

For self-hosted deployments, the instance operator controls storage, deletion, and backups for all local data. The project does not operate a centralised service and therefore does not control operator data.

## 🔗 External Processing and Integrations

| Service or integration | Purpose | Data involved | Configuration or documentation |
|-----------------------|---------|---------------|--------------------------------|
| Operator-configured SMTP server | Relays submitted plain-text email for final delivery | Sender address, sender display name, recipient, subject, body, SMTP credentials | `SmtpSettings` in `appsettings.json` or environment variables; see README.md Configuration section |
| NuciLog (via NuciAPI middleware and EmailService) | Emits structured request and delivery operation records | Sender address, sender display name, recipient, subject, attempt, outcome, timestamps | `NuciLoggerSettings` in `appsettings.json` or environment variables; see README.md Configuration section |
| NuciAPI middleware packages | Provides API-key authorisation, scanner protection, replay protection, request logging, exception handling | API key, HTTP request metadata | Pinned package versions in `NuciNotifications.API.csproj`; see README.md Dependencies section |

The application has no built-in external data transfer beyond the operator-configured SMTP server and logging destination.

## ⚙️ User Controls and Requests

The following documented controls or request procedures are available:
- Operators configure all data flows through `appsettings.json`, environment variables, or secret providers; see README.md Configuration section
- Operators can disable file logging by setting `NuciLoggerSettings__IsFileOutputEnabled` to `false`
- Operators control SMTP server selection, credentials, and network routing
- Calling applications control the email content submitted per request
- No in-application data-subject request endpoint exists; operators handle requests according to their policies

## 🌍 International Transfers

Data may be transferred to the country or region where the operator-configured SMTP server resides. The deployment location and SMTP server location are controlled by the instance operator. The application itself performs no cross-border transfers independent of operator configuration.

## 🧒 Children

The service is not directed to children and imposes no age restrictions. No application controls for child accounts or data exist.

## 🛡️ Data Protection and Security

Verified technical and operational safeguards:
- API-key authorisation required for every `POST /Email` request
- Optional HMAC request signing via NuciSecurity.HMAC (operator-enabled)
- Scanner and replay protection middleware active by default
- HTTPS redirection configured for inbound traffic
- SMTP submission uses TLS with credential-based authentication (200-second timeout)
- Delivery logs exclude message bodies and credentials
- Configuration uses standard .NET providers; operators must inject production secrets through protected providers (environment variables, mounted secrets, etc.)
- No database, queue, or durable delivery store exists in the application

For self-hosted deployments, the operator is responsible for:
- Applying updates to the application and underlying OS
- Protecting secrets (API key, SMTP password) in configuration providers
- Configuring access controls, network exposure, and reverse-proxy settings
- Securing log files and backups
- Validating TLS certificates for inbound HTTPS and outbound SMTP

No absolute security is promised.

## 🔄 Document Changes

Update this document when application data flows, storage, integrations, or deployment responsibilities change. The current version is published at https://github.com/hmlendea/nucinotifications-api/blob/master/PRIVACY.md.

## 📬 Contact

For questions about application data handling, contact the project maintainers through GitHub Security Advisories (https://github.com/hmlendea/nucinotifications-api/security/advisories) or the repository's private communication channels. For a self-hosted instance, contact the instance operator, unless the project explicitly handles the request. Include the deployment identifier or configuration details if available; do not send passwords, access tokens, or other secrets.