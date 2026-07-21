using System.Text.Json;

namespace Axis.Saga.Json;

internal static class AxisSagaJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
