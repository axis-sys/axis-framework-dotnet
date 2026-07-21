using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AxisTelemetry.AzureMonitor.IntegrationTests;

/// <summary>
/// There is no official Application Insights emulator (unlike Azurite for Blob), but the Azure
/// Monitor exporter is plain HTTP: it POSTs gzipped NDJSON envelopes to the connection string's
/// IngestionEndpoint. This fixture stands in for that endpoint on a loopback port, captures every
/// payload and answers the track API's success body — the real SDK pipeline, no cloud, no Docker.
/// </summary>
public sealed class FakeIngestionFixture : IAsyncLifetime
{
    private const string StatsbeatVariable = "APPLICATIONINSIGHTS_STATSBEAT_DISABLED";

    private readonly HttpListener _listener = new();
    private readonly ConcurrentQueue<string> _payloads = new();
    private Task _serverLoop = Task.CompletedTask;
    private string? _previousStatsbeat;

    public string ConnectionString { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        // Statsbeat would call Microsoft's real endpoints from inside the test run.
        _previousStatsbeat = Environment.GetEnvironmentVariable(StatsbeatVariable);
        Environment.SetEnvironmentVariable(StatsbeatVariable, "true");

        var endpoint = $"http://localhost:{GetFreePort()}/";
        ConnectionString =
            $"InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint={endpoint};LiveEndpoint={endpoint}";

        _listener.Prefixes.Add(endpoint);
        _listener.Start();
        _serverLoop = Task.Run(ServeAsync);
        return ValueTask.CompletedTask;
    }

    public async Task<bool> WaitForPayloadAsync(string fragment, TimeSpan timeout)
    {
        var deadline = TimeProvider.System.GetTimestamp();
        while (TimeProvider.System.GetElapsedTime(deadline) < timeout)
        {
            if (ContainsPayload(fragment))
                return true;
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return ContainsPayload(fragment);
    }

    public bool ContainsPayload(string fragment)
        => _payloads.Any(p => p.Contains(fragment, StringComparison.Ordinal));

    public int RequestCount => _payloads.Count;

    public string DumpPayloads(int maxCharsPerPayload)
        => string.Join("\n---\n", _payloads.Select(p => p[..Math.Min(p.Length, maxCharsPerPayload)]));

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        _listener.Close();
        try
        {
            await _serverLoop;
        }
        catch (Exception)
        {
            // Listener shutdown races the accept loop; nothing to report in a test double.
        }

        Environment.SetEnvironmentVariable(StatsbeatVariable, _previousStatsbeat);
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (!_listener.IsListening)
            {
                return;
            }

            await HandleAsync(context);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        using (var buffer = new MemoryStream())
        {
            await context.Request.InputStream.CopyToAsync(buffer);
            _payloads.Enqueue(Decode(buffer.ToArray()));
        }

        var response = "{\"itemsReceived\":1,\"itemsAccepted\":1,\"errors\":[]}"u8.ToArray();
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        await context.Response.OutputStream.WriteAsync(response);
        context.Response.Close();
    }

    // The exporter gzips the NDJSON body; sniff the magic bytes instead of trusting the header.
    private static string Decode(byte[] body)
    {
        if (body is [0x1f, 0x8b, ..])
        {
            using var gzip = new GZipStream(new MemoryStream(body), CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        return Encoding.UTF8.GetString(body);
    }

    private static int GetFreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}

[CollectionDefinition("FakeIngestionCollection")]
public class FakeIngestionCollection : ICollectionFixture<FakeIngestionFixture>;
