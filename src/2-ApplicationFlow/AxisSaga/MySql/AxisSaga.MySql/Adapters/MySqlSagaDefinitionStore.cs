using Axis.Persistence.Scripts;
using Axis.Ports;
using AxisSaga.MySql.Persistence;
using MySqlConnector;

namespace AxisSaga.MySql.Adapters;

/// <inheritdoc/>
internal class MySqlSagaDefinitionStore(AxisSagaMySqlDataSource dataSource) : ISagaDefinitionStore
{
    public async Task<bool> UpsertAsync(string sagaName, string definitionHash, string definitionJson, CancellationToken cancellationToken)
    {
        await using var conn = await dataSource.Inner.OpenConnectionAsync(cancellationToken);
        await using var cmd = new MySqlCommand(
            $"""
             INSERT INTO {SagaDefinitionsTable.Table}
                 ({SagaDefinitionsTable.SagaName}, {SagaDefinitionsTable.DefinitionHash},
                  {SagaDefinitionsTable.DefinitionJson}, {SagaDefinitionsTable.UpdatedAt})
             VALUES (@name, @hash, @json, UTC_TIMESTAMP(6)) AS new
             ON DUPLICATE KEY UPDATE
                 {SagaDefinitionsTable.DefinitionHash} =
                     IF({SagaDefinitionsTable.Table}.{SagaDefinitionsTable.DefinitionHash} <> new.{SagaDefinitionsTable.DefinitionHash},
                        new.{SagaDefinitionsTable.DefinitionHash}, {SagaDefinitionsTable.Table}.{SagaDefinitionsTable.DefinitionHash}),
                 {SagaDefinitionsTable.DefinitionJson} =
                     IF({SagaDefinitionsTable.Table}.{SagaDefinitionsTable.DefinitionHash} <> new.{SagaDefinitionsTable.DefinitionHash},
                        new.{SagaDefinitionsTable.DefinitionJson}, {SagaDefinitionsTable.Table}.{SagaDefinitionsTable.DefinitionJson}),
                 {SagaDefinitionsTable.UpdatedAt} =
                     IF({SagaDefinitionsTable.Table}.{SagaDefinitionsTable.DefinitionHash} <> new.{SagaDefinitionsTable.DefinitionHash},
                        UTC_TIMESTAMP(6), {SagaDefinitionsTable.Table}.{SagaDefinitionsTable.UpdatedAt})
             """, conn);
        cmd.Parameters.AddWithValue("name", sagaName);
        cmd.Parameters.AddWithValue("hash", definitionHash);
        cmd.Parameters.AddWithValue("json", definitionJson);

        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }
}
