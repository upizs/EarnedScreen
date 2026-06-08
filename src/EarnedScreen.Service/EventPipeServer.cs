using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using EarnedScreen.Core;

namespace EarnedScreen.Service;

/// <summary>
/// Server-push pipe. Holds a single connected client (the tray app) and pushes events such as
/// <see cref="EventType.SessionEnded"/> so the app can show the cool-down lock. Best-effort: if no
/// client is connected, the network cut still happened — only the UI nudge is missed.
/// </summary>
public sealed class EventPipeServer
{
    private readonly ILogger<EventPipeServer> _log;
    private readonly object _lock = new();
    private NamedPipeServerStream? _client;

    public EventPipeServer(ILogger<EventPipeServer> log) => _log = log;

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var server = PipeFactory.CreateServer(PipeNames.Events);
            try
            {
                await server.WaitForConnectionAsync(ct);
                lock (_lock)
                {
                    _client?.Dispose();
                    _client = server;
                }

                // Park here until the client disconnects (read returns 0) or we shut down.
                var buffer = new byte[1];
                _ = await server.ReadAsync(buffer, ct);
            }
            catch (OperationCanceledException)
            {
                server.Dispose();
                break;
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Event pipe client dropped");
            }
            finally
            {
                lock (_lock)
                {
                    if (_client == server) _client = null;
                }
            }
        }
    }

    public void Push(EventMessage message)
    {
        lock (_lock)
        {
            if (_client is null || !_client.IsConnected) return;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message) + "\n");
                _client.Write(bytes, 0, bytes.Length);
                _client.Flush();
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Failed to push event");
            }
        }
    }
}
