using EarnedScreen.Core;

namespace EarnedScreen.Service;

/// <summary>
/// The single source of truth for blocking. Owns the hosts file, the persisted state, and the
/// per-day budget. All public methods are serialized behind a lock so the pipe servers and the
/// expiry loop can call in concurrently.
/// </summary>
public sealed class EnforcementEngine
{
    private readonly object _lock = new();
    private readonly HostsFileManager _hosts = new();
    private readonly BrowserPolicyManager _browser = new();
    private readonly DnsFilterManager _dns = new();
    private readonly NetworkUiLockManager _netLock = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly StateStore _stateStore = new();
    private readonly ILogger<EnforcementEngine> _log;

    private Settings _settings;
    private SessionState _state;

    /// <summary>Raised (outside the lock) when the Guillotine drops at session end.</summary>
    public event Action? SessionEnded;

    public EnforcementEngine(ILogger<EnforcementEngine> log)
    {
        _log = log;
        _settings = _settingsStore.Load();
        _state = _stateStore.Load();
    }

    /// <summary>Reconciles persisted state with reality at startup.</summary>
    public void Initialize()
    {
        lock (_lock)
        {
            _settings = _settingsStore.Load();
            _state = _stateStore.Load();

            // DoH bypass is disabled once on startup and stays off for the service lifetime.
            // Browsers with DoH enabled send encrypted DNS queries directly to remote resolvers,
            // completely bypassing the OS hosts file and making block ineffective.
            _browser.DisableDoH();
            _log.LogInformation("Browser DoH disabled via policy registry.");

            // Family-safe DNS: pin the adapter to CleanBrowsing and lock the network UI.
            ApplyDnsFilter();

            var now = DateTime.UtcNow;
            if (_state.Status == BlockStatus.Unlocked && _state.SessionEndUtc is { } end && end > now)
            {
                // A session was still running when the service stopped: resume the remaining time.
                _log.LogInformation("Resuming active session, ends {End:u}", end);
                _hosts.RemoveBlock();
                _hosts.FlushDns();
            }
            else
            {
                EnterBlockedState();
            }
        }
    }

    public StatusResponse GetStatus()
    {
        lock (_lock) return BuildStatus(true, null);
    }

    public StatusResponse RequestUnlock()
    {
        lock (_lock)
        {
            _settings = _settingsStore.Load(); // pick up any Claude-Code edits to limits/domains
            var today = DateOnly.FromDateTime(DateTime.Now);

            if (_state.Status == BlockStatus.Unlocked && _state.SessionEndUtc is { } e && e > DateTime.UtcNow)
                return BuildStatus(true, "A session is already active.");

            if (!_state.IsSessionAvailable(_settings.SessionsPerDay, today))
                return BuildStatus(false, "No sessions left today. Come back tomorrow.");

            if (_state.LastSessionDate != today)
            {
                _state.LastSessionDate = today;
                _state.SessionsToday = 0;
            }
            _state.SessionsToday++;
            _state.Status = BlockStatus.Unlocked;
            _state.SessionEndUtc = DateTime.UtcNow.AddMinutes(_settings.SessionMinutes);
            _stateStore.Save(_state);

            _hosts.RemoveBlock();
            _hosts.FlushDns();

            _log.LogInformation("Session granted until {End:u} ({Count}/{Max} today)",
                _state.SessionEndUtc, _state.SessionsToday, _settings.SessionsPerDay);

            return BuildStatus(true, "Enjoy your earned screen time.");
        }
    }

    /// <summary>Called by the worker loop; re-blocks if the active session has expired.</summary>
    public void CheckExpiry()
    {
        var dropped = false;
        lock (_lock)
        {
            if (_state.Status == BlockStatus.Unlocked &&
                _state.SessionEndUtc is { } end && end <= DateTime.UtcNow)
            {
                _log.LogInformation("Guillotine: session expired, re-blocking.");
                EnterBlockedState();
                dropped = true;
            }
        }

        if (dropped) SessionEnded?.Invoke();
    }

    /// <summary>Applies family-safe DNS + UI lock. Idempotent; called on startup and on network change.</summary>
    public void ApplyDnsFilter()
    {
        lock (_lock)
        {
            if (!_settings.DnsFilter.Enabled) return;
            _dns.Apply(_settings.DnsFilter);
            if (_settings.DnsFilter.LockNetworkUi) _netLock.Lock();
            _log.LogInformation("Family-safe DNS applied ({Filter}).", _settings.DnsFilter.Filter);
        }
    }

    private void EnterBlockedState()
    {
        _hosts.ApplyBlock(_settings.BlockedDomains);
        _hosts.FlushDns();
        _state.Status = BlockStatus.Blocked;
        _state.SessionEndUtc = null;
        _stateStore.Save(_state);
    }

    private StatusResponse BuildStatus(bool success, string? message)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var remaining = 0;
        if (_state.Status == BlockStatus.Unlocked && _state.SessionEndUtc is { } end)
            remaining = Math.Max(0, (int)(end - DateTime.UtcNow).TotalSeconds);

        return new StatusResponse
        {
            Success = success,
            Message = message,
            Status = _state.Status,
            SessionAvailableToday = _state.IsSessionAvailable(_settings.SessionsPerDay, today),
            SessionMinutes = _settings.SessionMinutes,
            SessionEndUtc = _state.SessionEndUtc,
            RemainingSeconds = remaining,
            DnsFilterActive = _settings.DnsFilter.Enabled,
            DnsFilterName = _settings.DnsFilter.Filter,
        };
    }
}
