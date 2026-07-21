# Settings · `AxisEmailSettings`

> Identidade do remetente e configuração SMTP num record tipado. Lido de `Axis:Email:Settings` em `IConfiguration` e resolvido via `IOptions<AxisEmailSettings>` dentro do adapter embarcado.

```csharp
public record AxisEmailSettings
{
    public SenderData Sender { get; init; } = new();
    public SmtpData   Smtp   { get; init; } = new();

    public class SenderData
    {
        public string Address  { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string Name     { get; init; } = string.Empty;
    }

    public class SmtpData
    {
        public string Host       { get; init; } = string.Empty;
        public int    Port       { get; init; }
        public bool   SslEnabled { get; init; }
    }
}
```

---

## Quando usar

`AxisEmailSettings` é carregado uma vez no startup por `AddAxisMimeKitEmail(IConfiguration)`. Você quase nunca o constrói à mão — a configuração é a fonte da verdade.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| trocar de host SMTP por requisição | um adapter diferente ou dois registros |
| guardar a senha no código | um secret store (User Secrets, Key Vault, env var) |

---

## Os campos

### `Sender`

| Campo | Descrição |
|---|---|
| `Address` | o From — o que os destinatários veem na caixa de entrada |
| `Password` | a senha de auth SMTP — guarde num secret store, nunca em controle de versão |
| `Name` | o display name exibido junto a `Address` (ex.: `"Axis Sample"`) |

### `Smtp`

| Campo | Descrição |
|---|---|
| `Host` | o hostname do servidor SMTP |
| `Port` | a porta SMTP (comumente 25, 465 ou 587) |
| `SslEnabled` | `true` → o adapter embarcado usa `SecureSocketOptions.Auto`; `false` → `SecureSocketOptions.None` |

---

## Formato da configuração

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

`AddAxisMimeKitEmail(builder.Configuration)` faz bind da seção a `IOptions<AxisEmailSettings>`. O adapter captura o valor uma única vez, no construtor — uma mudança de configuração só é percebida pela próxima instância Scoped (o próximo escopo de requisição), não por um `SendAsync` já em andamento.

---

## Exemplo real — overrides por env-var em produção

```json
// appsettings.Production.json — mantém segredos fora
{
  "Axis": {
    "Email": {
      "Settings": {
        "Sender": { "Name": "Axis", "Address": "no-reply@axis.example" },
        "Smtp":   { "Host": "smtp.gmail.com", "Port": 587, "SslEnabled": true }
      }
    }
  }
}
```

```
# environment
Axis__Email__Settings__Sender__Password=<o segredo>
```

**Por que compensa:** o segredo fica no environment / key vault. O app lê via `IConfiguration` igual a qualquer outro bind. Rotacionar a senha é mudança de config de deploy, não de código.

---

## Veja também

- [Primeiros passos](getting-started.md) — instale e faça bind num único passo
- [Adapter `AxisEmail.MimeKit`](mimekit-adapter.md) — como o adapter consome estas settings
- [Payload de email · `AxisEmailData`](axis-email-data.md) — o que *não* está nas settings

---

↩ [Voltar à documentação do AxisEmail](README.md)
