# AxisEmail — Documentation

> 🌐 [Português (documentação navegável)](../../../pt-br/3-Infra/AxisEmail/README.md)

**A one-method email port** — `IAxisEmailService.SendAsync(AxisEmailData)` returning an `AxisResult`. The bundled `AxisEmail.MimeKit` adapter handles SMTP (gmail, SES, Mailtrap, anywhere with credentials and a port). The application code never sees `MimeMessage` or `SmtpClient`.

```csharp
public Task<AxisResult> NotifyOrderShippedAsync(Order order)
    => email.SendAsync(new AxisEmailData
    {
        To      = [(order.Customer.Name, order.Customer.Email)],
        Subject = $"Your order {order.OrderId} has shipped",
        Body    = $"Hi {order.Customer.Name}, your order is on its way.",
    });
```

Use this page as a **map**: read the trunk below (~5 min) and jump straight to the detail of the group you need — without reading hundreds of lines.

---

## The trunk (read first)

### The interface in 60 seconds

```csharp
public interface IAxisEmailService
{
    Task<AxisResult> SendAsync(AxisEmailData data);
}
```

One method. The payload (`AxisEmailData`) carries recipients (To + Cc), subject, body and the body's MIME subtype (`plain` or `html`). Sender, SMTP host and credentials live in **`AxisEmailSettings`**, bound from configuration. → **[The `IAxisEmailService` contract](iaxisemailservice.md)**

### The payload — `AxisEmailData`

```csharp
public record AxisEmailData
{
    public required IEnumerable<(string Name, string Email)> To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public string BodyTextType { get; init; } = "plain";          // or "html"
    public IEnumerable<(string Name, string Email)> Cc { get; init; } = [];
}
```

→ **[Email payload · `AxisEmailData`](axis-email-data.md)**

### Bundled adapter — MimeKit

`AxisEmail.MimeKit` is the MailKit / MimeKit implementation. SMTP host, port, SSL, sender and password come from configuration. → **[`AxisEmail.MimeKit` adapter](mimekit-adapter.md)**

```csharp
services.AddAxisMimeKitEmail(builder.Configuration);
```

### Installation

```
dotnet add package AxisEmail            # the abstraction
dotnet add package AxisEmail.MimeKit    # the MimeKit/MailKit adapter
```

→ Full guide: **[Getting started](getting-started.md)**

---

## The map (jump to what you need)

| Group | You want to… | Detail |
|---|---|---|
| **Contract · `IAxisEmailService`** ⭐ | send one email | [iaxisemailservice.md](iaxisemailservice.md) |
| **Payload · `AxisEmailData`** | the shape of an email | [axis-email-data.md](axis-email-data.md) |
| **Settings · `AxisEmailSettings`** | configure SMTP and sender | [settings.md](settings.md) |
| **MimeKit · `AxisEmail.MimeKit`** | the bundled MailKit adapter | [mimekit-adapter.md](mimekit-adapter.md) |
| **Custom adapter** | write your own (SendGrid, SES, Mailgun) | [custom-adapter.md](custom-adapter.md) |
| **Why?** | the case against `SmtpClient` directly | [why-axisemail.md](why-axisemail.md) |
| **Reference** | every member at a glance | [api-reference.md](api-reference.md) |

**Start here:** [Getting started](getting-started.md) · [The `IAxisEmailService` contract](iaxisemailservice.md) · [Why AxisEmail?](why-axisemail.md)

**Fundamentals:** [Email payload](axis-email-data.md) · [Settings](settings.md) · [`AxisEmail.MimeKit` adapter](mimekit-adapter.md)

**Reference & extras:** [Custom adapter](custom-adapter.md) · [API reference](api-reference.md)

---

## Design principles

1. **One method, one payload.** Everything an email needs lives in `AxisEmailData`. Adapters never invent extra parameters.
2. **Credentials live in `AxisEmailSettings`.** Application code never sees the password. Configuration binds the secrets once.
3. **Errors are values.** SMTP failures, auth errors, timeouts — all collapse into a single `AxisError.InternalServerError("ERROR_SENDING_EMAIL")` so callers handle them like any other rail failure.
4. **The adapter is replaceable.** Drop in `AddSendGridEmail()` and the application is none the wiser.
5. **HTML or plain.** `BodyTextType` picks the MIME subtype — `plain` by default, `html` for templated mails.

---

## License

Apache 2.0
