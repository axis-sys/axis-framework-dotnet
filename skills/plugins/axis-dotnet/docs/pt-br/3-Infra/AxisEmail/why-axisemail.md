# Por que AxisEmail? · comparação

> Há outras maneiras de enviar mail a partir de .NET. Esta página diz por que o AxisEmail é diferente — uma comparação direta, sem mão na cintura.

---

## vs. `System.Net.Mail.SmtpClient`

A classe legada está oficialmente obsoleta (`[ObsoleteAttribute]` em `SmtpClient.SendAsync` na doc moderna do .NET). A API lança, a configuração é embutida no tipo, e ela não entende TLS moderno como o MailKit entende. `AxisEmail` coloca um contrato tipado que retorna `AxisResult` na frente do MailKit / MimeKit — que é o que a própria Microsoft recomenda.

## vs. MailKit / MimeKit direto

O MailKit é excelente — e o adapter embarcado o usa. Chamá-lo de dentro de handlers acopla seu código a `SmtpClient`, `MimeMessage`, `SecureSocketOptions` e um wrapper try/catch em cada site. `AxisEmail` mantém esses tipos na borda e entrega ao handler um método com um payload.

## vs. SDKs de provedor (SendGrid, SES, Mailgun)

Se você precisa das features HTTP de um provedor, construa o adapter contra o SDK e esconda atrás de `IAxisEmailService`. Seus handlers não precisam saber a diferença; `AxisEmailData` é payload suficiente para qualquer mensagem transacional.

## vs. um `IEmailService` caseiro

DIY. Mesma forma do `IAxisEmailService`, mas você escreve o contrato, o adapter, os testes e o tratamento de falha por conta própria. `IAxisEmailService` poupa o custo — e herda a história de ferrovia do `AxisResult`.

---

## A comparação

| Característica | AxisEmail | `SmtpClient` direto | MailKit direto | SDK de provedor direto | Caseiro |
|---|:--:|:--:|:--:|:--:|:--:|
| Retorna `AxisResult` | **Sim** | Não | Não | Não | Talvez |
| Porta de um método só | **Sim** | n/a | Não | Não | Sim |
| HTML + texto via um único campo | **Sim** | Sim (`IsBodyHtml`) | Sim | Sim | Talvez |
| Remetente + SMTP em settings tipadas | **Sim** | Não | Não | Por provedor | Sim |
| Adapter MailKit embarcado | **Sim** | n/a | n/a | n/a | Não |
| Troca SMTP ↔ SendGrid sem mudar a aplicação | **Sim** | Não | Não | Não | Sim |
| Suporte a TLS moderno | **Sim** (via MailKit) | Não | Sim | Sim | Talvez |
| Zero deps NuGet na abstração | **Sim** | n/a | n/a | n/a | Sim |

---

## Veja também

- [O contrato `IAxisEmailService`](iaxisemailservice.md) — a superfície
- [Payload de email · `AxisEmailData`](axis-email-data.md) — os dados
- [Adapter `AxisEmail.MimeKit`](mimekit-adapter.md) — a implementação na caixa

---

↩ [Voltar à documentação do AxisEmail](README.md)
