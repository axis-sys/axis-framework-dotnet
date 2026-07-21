# Contrato · `IAxisEmailService`

> A porta inteira é um método só. O payload (`AxisEmailData`) carrega cada destinatário, cada header, o assunto e o corpo. O resultado é um `AxisResult` — `Ok()` no sucesso, um erro tipado em qualquer falha de SMTP / rede / autenticação.

```csharp
public interface IAxisEmailService
{
    Task<AxisResult> SendAsync(AxisEmailData data);
}
```

---

## Quando usar

- Notificações transacionais: confirmações de pedido, reset de senha, alertas, login com magic-link.
- Notificações internas: resumos noturnos, alertas de erro, pings para admins.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| enviar mail **em massa** (marketing) | um ESP dedicado (SendGrid, Mailchimp) — lidam com bounce/click/reputação |
| **chat / mensagem instantânea** | um transporte diferente (Slack webhook, Teams API) |
| receber mail entrante | o bus + um handler de webhook inbound |

---

## O que `SendAsync` retorna

Lendo o adapter embarcado direto:

| Desfecho | `AxisResult` retornado |
|---|---|
| todo destinatário aceito pelo servidor SMTP | `Ok()` |
| falha de auth, falha de rede, rejeição SMTP, erro de MIME | `Error(AxisError.InternalServerError("ERROR_SENDING_EMAIL"))` (e a exceção é logada via `AxisLogger`) |

> O adapter **não** distingue falhas "permanentes" de "transientes" no contrato — o `AxisErrorType.InternalServerError` tipado cobre tudo. Use um adapter custom se precisar separar códigos SMTP 4xx vs. 5xx.

---

## Exemplos reais

### 1. Notificar via event handler

```csharp
public sealed record OrderShippedEvent(AxisEntityId OrderId, string CustomerName, string CustomerEmail) : IAxisEvent;

public class SendShippedEmailHandler(IAxisEmailService email) : IAxisEventHandler<OrderShippedEvent>
{
    public Task<AxisResult> HandleAsync(OrderShippedEvent @event)
        => email.SendAsync(new AxisEmailData
        {
            To      = [(@event.CustomerName, @event.CustomerEmail)],
            Subject = $"Your order {@event.OrderId} has shipped",
            Body    = $"Hi {@event.CustomerName}, your package is on its way.",
        });
}
```

**Por que compensa:** o command handler que enviou o pedido não tem ideia de que um email está saindo. O fan-out do bus pluga o side effect; o handler retorna um `AxisResult` para que a chamada de publish também veja a falha.

### 2. Magic-link de login

```csharp
public Task<AxisResult> SendMagicLinkAsync(string name, string emailAddress, string url)
    => email.SendAsync(new AxisEmailData
    {
        To           = [(name, emailAddress)],
        Subject      = "Your sign-in link",
        Body         = $"<p>Hi {name}, <a href=\"{url}\">click here to sign in</a> (valid for 15 minutes).</p>",
        BodyTextType = "html",
    });
```

**Por que compensa:** o corpo HTML usa `BodyTextType = "html"` — o mesmo método, o mesmo tipo de retorno. Nenhuma mágica de `MailMessage.IsBodyHtml = true` no call site.

### 3. Recuperando de uma falha transiente

```csharp
return await email.SendAsync(data)
    .OrElseAsync(_ => deadLetter.EnqueueAsync(data));   // dead-letter para retry
```

**Por que compensa:** `SendAsync` retorna o `Task<AxisResult>` não-genérico, então o fallback passa por `OrElseAsync` — um envio falho vira um valor na trilha e o ramo de recuperação roteia para uma fila de dead-letter (que retorna seu próprio `Task<AxisResult>`) para retry posterior, sem que uma exceção jamais alcance o chamador. O contrato só produz `AxisErrorType.InternalServerError` (veja acima), logo recuperar em qualquer falha é exatamente o caminho da dead-letter.

---

## Veja também

- [Payload de email · `AxisEmailData`](axis-email-data.md) — o que colocar dentro
- [Settings · `AxisEmailSettings`](settings.md) — remetente + SMTP
- [Adapter `AxisEmail.MimeKit`](mimekit-adapter.md) — a implementação na caixa
- [Adapter custom](custom-adapter.md) — escreva um para SendGrid / SES / Mailgun

---

↩ [Voltar à documentação do AxisEmail](README.md)
