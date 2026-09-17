# Security Policy

This policy covers security vulnerabilities in the NuciNotifications API source and its latest self-contained .NET 10 release. Reports should be submitted privately through GitHub Security Advisories or directly to the maintainers so that issues can be validated and remediated through coordinated disclosure.

## 📑 Table of Contents

- [Supported Versions](#supported-versions)
- [Reporting a Vulnerability](#reporting-a-vulnerability)
- [Scope](#scope)
- [Disclosure Policy](#disclosure-policy)

## 🛡️ Supported Versions

Use this table to indicate which project versions currently receive security maintenance.

| Version | Distribution Channel | Supported |
|---------|--------------------|-----------|
| Latest version | GitHub Releases | ✅ |
| Latest version | GitHub source repository (`master` branch) | ✅ |
| Latest version | Unofficial package managers | ❌ |
| Latest version | Modified or repackaged archives | ❌ |
| Latest version | Unofficial third-party distribution channels | ❌ |
| Preceding versions | Any distribution channel | ❌ |

## 🚨 Reporting a Vulnerability

Please do not disclose suspected vulnerabilities publicly before maintainers have had an opportunity to validate and remediate them.

To report a vulnerability:
- [GitHub Security Advisories](https://github.com/hmlendea/nucinotifications-api/security/advisories)
- Contact the maintainers directly through the repository's private communication channels

Include the affected version or commit, a description of the impact, reproduction steps, and any relevant logs or proof of concept. Please redact API keys, SMTP credentials, personal data, and message content before submission.

## 📌 Scope

The subsequent report categories are in scope for this repository:
- Authentication and authorisation bypasses affecting the HTTP API or HMAC request protection
- Exposure or mishandling of API keys, SMTP credentials, email data, or sensitive operational logs

The subsequent categories are out of scope unless explicitly stated to the contrary:
- Vulnerabilities in unofficial packages, modified archives, or third-party infrastructure
- Denial-of-service testing, social engineering, spam, and reports concerning unsupported preceding versions

## 📢 Disclosure Policy

This project follows coordinated disclosure:
1. Vulnerabilities are investigated privately.
2. A remediation plan is prepared and validated.
3. Public disclosure is published after a fix, mitigation, or agreed risk decision is available.
4. Credit is attributed in accordance with reporter preference and project policy.
