using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using EarnedScreen.Core;

namespace EarnedScreen.App;

/// <summary>
/// Thin client over the service's named pipes. One-shot request/response on the Command pipe,
/// plus a long-lived listener on the Events pipe that raises <see cref="SessionEnded"/>.
/// </summary>
public sealed class ServiceClient
{
    public event Action? SessionEnded;

    public Task<StatusResponse?> GetStatusAsync(CancellationToken ct = default)
        => SendAsync(CommandType.GetStatus, ct);

    public Task<StatusResponse?> RequestUnlockAsync(CancellationToken ct = default)
        => SendAsync(CommandType.RequestUnlock, ct);

    private static async Task<StatusResponse?> SendAsync(CommandType cmd, CancellationToken ct)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeNames.Command, PipeDirection.InOut);
            await pipe.ConnectAsync(3000, ct);

            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };

            await writer.WriteLineAsync(JsonSerializer.Serialize(new CommandRequest { Command = cmd }));
            var line = await reader.ReadLineAsync(ct);
            return line is null ? null : JsonSerializer.Deserialize<StatusResponse>(line);
        }
        catch
        {
            return null; // service not running / pipe unavailable
        }
    }

    public async Task ListenForEventsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", PipeNames.Events, PipeDirection.InOut);
                await pipe.ConnectAsync(ct);
                using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);

                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line is null) break; // server dropped the connection

                    var msg = JsonSerializer.Deserialize<EventMessage>(line);
                    if (msg?.Type == EventType.SessionEnded)
                        SessionEnded?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                try { await Task.Delay(2000, ct); } catch { break; }
            }
        }
    }
}
