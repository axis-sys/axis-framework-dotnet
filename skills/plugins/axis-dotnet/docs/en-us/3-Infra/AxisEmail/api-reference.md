# API reference

> The complete catalogue, grouped by responsibility. Use it for lookup — each group links back to its detail page.

---

## The contract — `IAxisEmailService`

| Method | Signature | Description |
|---|---|---|
| `SendAsync` | `Task<AxisResult> SendAsync(AxisEmailData data)` | deliver one email; `Ok()` on success, `Error(InternalServerError("ERROR_SENDING_EMAIL"))` on failure |

→ [The `IAxisEmailService` contract](iaxisemailservice.md)

---

## Payload — `AxisEmailData`

| Field | Type | Required | Default | Description |
|---|---|:--:|---|---|
| `To` | `IEnumerable<(string Name, string Email)>` | yes | — | primary recipients |
| `Subject` | `string` | yes | — | subject line |
| `Body` | `string` | yes | — | body content, interpreted as `BodyTextType` |
| `BodyTextType` | `string` | no | `"plain"` | MIME subtype (`"plain"` or `"html"`) |
| `Cc` | `IEnumerable<(string Name, string Email)>` | no | empty | carbon-copy recipients |

→ [Email payload · `AxisEmailData`](axis-email-data.md)

---

## Settings — `AxisEmailSettings`

| Path | Property | Description |
|---|---|---|
| `Sender.Address` | `string` | the From address |
| `Sender.Password` | `string` | the SMTP auth password |
| `Sender.Name` | `string` | the display name |
| `Smtp.Host` | `string` | SMTP server hostname |
| `Smtp.Port` | `int` | SMTP port (25 / 465 / 587) |
| `Smtp.SslEnabled` | `bool` | toggle `SecureSocketOptions.Auto` |

Bound from `Axis:Email:Settings` in `IConfiguration` by `AddAxisMimeKitEmail(IConfiguration)`.

→ [Settings · `AxisEmailSettings`](settings.md)

---

## MimeKit adapter — `AxisEmail.MimeKit`

| Member | Description |
|---|---|
| `AxisEmailService(IOptions<AxisEmailSettings>, IAxisLogger<AxisEmailService>)` | constructor; binds settings via `IOptions<>`, logs failures via `AxisLogger` |
| `EmailDependencyInjection.AddAxisMimeKitEmail(IConfiguration)` | DI extension; binds settings + registers `IAxisEmailService → AxisEmailService` (scoped) |

→ [`AxisEmail.MimeKit` adapter](mimekit-adapter.md)

---

## Behaviour contract (for adapters)

| Scenario | Returned `AxisResult` |
|---|---|
| message accepted | `Ok()` |
| auth / network / MIME / SMTP failure | `Error(InternalServerError("ERROR_SENDING_EMAIL"))` (and the exception logged) |

→ [Custom adapter](custom-adapter.md)

---

## See also

- [Getting started](getting-started.md) — install, configure, send
- [Why AxisEmail?](why-axisemail.md) — the case for the abstraction
- [Full documentation](README.md) — the map of the whole documentation

---

↩ [Back to AxisEmail docs](README.md)
