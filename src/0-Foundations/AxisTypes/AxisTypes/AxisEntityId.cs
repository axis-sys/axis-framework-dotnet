using AxisTypes.SourceGenerator;

namespace Axis;

[ValueObject]
public readonly partial record struct AxisEntityId
{
    public static AxisEntityId New => new(Guid.CreateVersion7().ToString());

    private AxisEntityId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentNullException(nameof(value), "AxisEntityId cannot be null or empty");

        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException($"'{value}' is not a valid AxisEntityId");

        if (guid.Version != 7)
            throw new ArgumentException($"'{value}' is not a valid AxisEntityId (version 7 expected)");

        Value = value;
    }

    private string Value { get; }
}
