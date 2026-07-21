namespace Axis;

public record AxisEmailSettings
{
    public SenderData Sender { get; init; } = new();
    public SmtpData Smtp { get; init; } = new();

    public class SenderData
    {
        public string Address { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }

    public class SmtpData
    {
        public string Host { get; init; } = string.Empty;
        public int Port { get; init; }
        public bool SslEnabled { get; init; }
    }
}
