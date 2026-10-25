namespace Scaffolds.ECommerce.Contracts.Driving.Admin.v1.MigrateDatabase;

/// <summary>Outcome of a database migration run.</summary>
public sealed record MigrateDatabaseResponse : IAxisCommandResponse
{
    /// <summary>The schemas brought up to date, in the order they were migrated.</summary>
    public required IReadOnlyList<string> Schemas { get; init; }
}
