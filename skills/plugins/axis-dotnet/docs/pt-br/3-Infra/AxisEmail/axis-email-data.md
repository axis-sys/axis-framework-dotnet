# Payload de email · `AxisEmailData`

> O record onde todo email viaja. Cinco campos: `To`, `Subject`, `Body`, `BodyTextType` (`plain` por padrão) e `Cc`. O remetente não fica no payload — ele vive em [`AxisEmailSettings`](settings.md).

```csharp
public record AxisEmailData
{
    public required IEnumerable<(string Name, string Email)> To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public string BodyTextType { get; init; } = "plain";
    public IEnumerable<(string Name, string Email)> Cc { get; init; } = [];
}
```

---

## Os campos

| Campo | Tipo | Obrigatório | Padrão | Significado |
|---|---|:--:|---|---|
| `To` | `IEnumerable<(string Name, string Email)>` | sim | — | os destinatários primários; o adapter embarcado chama `message.To.Add(new MailboxAddress(name, email))` para cada |
| `Subject` | `string` | sim | — | a linha de assunto |
| `Body` | `string` | sim | — | o corpo, interpretado conforme `BodyTextType` |
| `BodyTextType` | `string` | não | `"plain"` | o subtipo MIME passado para `new TextPart(...)` — tipicamente `"plain"` ou `"html"` |
| `Cc` | `IEnumerable<(string Name, string Email)>` | não | vazio | destinatários em cópia |

> O record é imutável (setters `init` em toda parte). Construa uma vez no call site; o adapter nunca muta.

---

## Escolhendo o `BodyTextType`

| Valor | Quando |
|---|---|
| `"plain"` (padrão) | mensagens de sistema, pings de dev, alertas — qualquer lugar onde texto ASCII basta |
| `"html"` | emails transacionais templados (boas-vindas, confirmação de pedido, reset de senha) |

Qualquer outro subtipo MIME que o `MimeKit` entenda também é válido — mas fique em `plain`/`html` salvo razão específica.

---

## Exemplos reais

### 1. Alerta em texto puro

```csharp
await email.SendAsync(new AxisEmailData
{
    To      = [("ops", "ops@example.com")],
    Subject = "Daily report",
    Body    = $"Processed {count} orders today.",
});
```

### 2. Email transacional HTML

```csharp
var body = $"""
    <h1>Welcome, {name}</h1>
    <p>Your trial ends on {trialEnd:yyyy-MM-dd}.</p>
    <p><a href="{billingUrl}">Add a card</a> to keep your account active.</p>
    """;

await email.SendAsync(new AxisEmailData
{
    To           = [(name, emailAddress)],
    Subject      = "Welcome to Axis",
    Body         = body,
    BodyTextType = "html",
});
```

### 3. CC no time de uma mensagem para o cliente

```csharp
await email.SendAsync(new AxisEmailData
{
    To      = [(customerName, customerEmail)],
    Cc      = [("Support", "support@example.com"), ("Sales", "sales@example.com")],
    Subject = "Welcome!",
    Body    = "We're glad to have you.",
});
```

**Por que compensa:** o payload lê exatamente como uma pessoa descreve o email — *para quem, com qual assunto, com qual corpo, copiando quem*. Sem boilerplate de `MailMessage`.

---

## Veja também

- [O contrato `IAxisEmailService`](iaxisemailservice.md) — a superfície
- [Settings · `AxisEmailSettings`](settings.md) — o remetente, não no payload
- [Adapter `AxisEmail.MimeKit`](mimekit-adapter.md) — como cada campo mapeia para `MimeMessage`

---

↩ [Voltar à documentação do AxisEmail](README.md)
