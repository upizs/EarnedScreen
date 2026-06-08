namespace EarnedScreen.Core;

public enum BlockStatus
{
    Blocked,
    Unlocked,
}

/// <summary>
/// Persisted runtime state at %ProgramData%\EarnedScreen\state.json. The service is the only
/// writer. Survives reboots so the daily limit and an in-progress session both hold across restarts.
/// </summary>
public sealed class SessionState
{
    public BlockStatus Status { get; set; } = BlockStatus.Blocked;

    /// <summary>Local date of the most recent session start (used for the per-day limit).</summary>
    public DateOnly? LastSessionDate { get; set; }

    /// <summary>How many sessions have been started on <see cref="LastSessionDate"/>.</summary>
    public int SessionsToday { get; set; }

    /// <summary>When the current unlocked session ends (UTC). Null while blocked.</summary>
    public DateTime? SessionEndUtc { get; set; }

    /// <summary>True if another session may still be granted today, given the configured per-day cap.</summary>
    public bool IsSessionAvailable(int sessionsPerDay, DateOnly today)
    {
        if (LastSessionDate != today) return true; // new day resets the budget
        return SessionsToday < sessionsPerDay;
    }
}
