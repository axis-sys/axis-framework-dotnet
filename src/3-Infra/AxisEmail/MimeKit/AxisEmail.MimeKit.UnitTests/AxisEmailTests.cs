namespace AxisEmail.MimeKit.UnitTests;

public class AxisEmailTests
{
    // ── AxisEmailData ───────────────────────────────────────────────────────

    [Fact]
    public void AxisEmailData_Creation_SetsRequiredProperties()
    {
        var to = new[] { ("John", "john@example.com") };
        var data = new AxisEmailData
        {
            To = to,
            Subject = "Test Subject",
            Body = "Test Body"
        };

        Assert.Equal(to, data.To);
        Assert.Equal("Test Subject", data.Subject);
        Assert.Equal("Test Body", data.Body);
    }

    [Fact]
    public void AxisEmailData_BodyTextType_DefaultsToPlain()
    {
        var data = new AxisEmailData
        {
            To = [("John", "john@example.com")],
            Subject = "Subject",
            Body = "Body"
        };

        Assert.Equal("plain", data.BodyTextType);
    }

    [Fact]
    public void AxisEmailData_Cc_DefaultsToEmpty()
    {
        var data = new AxisEmailData
        {
            To = [("John", "john@example.com")],
            Subject = "Subject",
            Body = "Body"
        };

        Assert.Empty(data.Cc);
    }

    [Fact]
    public void AxisEmailData_CanOverrideBodyTextType()
    {
        var data = new AxisEmailData
        {
            To = [("John", "john@example.com")],
            Subject = "Subject",
            Body = "<p>HTML</p>",
            BodyTextType = "html"
        };

        Assert.Equal("html", data.BodyTextType);
    }

    [Fact]
    public void AxisEmailData_CanSetCcRecipients()
    {
        var cc = new[] { ("Jane", "jane@example.com") };
        var data = new AxisEmailData
        {
            To = [("John", "john@example.com")],
            Subject = "Subject",
            Body = "Body",
            Cc = cc
        };

        Assert.Single(data.Cc);
    }

    // ── AxisEmailSettings ───────────────────────────────────────────────────

    [Fact]
    public void AxisEmailSettings_Sender_DefaultsToEmpty()
    {
        var settings = new AxisEmailSettings();

        Assert.NotNull(settings.Sender);
        Assert.Equal(string.Empty, settings.Sender.Address);
        Assert.Equal(string.Empty, settings.Sender.Password);
        Assert.Equal(string.Empty, settings.Sender.Name);
    }

    [Fact]
    public void AxisEmailSettings_Smtp_DefaultsToEmpty()
    {
        var settings = new AxisEmailSettings();

        Assert.NotNull(settings.Smtp);
        Assert.Equal(string.Empty, settings.Smtp.Host);
        Assert.Equal(0, settings.Smtp.Port);
        Assert.False(settings.Smtp.SslEnabled);
    }

    [Fact]
    public void AxisEmailSettings_CanOverrideSenderProperties()
    {
        var settings = new AxisEmailSettings
        {
            Sender = new AxisEmailSettings.SenderData
            {
                Address = "sender@example.com",
                Password = "secret",
                Name = "Sender Name"
            }
        };

        Assert.Equal("sender@example.com", settings.Sender.Address);
        Assert.Equal("secret", settings.Sender.Password);
        Assert.Equal("Sender Name", settings.Sender.Name);
    }

    [Fact]
    public void AxisEmailSettings_CanOverrideSmtpProperties()
    {
        var settings = new AxisEmailSettings
        {
            Smtp = new AxisEmailSettings.SmtpData
            {
                Host = "smtp.example.com",
                Port = 587,
                SslEnabled = true
            }
        };

        Assert.Equal("smtp.example.com", settings.Smtp.Host);
        Assert.Equal(587, settings.Smtp.Port);
        Assert.True(settings.Smtp.SslEnabled);
    }
}
