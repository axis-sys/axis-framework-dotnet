---
name: axis-email
description: >
  Send transactional and internal email on Axis through the one-method port `IAxisEmailService.SendAsync`,
  which takes an `AxisEmailData` payload and returns an `AxisResult` — never throwing. Use when a handler
  must send mail (order confirmation, password reset, alert, magic link), when configuring SMTP via
  `AxisEmailSettings`, when wiring the bundled `AxisEmail.MimeKit` adapter, or when writing a custom adapter
  over SendGrid/SES/Mailgun or a test double. This skill is a MAP: each row points to the canonical rule in
  `rules/` — open only the one the context asks for. It does NOT restate invariants nor carry code. It does
  NOT cover the return monad (→ axis-result), the ambient context/dispatch (→ axis-mediator), the event that
  usually triggers a send (→ axis-bus), nor the swappable-infra-port pattern in the abstract
  (→ axis-dotnet-architect).
---

# AxisEmail — rule map (one-method transactional email port)

**Email** is a driven port with a single method: a caller hands `IAxisEmailService.SendAsync` one
`AxisEmailData` (recipients, subject, body, body MIME subtype, Cc) and gets back an `AxisResult` — `Ok()` when
the message is accepted, a typed `AxisError` on any SMTP / network / auth / MIME failure. The sender identity
and SMTP credentials are not on the payload; they live in `AxisEmailSettings`, bound from configuration. The
bundled `AxisEmail.MimeKit` adapter (MailKit + MimeKit) is one implementation; the transport is a swap. The
package is 3-infra.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/framework/3-infra/axis-email/`; open **only** the rule the context
requires.

## Rule map

### Start here — route by intent ⭐

| Context / what you were about to write | Rule |
|---|---|
| Is email the right tool here? (transactional/internal vs bulk/inbound/chat) | [email-transactional-scope](../../rules/framework/3-infra/axis-email/email-transactional-scope.yaml) |
| The surface itself — one `SendAsync(AxisEmailData)`, nothing else | [email-port-single-method](../../rules/framework/3-infra/axis-email/email-port-single-method.yaml) |

### The payload — `AxisEmailData`

| Context | Rule |
|---|---|
| Required fields — `To`, `Subject`, `Body` (recipients are `(Name, Email)` tuples) | [email-data-required-fields](../../rules/framework/3-infra/axis-email/email-data-required-fields.yaml) |
| Plain vs HTML — the single `BodyTextType` field (default `plain`) | [email-data-body-text-type](../../rules/framework/3-infra/axis-email/email-data-body-text-type.yaml) |
| Carbon copy — `Cc` is optional, defaults to empty | [email-data-cc-optional](../../rules/framework/3-infra/axis-email/email-data-cc-optional.yaml) |
| The payload is an immutable record, built once, never mutated | [email-data-immutable-record](../../rules/framework/3-infra/axis-email/email-data-immutable-record.yaml) |
| Gotcha — the sender is NOT on the payload; no `From`, `Bcc` or attachments | [email-data-sender-not-on-payload](../../rules/framework/3-infra/axis-email/email-data-sender-not-on-payload.yaml) |

### Settings — `AxisEmailSettings`

| Context | Rule |
|---|---|
| The record shape — `Sender` + `Smtp`, empty defaults | [email-settings-shape](../../rules/framework/3-infra/axis-email/email-settings-shape.yaml) |
| Binding — from `Axis:Email:Settings` via `IOptions<>` | [email-settings-binding-section](../../rules/framework/3-infra/axis-email/email-settings-binding-section.yaml) |
| `Sender.Address` doubles as From + SMTP username; keep `Password` in a secret store | [email-settings-sender-credentials](../../rules/framework/3-infra/axis-email/email-settings-sender-credentials.yaml) |
| Gotcha — settings are bound but NOT validated; blanks surface as a runtime send failure | [email-settings-no-validation](../../rules/framework/3-infra/axis-email/email-settings-no-validation.yaml) |

### Result & cancellation (the behaviour contract)

| Context | Rule |
|---|---|
| `SendAsync` returns `AxisResult`, never throws — compose with `ThenAsync`/`OrElseAsync` | [email-send-returns-result-never-throws](../../rules/framework/3-infra/axis-email/email-send-returns-result-never-throws.yaml) |
| No `CancellationToken` parameter — cancellation is ambient (and the in-box adapter does not thread it) | [email-send-ambient-cancellation](../../rules/framework/3-infra/axis-email/email-send-ambient-cancellation.yaml) |
| The failure code `ERROR_SENDING_EMAIL` is the caller-facing contract, not the message | [email-error-code-is-contract](../../rules/framework/3-infra/axis-email/email-error-code-is-contract.yaml) |

### The bundled MimeKit adapter — `AxisEmail.MimeKit`

| Context | Rule |
|---|---|
| Wiring — `AddAxisMimeKitEmail(configuration)` (binds settings, registers Scoped) | [email-mimekit-registration](../../rules/framework/3-infra/axis-email/email-mimekit-registration.yaml) |
| How the payload maps onto a `MimeMessage` (From from settings) | [email-mimekit-message-mapping](../../rules/framework/3-infra/axis-email/email-mimekit-message-mapping.yaml) |
| SMTP transport — connect / authenticate / send / disconnect; `SslEnabled` → `Auto`/`None` | [email-mimekit-smtp-transport](../../rules/framework/3-infra/axis-email/email-mimekit-smtp-transport.yaml) |
| The boundary — any exception becomes a logged `InternalServerError` | [email-mimekit-failure-to-axiserror](../../rules/framework/3-infra/axis-email/email-mimekit-failure-to-axiserror.yaml) |

### Writing your own adapter

| Context | Rule |
|---|---|
| The contract a custom `IAxisEmailService` (SendGrid, SES, Mailgun, test double) must honour | [email-adapter-swappable-port](../../rules/framework/3-infra/axis-email/email-adapter-swappable-port.yaml) |

## See also

- `axis-result` — the monad `SendAsync` returns; `OrElseAsync` dead-letters a failed send, `ThenAsync` chains the next step.
- `axis-bus` — the event handler that usually calls `SendAsync`, so the command that triggered the mail stays unaware of it.
- `axis-mediator` — the ambient `CancellationToken` a cancellation-aware adapter would read (the in-box one does not).
- `axis-dotnet-architect` — the hub; the swappable-infra-port pattern (`IAxis*` + `AxisResult` + `AddAxis*`) email is one instance of.
- `axis-rules` — how these rules are authored and maintained (the extraction method from code).
