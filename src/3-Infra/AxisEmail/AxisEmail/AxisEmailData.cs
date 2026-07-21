namespace Axis;

public record AxisEmailData
{
    public required IEnumerable<(string Name, string Email)> To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public string BodyTextType { get; init; } = "plain";
    public IEnumerable<(string Name, string Email)> Cc { get; init; } = [];
}
