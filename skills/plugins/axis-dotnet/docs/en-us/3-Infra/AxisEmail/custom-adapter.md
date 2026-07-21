# Custom adapter · write your own `IAxisEmailService`

> Swap the SMTP adapter for SendGrid, SES, Mailgun, Postmark — or a test double that captures every send. Implement one method, register your class for `IAxisEmailService`.

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

## When to use

- A provider's HTTP API (SendGrid, SES, Mailgun, Postmark, Resend) gives you delivery tracking, template-side rendering, suppression management.
- A **test double** so unit tests do not hit the network.
- A **dual-write** adapter that records every send to an audit table before delivering.

## When *not* to use

| You want to… | Use instead |
|---|---|
| stay on SMTP | the in-box [`AxisEmail.MimeKit` adapter](mimekit-adapter.md) |
| expose provider-specific features (categories, custom args) | extend the contract with a new interface and require the *adapter* to implement both |

---

## The contract you must honour

| Behaviour | Required | Rationale |
|---|---|---|
| Return `Task<AxisResult>`, never throw cooperatively | yes | the railway depends on it |
| `Ok()` when the message was accepted by the provider | yes | callers chain on success |
| `Error(AxisError.InternalServerError("ERROR_SENDING_EMAIL"))` on auth / network / rejection | recommended | match the in-box code so caller-side handling is identical |
| Honour the `BodyTextType` (`"plain"`/`"html"`) | yes | the payload promised it |
| Read sender / credentials from `AxisEmailSettings` (or your own settings object registered the same way) | recommended | configuration stays in one place |
| Log failures via `AxisLogger` | recommended | enrichers attach correlation / tenant |

---

## Real-world example — a recording test double

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

// in a test
services.AddSingleton<IAxisEmailService, CapturingEmailService>();
// later
Assert.Single(emailService.Sent);
Assert.Equal("Welcome", emailService.Sent[0].Subject);
```

**Why it pays off:** integration tests assert on the *exact* payload your code produced — subject, body, recipients — without spinning up an SMTP server or mocking MailKit.

---

## See also

- [The `IAxisEmailService` contract](iaxisemailservice.md) — what you have to satisfy
- [`AxisEmail.MimeKit` adapter](mimekit-adapter.md) — the in-box reference
- [Settings · `AxisEmailSettings`](settings.md) — the configuration shape

---

↩ [Back to AxisEmail docs](README.md)
