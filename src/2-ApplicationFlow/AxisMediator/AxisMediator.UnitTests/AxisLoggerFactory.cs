using Microsoft.Extensions.Logging;

namespace AxisMediator.UnitTests;

internal static class AxisLoggerFactory
{
    public static ILoggerFactory Create()
        => LoggerFactory.Create(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug));
}
