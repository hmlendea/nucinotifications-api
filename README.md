[![Donate](https://img.shields.io/badge/-%E2%99%A5%20Donate-%23ff69b4)](https://hmlendea.go.ro/fund.html) [![Latest GitHub release](https://img.shields.io/github/v/release/hmlendea/nucinotifications-api)](https://github.com/hmlendea/nucinotifications-api/releases/latest) [![Build Status](https://github.com/hmlendea/nucinotifications-api/actions/workflows/dotnet.yml/badge.svg)](https://github.com/hmlendea/nucinotifications-api/actions/workflows/dotnet.yml)

# About

Small ASP.NET Core HTTP API that sends plain-text emails via SMTP.

It’s meant to be deployed as a tiny “notifications gateway” that other apps can call, instead of giving each app direct SMTP credentials.

## Features

- Single endpoint: `POST /Email`
- SMTP delivery with TLS (`EnableSsl = true`)
- Retry on SMTP timeouts (configurable max attempts + delay)
- Request authorization via API key
- Optional HMAC request signing support (via `NuciSecurity.HMAC`)
- Logging via `NuciLog`

## Requirements

- .NET SDK 10
- Access to an SMTP server (host/port/username/password)

## Configuration

Configuration is read from `appsettings.json` and can be overridden using standard ASP.NET Core configuration providers (environment variables, mounted secrets, etc.).

### `SecuritySettings`

- `ApiKey` - API key used to authorize calls to the API

### `SmtpSettings`

- `Host` - SMTP host
- `Port` - SMTP port (default: 587)
- `Username` - SMTP username (also used as the email sender)
- `Password` - SMTP password
- `SenderName` - Default sender name, if not specified in the request (default: Notifier)
- `MaximumAttempts` - how many times to retry when an SMTP timeout occurs (default: 3)
- `DelayBetweenAttemptsInSeconds` - delay between timeout retries (default: 5)

Example `appsettings.json`:

```json
{
	"securitySettings": {
		"apiKey": "53d77dc5-8b49-49fa-97ac-2505c01ce435"
	},
	"smtpSettings": {
		"host": "[[SMTP_HOST]]",
		"port": 587,
		"username": "[[SMTP_USERNAME]]",
		"password": "[[SMTP_PASSWORD]]",
        "senderName": "Notifier",
		"maximumAttempts": 3,
		"delayBetweenAttemptsInSeconds": 5
	}
}
```

Example environment variable overrides:

- `SecuritySettings__ApiKey`
- `SmtpSettings__Host`
- `SmtpSettings__Port`
- `SmtpSettings__Username`
- `SmtpSettings__Password`

## Run locally

```bash
dotnet restore
dotnet run
```

Kestrel will listen on the default ASP.NET Core URLs for your environment.

## API

### `POST /Email`

Sends a plain-text email.

Request body:

```json
{
    "sender": "Sender User",
	"recipient": "user@example.com",
	"subject": "Hello",
	"body": "Test message"
}
```

Notes:

- `sender` is optional. If it's not specified, the default one from the configuration (see `smtpSettings.senderName`) will be used instead.
- `recipient`, `subject` and `body` are required.
- Authorisation is enforced via an API key (see `securitySettings.apiKey`). The exact way the key (and optional HMAC token) is passed is defined by the `NuciAPI.Controllers` authorization implementation used by this project.

## License

GPL-3.0-only. See `LICENSE`.