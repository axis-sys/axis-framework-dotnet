# Primeiros passos · instalação e uso

> Instale a abstração e o adapter MimeKit, configure SMTP e envie seu primeiro email em cinco minutos.

---

## Instalação

```
dotnet add package AxisEmail            # a abstração
dotnet add package AxisEmail.MimeKit    # o adapter MimeKit/MailKit
```

`AxisEmail.MimeKit` traz `MailKit` (o cliente SMTP) e `MimeKit` (o builder MIME).

---

## Configurando SMTP

Adicione `Axis:Email:Settings` ao `appsettings.json`:

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

> Em produção, coloque a senha num secret store (User Secrets, Key Vault, env var) — o bind do `IConfiguration` pega da mesma forma.

---

## Registrando o adapter

```csharp
using AxisEmail.MimeKit;

builder.Services.AddAxisMimeKitEmail(builder.Configuration);
```

`AddAxisMimeKitEmail` faz bind de `AxisEmailSettings` a partir de `Axis:Email:Settings` e registra `IAxisEmailService → AxisEmailService` (scoped).

---

## Enviando

```csharp
public Task<AxisResult> NotifyOrderShippedAsync(Order order)
    => email.SendAsync(new AxisEmailData
    {
        To      = [(order.Customer.Name, order.Customer.Email)],
        Subject = $"Your order {order.OrderId} has shipped",
        Body    = $"Hi {order.Customer.Name}, your order is on its way.",
    });
```

Para corpos HTML:

```csharp
await email.SendAsync(new AxisEmailData
{
    To           = [("Jane", "jane@example.com")],
    Subject      = "Welcome",
    Body         = "<h1>Welcome, Jane!</h1>",
    BodyTextType = "html",
});
```

**Por que compensa:** o handler não toca em `SmtpClient`, `MimeMessage` ou nas opções SSL. Um método, um payload, um `AxisResult`.

---

## Veja também

- [O contrato `IAxisEmailService`](iaxisemailservice.md) — a superfície
- [Payload de email · `AxisEmailData`](axis-email-data.md) — cada campo
- [Settings · `AxisEmailSettings`](settings.md) — configuração de remetente e SMTP
- [Adapter `AxisEmail.MimeKit`](mimekit-adapter.md) — o que o adapter embarcado faz de fato
- [Adapter custom](custom-adapter.md) — plugue SendGrid, SES, Mailgun
- [Por que AxisEmail?](why-axisemail.md) — o argumento contra `SmtpClient` direto
- [Referência da API](api-reference.md) — cada membro num só lugar

---

↩ [Voltar à documentação do AxisEmail](README.md)
