# Getting started · installation and usage

> Install the abstraction and the MimeKit adapter, configure SMTP, and send your first email in five minutes.

---

## Installation

```
dotnet add package AxisEmail            # the abstraction
dotnet add package AxisEmail.MimeKit    # the MimeKit/MailKit adapter
```

`AxisEmail.MimeKit` brings `MailKit` (the SMTP client) and `MimeKit` (the MIME builder).

---

## Configuring SMTP

Add `Axis:Email:Settings` to `appsettings.json`:

```json
{
  "Axis": {
    "Email": {
      "Settings": {
        "Sender": {
          "Name":     "Axis Sample",
          "Address":  "no-reply@example.com",
          "Password": "…"
        },
        "Smtp": {
          "Host":       "smtp.example.com",
          "Port":       587,
          "SslEnabled": true
        }
      }
    }
  }
}
```

> Put the password in a secret store in production (User Secrets, Key Vault, env var) — the `IConfiguration` binding picks it up the same way.

---

## Registering the adapter

```csharp
using AxisEmail.MimeKit;

builder.Services.AddAxisMimeKitEmail(builder.Configuration);
```

`AddAxisMimeKitEmail` binds `AxisEmailSettings` from `Axis:Email:Settings` and registers `IAxisEmailService → AxisEmailService` (scoped).

---

## Sending

```csharp
public Task<AxisResult> NotifyOrderShippedAsync(Order order)
    => email.SendAsync(new AxisEmailData
    {
        To      = [(order.Customer.Name, order.Customer.Email)],
        Subject = $"Your order {order.OrderId} has shipped",
        Body    = $"Hi {order.Customer.Name}, your order is on its way.",
    });
```

For HTML bodies:

```csharp
await email.SendAsync(new AxisEmailData
{
    To           = [("Jane", "jane@example.com")],
    Subject      = "Welcome",
    Body         = "<h1>Welcome, Jane!</h1>",
    BodyTextType = "html",
});
```

**Why it pays off:** the handler does not touch `SmtpClient`, `MimeMessage` or the SSL options. One method, one payload, one `AxisResult`.

---

## See also

- [The `IAxisEmailService` contract](iaxisemailservice.md) — the surface
- [Email payload · `AxisEmailData`](axis-email-data.md) — every field
- [Settings · `AxisEmailSettings`](settings.md) — sender and SMTP configuration
- [`AxisEmail.MimeKit` adapter](mimekit-adapter.md) — what the bundled adapter actually does
- [Custom adapter](custom-adapter.md) — plug in SendGrid, SES, Mailgun
- [Why AxisEmail?](why-axisemail.md) — the case against `SmtpClient` directly
- [API reference](api-reference.md) — every member in one place

---

↩ [Back to AxisEmail docs](README.md)
