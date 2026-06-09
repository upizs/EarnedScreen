using System.Net.NetworkInformation;
using EarnedScreen.Core;

namespace EarnedScreen.Service;

/// <summary>
/// Orchestrates the enforcement engine and the two pipe servers, and ticks the expiry check that
/// drops the Guillotine when an earned session runs out.
/// </summary>
public class Worker : BackgroundService
{
    private readonly EnforcementEngine _engine;
    private readonly CommandPipeServer _command;
    private readonly EventPipeServer _events;
    private readonly ILogger<Worker> _log;

    public Worker(EnforcementEngine engine, CommandPipeServer command, EventPipeServer events, ILogger<Worker> log)
    {
        _engine = engine;
        _command = command;
        _events = events;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("EarnedScreen service starting; enforcing the Wall.");
        _engine.SessionEnded += OnSessionEnded;
        _engine.Initialize();

        // Re-pin family-safe DNS whenever the network changes (e.g. a new adapter comes up).
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;

        var cmdTask = _command.RunAsync(stoppingToken);
        var evtTask = _events.RunAsync(stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _engine.CheckExpiry();
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        finally
        {
            _engine.SessionEnded -= OnSessionEnded;
            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        }

        await Task.WhenAll(cmdTask, evtTask);
    }

    private void OnSessionEnded() => _events.Push(new EventMessage { Type = EventType.SessionEnded });

    private void OnNetworkChanged(object? sender, EventArgs e)
    {
        try { _engine.ApplyDnsFilter(); }
        catch (Exception ex) { _log.LogDebug(ex, "DNS re-apply on network change failed"); }
    }
}
