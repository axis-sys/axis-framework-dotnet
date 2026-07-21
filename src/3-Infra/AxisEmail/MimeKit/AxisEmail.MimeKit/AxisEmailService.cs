using System.Diagnostics.CodeAnalysis;
using Axis;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AxisEmail.MimeKit;

[ExcludeFromCodeCoverage]
public class AxisEmailService(IOptions<AxisEmailSettings> emailSettings, IAxisLogger<AxisEmailService> logger) : IAxisEmailService
{
    private readonly AxisEmailSettings _settings = emailSettings.Value;

    public async Task<AxisResult> SendAsync(AxisEmailData emailData)
    {
        try
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(_settings.Sender.Name, _settings.Sender.Address));
            message.Subject = emailData.Subject;
            message.Body = new TextPart(emailData.BodyTextType) { Text = emailData.Body };
            foreach (var (name, emailAddress) in emailData.To)
                message.To.Add(new MailboxAddress(name, emailAddress));
            foreach (var (name, emailAddress) in emailData.Cc)
                message.Cc.Add(new MailboxAddress(name, emailAddress));

            using var client = new SmtpClient();
            var secureSocketOptions = _settings.Smtp.SslEnabled ? SecureSocketOptions.Auto : SecureSocketOptions.None;
            await client.ConnectAsync(_settings.Smtp.Host, _settings.Smtp.Port, secureSocketOptions);
            await client.AuthenticateAsync(_settings.Sender.Address, _settings.Sender.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ERROR_SENDING_EMAIL");
            return AxisError.InternalServerError("ERROR_SENDING_EMAIL");
        }

        return AxisResult.Ok();
    }

}
