# Why AxisEmail? · comparison

> There are other ways to send mail from .NET. This page tells you why AxisEmail is different — a direct comparison, no hand-waving.

---

## vs. `System.Net.Mail.SmtpClient`

The legacy class is officially obsolete (`[ObsoleteAttribute]` on `SmtpClient.SendAsync` in modern .NET docs). Its API throws, its configuration is baked into the type, and it does not understand modern TLS the way MailKit does. `AxisEmail` puts a typed `AxisResult`-returning contract in front of MailKit / MimeKit — which is what Microsoft itself recommends.

## vs. MailKit / MimeKit directly

MailKit is excellent — and the bundled adapter uses it. Calling it from handlers couples your code to `SmtpClient`, `MimeMessage`, `SecureSocketOptions` and a try/catch wrapper at every site. `AxisEmail` keeps those types at the boundary and hands the handler one method with one payload.

## vs. provider SDKs (SendGrid, SES, Mailgun)

If you need a provider's HTTP features, build the adapter against the SDK and hide it behind `IAxisEmailService`. Your handlers do not need to know the difference; `AxisEmailData` is enough payload for any transactional message.

## vs. a bespoke `IEmailService`

DIY. Same shape as `IAxisEmailService`, but you write the contract, the adapter, the tests and the failure handling yourself. `IAxisEmailService` saves the cost — and inherits the railway story from `AxisResult`.

---

## The comparison

| Feature | AxisEmail | `SmtpClient` direct | MailKit direct | Provider SDK direct | Bespoke |
|---|:--:|:--:|:--:|:--:|:--:|
| Returns `AxisResult` | **Yes** | No | No | No | Maybe |
| One-method port | **Yes** | n/a | No | No | Yes |
| HTML + plain via one field | **Yes** | Yes (`IsBodyHtml`) | Yes | Yes | Maybe |
| Sender + SMTP in typed settings | **Yes** | No | No | Per provider | Yes |
| Bundled MailKit adapter | **Yes** | n/a | n/a | n/a | No |
| Swap SMTP ↔ SendGrid without app changes | **Yes** | No | No | No | Yes |
| Modern TLS support | **Yes** (via MailKit) | No | Yes | Yes | Maybe |
| Zero NuGet deps in the abstraction | **Yes** | n/a | n/a | n/a | Yes |

---

## See also

- [The `IAxisEmailService` contract](iaxisemailservice.md) — the surface
- [Email payload · `AxisEmailData`](axis-email-data.md) — the data
- [`AxisEmail.MimeKit` adapter](mimekit-adapter.md) — the in-box implementation

---

↩ [Back to AxisEmail docs](README.md)
