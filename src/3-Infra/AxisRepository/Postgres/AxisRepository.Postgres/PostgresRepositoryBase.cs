using System.Data.Common;
using Axis;
using AxisMediator.Contracts;
using Npgsql;

namespace AxisRepository.Postgres;

/// <inheritdoc cref="AxisRepositoryBase{TCommand,TReader,TParameters}"/>
public abstract class PostgresRepositoryBase(
    IAxisMediator mediator,
    IAxisLogger<PostgresRepositoryBase> logger,
    IPostgresUnitOfWork uow
) : AxisRepositoryBase<NpgsqlCommand, NpgsqlDataReader, NpgsqlParameterCollection>(mediator, uow)
{
    protected override bool IsTransient(DbException exception) => exception is NpgsqlException npgsql && npgsql.SqlState
        is "40001"  // serialization failure
        or "40P01"  // deadlock detected
        or "55P03"  // lock not available (NOWAIT / lock_timeout expired)
        or "08000"  // connection exception (generic)
        or "08001"  // unable to connect
        or "08003"  // connection does not exist
        or "08004"  // connection rejected by server (recovery / throttling)
        or "08006"  // connection failure
        or "53000"  // insufficient resources (generic)
        or "53100"  // disk full — use a longer backoff than the other codes
        or "53200"  // out of memory
        or "53300"  // too many connections
        or "53400"  // configuration limit exceeded — use a longer backoff
        or "55000"  // object not in prerequisite state (generic)
        or "55006"  // object in use
        or "57P01"  // admin shutdown
        or "57P02"  // crash shutdown
        or "57P03"  // cannot connect now
        or "57P05"; // idle session timeout

    // Match on SqlState 23505 (unique_violation), not the English message text, so detection is
    // locale-independent: a Conflict (409) must not become a generic 500 on a non-English server.
    protected override bool IsDuplicateKey(DbException exception)
        => exception is NpgsqlException { SqlState: "23505" };

    // 42P01 (undefined_table) is what DML against a schema-qualified relation raises even when the
    // whole schema is missing; 3F000 (invalid_schema_name) covers statements that resolve the
    // schema itself first.
    protected override bool IsSchemaMissing(DbException exception)
        => exception is NpgsqlException { SqlState: "42P01" or "3F000" };

    protected override string ErrorPrefix => "POSTGRES";

    protected override void LogError(Exception exception, string message) => logger.LogError(exception, message);
}
