# Contract · `IAxisEmailService`

> The whole port is one method. The payload (`AxisEmailData`) carries every recipient, every header, the subject and the body. The result is an `AxisResult` — `Ok()` on success, a typed error on any SMTP / network / authentication failure.

```csharp
public interface IAxisEmailService
{
    Task<AxisResult> SendAsync(AxisEmailData data);
}
```

---

## When to use

- Transactional notifications: order confirmations, password resets, alerts, magic-link logins.
- Internal notifications: nightly summaries, error alerts, admin pings.

## When *not* to use

| You want to… | Use instead |
|---|---|
| send **bulk marketing** mail | a dedicated ESP (SendGrid, Mailchimp) — they handle bounce/click/reputation |
| **chat / instant message** | a different transport (Slack webhook, Teams API) |
| receive incoming mail | the bus + an inbound webhook handler |

---

## What `SendAsync` returns

Reading the bundled adapter directly:

| Outcome | Returned `AxisResult` |
|---|---|
| every recipient accepted by the SMTP server | `Ok()` |
| auth failure, network failure, SMTP rejection, MIME error | `Error(AxisError.InternalServerError("ERROR_SENDING_EMAIL"))` (and the exception is logged via `AxisLogger`) |

> The adapter does **not** distinguish "permanent" from "transient" failures in the contract — the typed `AxisErrorType.InternalServerError` covers everything. Use a custom adapter if you need to surface 4xx vs. 5xx SMTP codes separately.

---

## Real-world examples

### 1. Notify via event handler

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

**Why it pays off:** the command handler that shipped the order has no idea an email is going out. The bus fan-out wires the side effect; the handler returns an `AxisResult` so the publish call sees the failure too.

### 2. Magic-link login

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

**Why it pays off:** the HTML body uses `BodyTextType = "html"` — the same method, same return type. No `MailMessage.IsBodyHtml = true` magic at the call site.

### 3. Recovering from a transient failure

```csharp
return await email.SendAsync(data)
    .OrElseAsync(_ => deadLetter.EnqueueAsync(data));   // dead-letter for retry
```

**Why it pays off:** `SendAsync` returns the non-generic `Task<AxisResult>`, so the fallback runs through `OrElseAsync` — a failed send becomes a value on the rail and the recovery branch routes it to a dead-letter queue (which returns its own `Task<AxisResult>`) for later retry, without an exception ever reaching the caller. The contract only ever yields `AxisErrorType.InternalServerError` (see above), so recovering on any failure is exactly the dead-letter path.

---

## See also

- [Email payload · `AxisEmailData`](axis-email-data.md) — what to put inside
- [Settings · `AxisEmailSettings`](settings.md) — sender + SMTP
- [`AxisEmail.MimeKit` adapter](mimekit-adapter.md) — the in-box implementation
- [Custom adapter](custom-adapter.md) — write one for SendGrid / SES / Mailgun

---

↩ [Back to AxisEmail docs](README.md)
