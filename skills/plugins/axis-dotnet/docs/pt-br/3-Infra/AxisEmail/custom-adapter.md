# Adapter custom · escreva seu próprio `IAxisEmailService`

> Troque o adapter SMTP por SendGrid, SES, Mailgun, Postmark — ou um test double que captura cada envio. Implemente um método, registre sua classe como `IAxisEmailService`.

```csharp
public class SendGridEmailAdapter(ISendGridClient client, AxisEmailSettings settings) : IAxisEmailService
{
    public async Task<AxisResult> SendAsync(AxisEmailData data)
    {
        var message = new SendGridMessage
        {
            From = new(settings.Sender.Address, settings.Sender.Name),
            Subject = data.Subject,
            PlainTextContent = data.BodyTextType == "plain" ? data.Body : null,
            HtmlContent      = data.BodyTextType == "html"  ? data.Body : null,
        };

        foreach (var (name, email) in data.To) message.AddTo(new EmailAddress(email, name));
        foreach (var (name, email) in data.Cc) message.AddCc(new EmailAddress(email, name));

        var response = await client.SendEmailAsync(message);
        return response.IsSuccessStatusCode
            ? AxisResult.Ok()
            : AxisError.InternalServerError("ERROR_SENDING_EMAIL");
    }
}
```

---

## Quando usar

- A API HTTP de um provedor (SendGrid, SES, Mailgun, Postmark, Resend) te dá tracking de entrega, renderização de template no lado do provedor, gestão de supressão.
- Um **test double** para que testes unitários não batam na rede.
- Um adapter **dual-write** que grava cada envio numa tabela de auditoria antes de entregar.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| ficar no SMTP | o [adapter `AxisEmail.MimeKit`](mimekit-adapter.md) da caixa |
| expor features específicas do provedor (categorias, custom args) | estenda o contrato com uma nova interface e exija que o *adapter* implemente os dois |

---

## O contrato que você precisa honrar

| Comportamento | Obrigatório | Razão |
|---|---|---|
| Retorne `Task<AxisResult>`, nunca lance cooperativamente | sim | a ferrovia depende disso |
| `Ok()` quando a mensagem foi aceita pelo provedor | sim | chamadores encadeiam no sucesso |
| `Error(AxisError.InternalServerError("ERROR_SENDING_EMAIL"))` em auth / rede / rejeição | recomendado | espelha o código embarcado para o tratamento do chamador ser idêntico |
| Honre o `BodyTextType` (`"plain"`/`"html"`) | sim | o payload prometeu |
| Leia remetente / credenciais de `AxisEmailSettings` (ou do seu próprio objeto de settings registrado do mesmo jeito) | recomendado | configuração fica num só lugar |
| Logue falhas via `AxisLogger` | recomendado | enrichers anexam correlation / tenant |

---

## Exemplo real — um test double que grava

```csharp
public class CapturingEmailService : IAxisEmailService
{
    public List<AxisEmailData> Sent { get; } = [];

    public Task<AxisResult> SendAsync(AxisEmailData data)
    {
        Sent.Add(data);
        return Task.FromResult(AxisResult.Ok());
    }
}

// num teste
services.AddSingleton<IAxisEmailService, CapturingEmailService>();
// depois
Assert.Single(emailService.Sent);
Assert.Equal("Welcome", emailService.Sent[0].Subject);
```

**Por que compensa:** testes de integração asseguram sobre o payload *exato* que seu código produziu — assunto, corpo, destinatários — sem subir um servidor SMTP ou mockar o MailKit.

---

## Veja também

- [O contrato `IAxisEmailService`](iaxisemailservice.md) — o que você precisa satisfazer
- [Adapter `AxisEmail.MimeKit`](mimekit-adapter.md) — a referência da caixa
- [Settings · `AxisEmailSettings`](settings.md) — o formato da configuração

---

↩ [Voltar à documentação do AxisEmail](README.md)
