# API Reference

## Endpoint

### POST /Email

Submits a plain-text email for delivery via the configured SMTP server.

- **URL:** `/Email`
- **Method:** `POST`
- **Content-Type:** `application/json`
- **Auth Required:** Yes (API key or HMAC)

### Request Headers

- `Authorization: Bearer <api-key>` (required unless using HMAC)
- `X-HMAC: <url-encoded-hmac-token>` (optional, for HMAC-authenticated requests)

### Request Body

```
{
  "sender": "string (optional, display name)",
  "recipient": "string (required, email address)",
  "subject": "string (required)",
  "body": "string (required)"
}
```

#### Field Details
- `sender`: Display name for the sender. If omitted/null/empty/whitespace, defaults to `smtpSettings.senderName`.
- `recipient`: Email address of the recipient. **Required.**
- `subject`: Email subject. **Required.**
- `body`: Email body. **Required.**

### HMAC Authentication

- **Header:** `X-HMAC: <url-encoded-hmac-token>`
- **Secret Key:** `securitySettings.apiKey` (same as API key)
- **Fields Used:**
  - `sender` (HmacOrder: 1)
  - `recipient` (HmacOrder: 2)
  - `subject` (HmacOrder: 5)
  - `body` (HmacOrder: 6)
- **Order:** Fields are concatenated in HmacOrder (1, 2, 5, 6) for HMAC computation.
- **Algorithm:** HMAC-SHA256 (as defined by NuciSecurity.HMAC package)
- **How to Compute:**
  1. Concatenate the values of the fields in HmacOrder (missing/empty fields are treated as empty strings).
  2. Compute HMAC-SHA256 using the API key as the secret.
  3. URL-encode the resulting token and set as `X-HMAC` header.
- **Validation:** The server validates the HMAC if present. If both `Authorization` and `X-HMAC` are present, both must be valid.

### API Key Authentication

- **Header:** `Authorization: Bearer <api-key>`
- **Source:** `securitySettings.apiKey` in configuration
- **Validation:** Case-insensitive `Bearer` prefix is stripped, value is compared to configured API key.
- **Required:** Yes, unless HMAC is used and valid.

### Example Request

```
POST /Email HTTP/1.1
Host: example.com
Authorization: Bearer abc123
Content-Type: application/json

{
  "sender": "Solaire of Astora",
  "recipient": "vasile.ciupitu@gmail.com",
  "subject": "Praise the Sun!",
  "body": "Would you like to join me on a jolly co-op adventure?"
}
```

### Example HMAC Calculation (Pseudo-Python)

```python
import hmac, hashlib, urllib.parse

fields = [sender, recipient, subject, body]
key = api_key.encode()
msg = ''.join(fields).encode()
token = hmac.new(key, msg, hashlib.sha256).digest()
url_encoded = urllib.parse.quote_plus(token)
```

### Responses

- **200 OK**: Email accepted for delivery (SMTP submission attempted)
- **400 Bad Request**: Invalid/missing fields, malformed JSON
- **401 Unauthorized**: Missing/invalid API key or HMAC
- **429 Too Many Requests**: Replay/scanner protection triggered (from NuciAPI middleware)
- **500 Internal Server Error**: Unhandled exception, SMTP failure, or delivery error

#### Success Response (NuciAPI Package-Defined)
The exact success response shape is defined by the `NuciAPI` package. Typically:
```
{
  "success": true,
  "data": null
}
```

#### Error Response (NuciAPI Package-Defined)
The exact error response shape is defined by the `NuciAPI` package. Typically:
```
{
  "success": false,
  "error": "Validation failed: recipient is required."
}
```

#### Common Error Scenarios
| Status | Trigger | Notes |
|--------|---------|-------|
| 400 | Missing `recipient`, `subject`, or `body` | `[ApiController]` model validation |
| 400 | Malformed JSON | ASP.NET Core model binding |
| 401 | Missing `Authorization` header | NuciAPI authorisation middleware |
| 401 | Invalid API key | NuciAPI authorisation middleware |
| 401 | Invalid HMAC signature | NuciSecurity.HMAC validation |
| 429 | Replay protection triggered | NuciAPI replay middleware |
| 429 | Scanner protection triggered | NuciAPI scanner middleware |
| 500 | SMTP timeout (after retries exhausted) | Wrapped as `TimeoutException` |
| 500 | SMTP auth failure, rejected, etc. | Original `SmtpException` rethrown |
| 500 | Unhandled exception | NuciAPI exception middleware |

### Notes
- All authentication and HMAC validation is performed by NuciAPI/NuciSecurity packages.
- Replay and scanner protection may reject requests with 429 or 401.
- The API does not persist emails or delivery state; success means SMTP submission was attempted.
- All configuration (API key, SMTP, sender name) is operator-supplied and not exposed in responses.
- The exact HTTP response shapes (success/error) are owned by the `NuciAPI` and `NuciAPI.Controllers` packages and may evolve with package versions.
