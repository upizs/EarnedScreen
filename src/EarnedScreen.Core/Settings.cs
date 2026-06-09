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

    /// <summary>Optional Notion integration: pulls today's open tasks into the gateway checklist.</summary>
    public NotionSettings Notion { get; set; } = new();

    /// <summary>Always-on family-safe DNS + network-settings lock.</summary>
    public DnsFilterSettings DnsFilter { get; set; } = new();

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
            "max.com", "www.max.com", "play.max.com",
            "hbomax.com", "www.hbomax.com", "play.hbomax.com",
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
            "20 sit-ups",
            "20 squats",
            "20 jumping jacks",
            "Drink a glass of water",
            "Stand up & stretch",
        },
        Notion = new(),
        DnsFilter = new(),
    };
}

/// <summary>
/// Always-on family-safe DNS. While the service runs, the active adapter's DNS is pinned to a
/// CleanBrowsing filter and (optionally) the Windows network-settings UI is locked so it can't be
/// changed. There is no disable path — only uninstall reverts it.
/// </summary>
public sealed class DnsFilterSettings
{
    /// <summary>Master switch for the whole module.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Named CleanBrowsing filter: Family | Adult | Security | Custom.</summary>
    public string Filter { get; set; } = "Family";

    /// <summary>IPv4 DNS servers. Defaults to CleanBrowsing Family.</summary>
    public List<string> Servers { get; set; } = new() { "185.228.168.168", "185.228.169.168" };

    /// <summary>IPv6 DNS servers. Defaults to CleanBrowsing Family.</summary>
    public List<string> ServersV6 { get; set; } = new() { "2a0d:2a00:1::", "2a0d:2a00:2::" };

    /// <summary>Hide/lock the Windows network-settings UI so DNS can't be changed by hand.</summary>
    public bool LockNetworkUi { get; set; } = true;
}

/// <summary>
/// Notion integration config. Disabled by default — Claude Code fills in the token + database id
/// after the user creates an internal integration and shares the My Tasks database with it.
/// Lives in the private settings.json, never committed.
/// </summary>
public sealed class NotionSettings
{
    /// <summary>Master switch. When false, the gateway shows only the static checklist.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Notion internal integration secret (starts with "secret_" / "ntn_").</summary>
    public string Token { get; set; } = "";

    /// <summary>The "My Tasks" database id.</summary>
    public string TasksDatabaseId { get; set; } = "";

    /// <summary>Title property name (the Notion Tasks template uses "Task name").</summary>
    public string TitleProperty { get; set; } = "Task name";

    /// <summary>Date property used to find "today's" tasks.</summary>
    public string DueProperty { get; set; } = "Due";

    /// <summary>Status property used to tell open tasks from completed ones.</summary>
    public string StatusProperty { get; set; } = "Status";
}
