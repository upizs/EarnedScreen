using EarnedScreen.Core;

namespace EarnedScreen.Tests;

public sealed class SessionStateTests
{
    private static readonly DateOnly Today = new(2026, 6, 8);
    private static readonly DateOnly Yesterday = new(2026, 6, 7);

    [Fact]
    public void Fresh_state_has_session_available()
    {
        var state = new SessionState();
        Assert.True(state.IsSessionAvailable(sessionsPerDay: 1, Today));
    }

    [Fact]
    public void Used_today_blocks_further_sessions_when_cap_is_one()
    {
        var state = new SessionState { LastSessionDate = Today, SessionsToday = 1 };
        Assert.False(state.IsSessionAvailable(sessionsPerDay: 1, Today));
    }

    [Fact]
    public void New_day_resets_the_budget()
    {
        var state = new SessionState { LastSessionDate = Yesterday, SessionsToday = 1 };
        Assert.True(state.IsSessionAvailable(sessionsPerDay: 1, Today));
    }

    [Fact]
    public void Higher_cap_allows_more_sessions_same_day()
    {
        var state = new SessionState { LastSessionDate = Today, SessionsToday = 1 };
        Assert.True(state.IsSessionAvailable(sessionsPerDay: 3, Today));

        state.SessionsToday = 3;
        Assert.False(state.IsSessionAvailable(sessionsPerDay: 3, Today));
    }
}
