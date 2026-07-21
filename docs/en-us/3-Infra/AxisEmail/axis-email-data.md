# Email payload · `AxisEmailData`

> The record every email travels inside. Five fields: `To`, `Subject`, `Body`, `BodyTextType` (`plain` by default), and `Cc`. The sender is not on the payload — it lives in [`AxisEmailSettings`](settings.md).

```csharp
public record AxisEmailData
{
    public required IEnumerable<(string Name, string Email)> To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public string BodyTextType { get; init; } = "plain";
    public IEnumerable<(string Name, string Email)> Cc { get; init; } = [];
}
```

---

## The fields

| Field | Type | Required | Default | Meaning |
|---|---|:--:|---|---|
| `To` | `IEnumerable<(string Name, string Email)>` | yes | — | the primary recipients; the bundled adapter calls `message.To.Add(new MailboxAddress(name, email))` for each |
| `Subject` | `string` | yes | — | the subject line |
| `Body` | `string` | yes | — | the body, interpreted as `BodyTextType` |
| `BodyTextType` | `string` | no | `"plain"` | the MIME subtype passed to `new TextPart(...)` — typically `"plain"` or `"html"` |
| `Cc` | `IEnumerable<(string Name, string Email)>` | no | empty | carbon copy recipients |

> The record is immutable (`init` setters everywhere). Build it once at the call site; the adapter never mutates it.

---

## Picking `BodyTextType`

| Value | When |
|---|---|
| `"plain"` (default) | system messages, dev pings, alerts — anywhere ASCII text is enough |
| `"html"` | templated transactional emails (welcome, order confirmation, password reset) |

Any other MIME subtype `MimeKit` understands is also valid — but stick to `plain`/`html` unless you have a specific reason.

---

## Real-world examples

### 1. Plain-text alert

```csharp
await email.SendAsync(new AxisEmailData
{
    To      = [("ops", "ops@example.com")],
    Subject = "Daily report",
    Body    = $"Processed {count} orders today.",
});
```

### 2. HTML transactional email

```csharp
var body = $"""
    <h1>Welcome, {name}</h1>
    <p>Your trial ends on {trialEnd:yyyy-MM-dd}.</p>
    <p><a href="{billingUrl}">Add a card</a> to keep your account active.</p>
    """;

await email.SendAsync(new AxisEmailData
{
    To           = [(name, emailAddress)],
    Subject      = "Welcome to Axis",
    Body         = body,
    BodyTextType = "html",
});
```

### 3. CC the team on a customer-facing message

```csharp
await email.SendAsync(new AxisEmailData
{
    To      = [(customerName, customerEmail)],
    Cc      = [("Support", "support@example.com"), ("Sales", "sales@example.com")],
    Subject = "Welcome!",
    Body    = "We're glad to have you.",
});
```

**Why it pays off:** the payload reads exactly as a person describes the email — *to whom, with what subject, with what body, copying whom*. No `MailMessage` boilerplate.

---

## See also

- [The `IAxisEmailService` contract](iaxisemailservice.md) — the surface
- [Settings · `AxisEmailSettings`](settings.md) — the sender, not on the payload
- [`AxisEmail.MimeKit` adapter](mimekit-adapter.md) — how each field maps to `MimeMessage`

---

↩ [Back to AxisEmail docs](README.md)
