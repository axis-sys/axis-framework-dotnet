# Settings · `AxisEmailSettings`

> Sender identity and SMTP configuration in one typed record. Bound from `Axis:Email:Settings` in `IConfiguration` and resolved via `IOptions<AxisEmailSettings>` inside the bundled adapter.

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

## When to use

`AxisEmailSettings` is bound once at startup by `AddAxisMimeKitEmail(IConfiguration)`. You almost never construct it by hand — configuration is the source of truth.

## When *not* to use

| You want to… | Use instead |
|---|---|
| change SMTP host per request | a different adapter or two registrations |
| store the password in code | a secret store (User Secrets, Key Vault, env var) |

---

## The fields

### `Sender`

| Field | Description |
|---|---|
| `Address` | the From address — what recipients see in their inbox |
| `Password` | the SMTP auth password — keep it in a secret store, never in source control |
| `Name` | the display name shown alongside `Address` (e.g. `"Axis Sample"`) |

### `Smtp`

| Field | Description |
|---|---|
| `Host` | the SMTP server hostname |
| `Port` | the SMTP port (commonly 25, 465 or 587) |
| `SslEnabled` | `true` → the bundled adapter uses `SecureSocketOptions.Auto`; `false` → `SecureSocketOptions.None` |

---

## Configuration shape

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

`AddAxisMimeKitEmail(builder.Configuration)` binds the section to `IOptions<AxisEmailSettings>`. The adapter captures the value once, in its constructor — a config change is only picked up by the next Scoped instance (the next request scope), not by an in-flight `SendAsync`.

---

## Real-world example — env-var overrides in production

```json
// appsettings.Production.json — keep secrets out
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
Axis__Email__Settings__Sender__Password=<the secret>
```

**Why it pays off:** the secret stays in the environment / key vault. The app reads it through `IConfiguration` exactly like every other binding. Rotating the password is a deployment-config change, not a code change.

---

## See also

- [Getting started](getting-started.md) — install and bind in one step
- [`AxisEmail.MimeKit` adapter](mimekit-adapter.md) — how the adapter consumes these settings
- [Email payload · `AxisEmailData`](axis-email-data.md) — what *isn't* in the settings

---

↩ [Back to AxisEmail docs](README.md)
