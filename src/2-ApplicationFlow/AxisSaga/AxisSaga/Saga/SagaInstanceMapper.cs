using System.Data.Common;
using Axis.SharedKernel;

namespace Axis.Saga;

/// <summary>
/// Maps a row from <c>SAGA_INSTANCES</c> (the canonical column ordering used by every read in the
/// dialect adapters) into an <see cref="AxisSagaInstance"/>. It takes the ADO.NET base
/// <see cref="DbDataReader"/> — not a dialect-specific reader — so a single mapper serves Postgres
/// (Npgsql), MySQL (MySqlConnector) and any future provider. Timestamps are read provider-neutrally:
/// the value is taken as a <see cref="DateTime"/> and pinned to UTC (Postgres stores
/// <c>timestamptz</c>, MySQL stores a UTC <c>DATETIME(6)</c>), then surfaced as a zero-offset
/// <see cref="DateTimeOffset"/>.
/// </summary>
public static class SagaInstanceMapper
{
    public static AxisSagaInstance Map(DbDataReader r) => new()
    {
        SagaId = r.GetString(0),
        SagaName = r.GetString(1),
        Status = Enum.Parse<AxisSagaStatus>(r.GetString(2)),
        CurrentStage = r.IsDBNull(3) ? null : r.GetString(3),
        PayloadJson = r.GetString(4),
        LastErrorCode = r.IsDBNull(5) ? null : r.GetString(5),
        LastErrorMessage = r.IsDBNull(6) ? null : r.GetString(6),
        Version = r.GetInt32(7),
        CreatedAt = ReadUtc(r, 8),
        UpdatedAt = ReadUtc(r, 9)
    };

    private static DateTimeOffset ReadUtc(DbDataReader r, int ordinal)
        => new(DateTime.SpecifyKind(r.GetDateTime(ordinal), DateTimeKind.Utc));
}
