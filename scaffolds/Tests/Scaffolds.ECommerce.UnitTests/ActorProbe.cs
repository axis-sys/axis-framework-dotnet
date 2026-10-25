namespace Scaffolds.ECommerce.UnitTests;

internal sealed class ActorProbe
{
    internal sealed record Observation(bool Stamped, AxisEntityId? Actor);

    public List<Observation> Observations { get; } = [];
}
