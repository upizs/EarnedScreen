namespace EarnedScreen.Core;

/// <summary>Named pipes the service exposes. The app is the client on both.</summary>
public static class PipeNames
{
    /// <summary>Request/response: app asks for status or requests an unlock.</summary>
    public const string Command = "EarnedScreen.Command";

    /// <summary>Server push: the service notifies the app when the Guillotine drops.</summary>
    public const string Events = "EarnedScreen.Events";
}

public enum CommandType
{
    GetStatus,
    RequestUnlock,
}

public sealed class CommandRequest
{
    public CommandType Command { get; set; }
}

public sealed class StatusResponse
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    public BlockStatus Status { get; set; }
    public bool SessionAvailableToday { get; set; }
    public int SessionMinutes { get; set; }
    public DateTime? SessionEndUtc { get; set; }
    public int RemainingSeconds { get; set; }
    public bool DnsFilterActive { get; set; }
    public string? DnsFilterName { get; set; }
}

public enum EventType
{
    SessionEnded,
}

public sealed class EventMessage
{
    public EventType Type { get; set; }
}
