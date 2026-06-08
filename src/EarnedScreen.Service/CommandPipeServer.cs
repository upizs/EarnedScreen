using System.Text;
using System.Text.Json;
using EarnedScreen.Core;

namespace EarnedScreen.Service;

/// <summary>
/// Request/response pipe. The app sends one <see cref="CommandRequest"/> (newline-terminated JSON),
/// the server replies with one <see cref="StatusResponse"/>, then the connection closes.
/// </summary>
public sealed class CommandPipeServer
{
    private readonly EnforcementEngine _engine;
    private readonly ILogger<CommandPipeServer> _log;

    public CommandPipeServer(EnforcementEngine engine, ILogger<CommandPipeServer> log)
    {
        _engine = engine;
        _log = log;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = PipeFactory.CreateServer(PipeNames.Command);
                await server.WaitForConnectionAsync(ct);
                await HandleAsync(server, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Command pipe error");
            }
        }
    }

    private async Task HandleAsync(Stream server, CancellationToken ct)
    {
        using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
        await using var writer = new StreamWriter(server, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };

        var line = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(line)) return;

        CommandRequest? req;
        try { req = JsonSerializer.Deserialize<CommandRequest>(line); }
        catch { req = null; }

        var resp = req?.Command switch
        {
            CommandType.RequestUnlock => _engine.RequestUnlock(),
            CommandType.GetStatus => _engine.GetStatus(),
            _ => new StatusResponse { Success = false, Message = "Unknown command." },
        };

        await writer.WriteLineAsync(JsonSerializer.Serialize(resp));
    }
}
