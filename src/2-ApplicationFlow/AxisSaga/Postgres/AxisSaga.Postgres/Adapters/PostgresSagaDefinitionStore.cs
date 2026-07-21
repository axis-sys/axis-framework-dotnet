using Axis.Persistence.Scripts;
using Axis.Ports;
using AxisSaga.Postgres.Persistence;
using Npgsql;

namespace AxisSaga.Postgres.Adapters;

/// <inheritdoc/>
internal class PostgresSagaDefinitionStore(AxisSagaPostgresDataSource dataSource) : ISagaDefinitionStore
{
    public async Task<bool> UpsertAsync(string sagaName, string definitionHash, string definitionJson, CancellationToken cancellationToken)
    {
        await using var conn = await dataSource.Inner.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            $"""
             INSERT INTO {SagaDefinitionsTable.Table}
                 ({SagaDefinitionsTable.SagaName}, {SagaDefinitionsTable.DefinitionHash},
                  {SagaDefinitionsTable.DefinitionJson}, {SagaDefinitionsTable.UpdatedAt})
             VALUES (@name, @hash, @json::JSONB, NOW())
             ON CONFLICT ({SagaDefinitionsTable.SagaName})
             DO UPDATE SET
                 {SagaDefinitionsTable.DefinitionHash} = EXCLUDED.{SagaDefinitionsTable.DefinitionHash},
                 {SagaDefinitionsTable.DefinitionJson} = EXCLUDED.{SagaDefinitionsTable.DefinitionJson},
                 {SagaDefinitionsTable.UpdatedAt}      = NOW()
             WHERE {SagaDefinitionsTable.Table}.{SagaDefinitionsTable.DefinitionHash}
                   IS DISTINCT FROM EXCLUDED.{SagaDefinitionsTable.DefinitionHash}
             """, conn);
        cmd.Parameters.AddWithValue("name", sagaName);
        cmd.Parameters.AddWithValue("hash", definitionHash);
        cmd.Parameters.AddWithValue("json", definitionJson);

        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rows == 1;
    }
}
