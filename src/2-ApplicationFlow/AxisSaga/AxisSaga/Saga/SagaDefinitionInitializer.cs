using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Axis.Contracts.Configuration;
using Axis.Ports;
using Axis.Saga.Json;

namespace Axis.Saga;

/// <inheritdoc/>
internal class SagaDefinitionInitializer(
    ISagaDefinitionStore store,
    IAxisSagaDefinitionRegistry registry,
    IAxisLogger<SagaDefinitionInitializer> logger
) : IAxisSagaDefinitionInitializer
{
    public async Task<int> InitializeAsync(CancellationToken cancellationToken)
    {
        var written = 0;
        foreach (var def in registry.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var snapshot = SerializeSnapshot(def);
                var hash = ComputeHash(snapshot);
                if (await store.UpsertAsync(def.SagaName, hash, snapshot, cancellationToken))
                    written++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to upsert saga definition", ("sagaName", def.SagaName));
            }
        }
        return written;
    }

    private static string SerializeSnapshot(AxisSagaDefinition def) => JsonSerializer.Serialize(new
    {
        sagaName = def.SagaName,
        payloadType = def.PayloadType.AssemblyQualifiedName,
        forwardStages = def.ForwardStages.Select(SnapshotStage).ToArray(),
        errorStages = def.ErrorStages.Select(SnapshotStage).ToArray()
    }, AxisSagaJsonOptions.Default);

    private static object SnapshotStage(AxisSagaStageDefinition s) => new
    {
        stageName = s.StageName,
        isErrorStage = s.IsErrorStage,
        onSuccessNext = s.NextStageOnSuccess,
        onErrorRoute = s.RouteToOnError
    };

    private static string ComputeHash(string snapshot)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(snapshot));
        return Convert.ToHexString(bytes);
    }
}
