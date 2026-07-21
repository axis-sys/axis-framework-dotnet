# AxisEmail — Documentação

> 🌐 [English (README principal)](../../../en-us/3-Infra/AxisEmail/README.md)

**Uma porta de email de um método só** — `IAxisEmailService.SendAsync(AxisEmailData)` retornando um `AxisResult`. O adapter embarcado `AxisEmail.MimeKit` lida com SMTP (gmail, SES, Mailtrap, qualquer lugar com credenciais e uma porta). O código da aplicação nunca vê `MimeMessage` ou `SmtpClient`.

```csharp
public Task<AxisResult> NotifyOrderShippedAsync(Order order)
    => email.SendAsync(new AxisEmailData
    {
        To      = [(order.Customer.Name, order.Customer.Email)],
        Subject = $"Your order {order.OrderId} has shipped",
        Body    = $"Hi {order.Customer.Name}, your order is on its way.",
    });
```

Use esta página como **mapa**: leia o tronco abaixo (~5 min) e salte direto para o detalhe do grupo que você precisa — sem ler centenas de linhas.

---

## O tronco (leia primeiro)

### A interface em 60 segundos

```csharp
public interface IAxisEmailService
{
    Task<AxisResult> SendAsync(AxisEmailData data);
}
```

Um método. O payload (`AxisEmailData`) carrega destinatários (To + Cc), assunto, corpo e o subtipo MIME do corpo (`plain` ou `html`). Remetente, host SMTP e credenciais vivem em **`AxisEmailSettings`**, lido da configuração. → **[O contrato `IAxisEmailService`](iaxisemailservice.md)**

### O payload — `AxisEmailData`

```csharp
public record AxisEmailData
{
    public required IEnumerable<(string Name, string Email)> To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public string BodyTextType { get; init; } = "plain";          // ou "html"
    public IEnumerable<(string Name, string Email)> Cc { get; init; } = [];
}
```

→ **[Payload de email · `AxisEmailData`](axis-email-data.md)**

### Adapter embarcado — MimeKit

`AxisEmail.MimeKit` é a implementação MailKit / MimeKit. Host SMTP, porta, SSL, remetente e senha vêm da configuração. → **[Adapter `AxisEmail.MimeKit`](mimekit-adapter.md)**

```csharp
services.AddAxisMimeKitEmail(builder.Configuration);
```

### Instalação

```
dotnet add package AxisEmail            # a abstração
dotnet add package AxisEmail.MimeKit    # o adapter MimeKit/MailKit
```

→ Guia completo: **[Primeiros passos](getting-started.md)**

---

## O mapa (salte para o que precisa)

| Grupo | Você quer… | Detalhe |
|---|---|---|
| **Contrato · `IAxisEmailService`** ⭐ | enviar um email | [iaxisemailservice.md](iaxisemailservice.md) |
| **Payload · `AxisEmailData`** | a forma de um email | [axis-email-data.md](axis-email-data.md) |
| **Settings · `AxisEmailSettings`** | configurar SMTP e remetente | [settings.md](settings.md) |
| **MimeKit · `AxisEmail.MimeKit`** | o adapter MailKit embarcado | [mimekit-adapter.md](mimekit-adapter.md) |
| **Adapter custom** | escreva o seu (SendGrid, SES, Mailgun) | [custom-adapter.md](custom-adapter.md) |
| **Por quê?** | o argumento contra `SmtpClient` direto | [why-axisemail.md](why-axisemail.md) |
| **Referência** | cada membro num só lugar | [api-reference.md](api-reference.md) |

**Comece aqui:** [Primeiros passos](getting-started.md) · [O contrato `IAxisEmailService`](iaxisemailservice.md) · [Por que AxisEmail?](why-axisemail.md)

**Fundamentos:** [Payload de email](axis-email-data.md) · [Settings](settings.md) · [Adapter `AxisEmail.MimeKit`](mimekit-adapter.md)

**Referência e extras:** [Adapter custom](custom-adapter.md) · [Referência da API](api-reference.md)

---

## Princípios de design

1. **Um método, um payload.** Tudo que um email precisa vive em `AxisEmailData`. Adapters nunca inventam parâmetros extras.
2. **Credenciais vivem em `AxisEmailSettings`.** Código de aplicação nunca vê a senha. Configuração faz bind dos segredos uma vez.
3. **Erros são valores.** Falhas de SMTP, erros de auth, timeouts — todos colapsam em um único `AxisError.InternalServerError("ERROR_SENDING_EMAIL")` para que chamadores os tratem como qualquer outra falha de trilha.
4. **O adapter é substituível.** Plugue `AddSendGridEmail()` e a aplicação nem fica sabendo.
5. **HTML ou texto.** `BodyTextType` escolhe o subtipo MIME — `plain` por padrão, `html` para mails templados.

---

## Licença

Apache 2.0
