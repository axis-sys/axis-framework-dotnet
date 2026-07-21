# MimeKit adapter · `AxisEmail.MimeKit`

> The bundled `IAxisEmailService` implementation. Builds a `MimeMessage`, connects via `MailKit.Net.Smtp.SmtpClient`, authenticates with the sender's password and sends. Any exception becomes a logged `InternalServerError`.

```csharp
services.AddAxisMimeKitEmail(builder.Configuration);   // binds AxisEmailSettings + registers the service
```

---

## When to use

- You have SMTP credentials (your own server, Gmail App Password, SES SMTP, Mailtrap).
- You do not need provider-specific features (templates, tracking, suppression lists).
- You want a single small dependency that just delivers mail.

## When *not* to use

| You want to… | Use instead |
|---|---|
| use a provider's HTTP API (SendGrid, Mailgun, Postmark) | a [custom adapter](custom-adapter.md) over the provider SDK |
| send via Microsoft Graph | a [custom adapter](custom-adapter.md) over `Microsoft.Graph` |
| handle delivery webhooks (bounces, complaints) | the provider's webhook plus your own subscriber |

---

## What gets registered

`EmailDependencyInjection.AddAxisMimeKitEmail`:

```csharp
public static IServiceCollection AddAxisMimeKitEmail(this IServiceCollection services, IConfiguration configuration)
{
    services.Configure<AxisEmailSettings>(configuration.GetSection("Axis:Email:Settings"));
    services.AddScoped<IAxisEmailService, AxisEmailService>();
    return services;
}
```

- `AxisEmailSettings` is bound from `Axis:Email:Settings` via `IOptions<>`.
- `IAxisEmailService` is registered as **scoped**.

The implementation depends on `IOptions<AxisEmailSettings>` and `IAxisLogger<AxisEmailService>` (so the failure path is logged with the framework's enrichers).

---

## How `SendAsync` maps to MimeKit / MailKit

Reading `AxisEmailService.SendAsync` directly:

| Step | Code | Notes |
|---|---|---|
| Build the message | `new MimeMessage(); message.From.Add(new MailboxAddress(Sender.Name, Sender.Address));` | sender comes from `AxisEmailSettings.Sender` |
| Subject and body | `message.Subject = data.Subject; message.Body = new TextPart(data.BodyTextType) { Text = data.Body };` | `BodyTextType` chooses the MIME subtype |
| To / Cc | iterates `data.To` / `data.Cc`, calls `message.To.Add(new MailboxAddress(name, email))` | tuples come straight from the payload |
| Connect | `client.ConnectAsync(Smtp.Host, Smtp.Port, sslEnabled ? Auto : None)` | `SslEnabled` toggles `SecureSocketOptions` |
| Authenticate | `client.AuthenticateAsync(Sender.Address, Sender.Password)` | the sender's password is the SMTP password |
| Send | `client.SendAsync(message); client.DisconnectAsync(true);` | quits cleanly |
| Any failure | catch `Exception` → `logger.LogError(ex, "ERROR_SENDING_EMAIL")` → `AxisError.InternalServerError("ERROR_SENDING_EMAIL")` | the original exception lives in the logs |

---

## Real-world example — Gmail

Gmail requires an **App Password** (regular passwords are blocked).

```json
{
  "Axis": {
    "Email": {
      "Settings": {
        "Sender": {
          "Name":     "Axis Sample",
          "Address":  "you@gmail.com",
          "Password": "abcd efgh ijkl mnop"
        },
        "Smtp": {
          "Host":       "smtp.gmail.com",
          "Port":       587,
          "SslEnabled": true
        }
      }
    }
  }
}
```

```csharp
builder.Services.AddAxisMimeKitEmail(builder.Configuration);

// later
await email.SendAsync(new AxisEmailData
{
    To = [("test", "test@example.com")],
    Subject = "Hello",
    Body = "World",
});
```

**Why it pays off:** zero provider SDKs to learn. The adapter is ~30 lines of MailKit; your handlers see only `IAxisEmailService`.

---

## See also

- [The `IAxisEmailService` contract](iaxisemailservice.md) — the surface
- [Email payload · `AxisEmailData`](axis-email-data.md) — every field
- [Settings · `AxisEmailSettings`](settings.md) — what the adapter reads from config
- [Custom adapter](custom-adapter.md) — for SendGrid / SES / Mailgun

---

↩ [Back to AxisEmail docs](README.md)
