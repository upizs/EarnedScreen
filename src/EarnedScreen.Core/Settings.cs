namespace EarnedScreen.Core;

/// <summary>
/// The private, Claude-Code-only configuration. Lives at %ProgramData%\EarnedScreen\settings.json.
/// Never committed to git. The defaults below are written out the first time the service runs.
/// </summary>
public sealed class Settings
{
    /// <summary>How long a single earned session lasts, in minutes.</summary>
    public int SessionMinutes { get; set; } = 60;

    /// <summary>How many sessions the user is allowed per day.</summary>
    public int SessionsPerDay { get; set; } = 1;

    /// <summary>Domains hard-blocked in the hosts file while locked.</summary>
    public List<string> BlockedDomains { get; set; } = new();

    /// <summary>The pre-watch "toll": completed before a session is granted.</summary>
    public List<string> GatewayChecklist { get; set; } = new();

    /// <summary>The post-session "anti-potato" list shown when the Guillotine drops.</summary>
    public List<string> CoolDownChecklist { get; set; } = new();

    public static Settings CreateDefault() => new()
    {
        SessionMinutes = 60,
        SessionsPerDay = 1,
        BlockedDomains = new()
        {
            "netflix.com", "www.netflix.com",
            "youtube.com", "www.youtube.com", "m.youtube.com", "youtu.be", "youtube-nocookie.com",
            "hulu.com", "www.hulu.com",
            "disneyplus.com", "www.disneyplus.com",
            "twitch.tv", "www.twitch.tv",
            "primevideo.com", "www.primevideo.com",
            "max.com", "www.max.com", "hbomax.com", "www.hbomax.com",
        },
        GatewayChecklist = new()
        {
            "40 push-ups",
            "40 sit-ups",
            "40 squats",
            "40 jumping jacks",
            "Wash the dishes",
            "Tidy up surfaces",
            "Sort the laundry",
        },
        CoolDownChecklist = new()
        {
            "20 push-ups",
            "20 squats",
            "Drink a glass of water",
            "Stand up & stretch",
        },
    };
}
