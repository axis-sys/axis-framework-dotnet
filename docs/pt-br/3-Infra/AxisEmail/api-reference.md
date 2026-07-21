# Referência da API

> O catálogo completo, agrupado por responsabilidade. Use para consulta — cada grupo linka de volta à sua página de detalhe.

---

## O contrato — `IAxisEmailService`

| Método | Assinatura | Descrição |
|---|---|---|
| `SendAsync` | `Task<AxisResult> SendAsync(AxisEmailData data)` | entrega um email; `Ok()` no sucesso, `Error(InternalServerError("ERROR_SENDING_EMAIL"))` na falha |

→ [O contrato `IAxisEmailService`](iaxisemailservice.md)

---

## Payload — `AxisEmailData`

| Campo | Tipo | Obrigatório | Padrão | Descrição |
|---|---|:--:|---|---|
| `To` | `IEnumerable<(string Name, string Email)>` | sim | — | destinatários primários |
| `Subject` | `string` | sim | — | linha de assunto |
| `Body` | `string` | sim | — | conteúdo do corpo, interpretado conforme `BodyTextType` |
| `BodyTextType` | `string` | não | `"plain"` | subtipo MIME (`"plain"` ou `"html"`) |
| `Cc` | `IEnumerable<(string Name, string Email)>` | não | vazio | destinatários em cópia |

→ [Payload de email · `AxisEmailData`](axis-email-data.md)

---

## Settings — `AxisEmailSettings`

| Caminho | Propriedade | Descrição |
|---|---|---|
| `Sender.Address` | `string` | o From |
| `Sender.Password` | `string` | a senha de auth SMTP |
| `Sender.Name` | `string` | o display name |
| `Smtp.Host` | `string` | hostname do servidor SMTP |
| `Smtp.Port` | `int` | porta SMTP (25 / 465 / 587) |
| `Smtp.SslEnabled` | `bool` | alterna `SecureSocketOptions.Auto` |

Amarrado a partir de `Axis:Email:Settings` em `IConfiguration` por `AddAxisMimeKitEmail(IConfiguration)`.

→ [Settings · `AxisEmailSettings`](settings.md)

---

## Adapter MimeKit — `AxisEmail.MimeKit`

| Membro | Descrição |
|---|---|
| `AxisEmailService(IOptions<AxisEmailSettings>, IAxisLogger<AxisEmailService>)` | construtor; amarra settings via `IOptions<>`, loga falhas via `AxisLogger` |
| `EmailDependencyInjection.AddAxisMimeKitEmail(IConfiguration)` | extensão DI; amarra settings + registra `IAxisEmailService → AxisEmailService` (scoped) |

→ [Adapter `AxisEmail.MimeKit`](mimekit-adapter.md)

---

## Contrato de comportamento (para adapters)

| Cenário | `AxisResult` retornado |
|---|---|
| mensagem aceita | `Ok()` |
| falha de auth / rede / MIME / SMTP | `Error(InternalServerError("ERROR_SENDING_EMAIL"))` (e a exceção logada) |

→ [Adapter custom](custom-adapter.md)

---

## Veja também

- [Primeiros passos](getting-started.md) — instale, configure, envie
- [Por que AxisEmail?](why-axisemail.md) — o argumento pela abstração
- [Documentação completa](README.md) — o mapa de toda a documentação

---

↩ [Voltar à documentação do AxisEmail](README.md)
