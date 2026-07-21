# Adapter MimeKit · `AxisEmail.MimeKit`

> A implementação embarcada de `IAxisEmailService`. Constrói um `MimeMessage`, conecta via `MailKit.Net.Smtp.SmtpClient`, autentica com a senha do remetente e envia. Qualquer exceção vira um `InternalServerError` logado.

```csharp
services.AddAxisMimeKitEmail(builder.Configuration);   // faz bind de AxisEmailSettings + registra o service
```

---

## Quando usar

- Você tem credenciais SMTP (seu servidor, App Password do Gmail, SES SMTP, Mailtrap).
- Você não precisa de features específicas do provedor (templates, tracking, listas de supressão).
- Você quer uma única dependência pequena que só entrega email.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| usar a API HTTP de um provedor (SendGrid, Mailgun, Postmark) | um [adapter custom](custom-adapter.md) sobre o SDK do provedor |
| enviar via Microsoft Graph | um [adapter custom](custom-adapter.md) sobre `Microsoft.Graph` |
| tratar webhooks de entrega (bounces, complaints) | o webhook do provedor mais seu próprio subscriber |

---

## O que é registrado

`EmailDependencyInjection.AddAxisMimeKitEmail`:

```csharp
public static IServiceCollection AddAxisMimeKitEmail(this IServiceCollection services, IConfiguration configuration)
{
    services.Configure<AxisEmailSettings>(configuration.GetSection("Axis:Email:Settings"));
    services.AddScoped<IAxisEmailService, AxisEmailService>();
    return services;
}
```

- `AxisEmailSettings` é amarrado a partir de `Axis:Email:Settings` via `IOptions<>`.
- `IAxisEmailService` é registrado como **scoped**.

A implementação depende de `IOptions<AxisEmailSettings>` e `IAxisLogger<AxisEmailService>` (para que o caminho de falha seja logado com os enrichers do framework).

---

## Como o `SendAsync` mapeia para MimeKit / MailKit

Lendo `AxisEmailService.SendAsync` direto:

| Passo | Código | Notas |
|---|---|---|
| Construir a mensagem | `new MimeMessage(); message.From.Add(new MailboxAddress(Sender.Name, Sender.Address));` | o remetente vem de `AxisEmailSettings.Sender` |
| Assunto e corpo | `message.Subject = data.Subject; message.Body = new TextPart(data.BodyTextType) { Text = data.Body };` | `BodyTextType` escolhe o subtipo MIME |
| To / Cc | itera `data.To` / `data.Cc`, chama `message.To.Add(new MailboxAddress(name, email))` | as tuplas vêm direto do payload |
| Conectar | `client.ConnectAsync(Smtp.Host, Smtp.Port, sslEnabled ? Auto : None)` | `SslEnabled` alterna `SecureSocketOptions` |
| Autenticar | `client.AuthenticateAsync(Sender.Address, Sender.Password)` | a senha do remetente é a senha SMTP |
| Enviar | `client.SendAsync(message); client.DisconnectAsync(true);` | encerra a sessão limpa |
| Qualquer falha | captura `Exception` → `logger.LogError(ex, "ERROR_SENDING_EMAIL")` → `AxisError.InternalServerError("ERROR_SENDING_EMAIL")` | a exceção original vive nos logs |

---

## Exemplo real — Gmail

Gmail exige uma **App Password** (senhas comuns são bloqueadas).

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

// depois
await email.SendAsync(new AxisEmailData
{
    To = [("test", "test@example.com")],
    Subject = "Hello",
    Body = "World",
});
```

**Por que compensa:** zero SDKs de provedor para aprender. O adapter tem ~30 linhas de MailKit; seus handlers só veem `IAxisEmailService`.

---

## Veja também

- [O contrato `IAxisEmailService`](iaxisemailservice.md) — a superfície
- [Payload de email · `AxisEmailData`](axis-email-data.md) — cada campo
- [Settings · `AxisEmailSettings`](settings.md) — o que o adapter lê da config
- [Adapter custom](custom-adapter.md) — para SendGrid / SES / Mailgun

---

↩ [Voltar à documentação do AxisEmail](README.md)
