using System.Text.Json;

namespace EarnedScreen.Core;

/// <summary>Loads/saves the persisted <see cref="SessionState"/>.</summary>
public sealed class StateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SessionState Load()
    {
        EarnedScreenPaths.EnsureDataDir();

        if (!File.Exists(EarnedScreenPaths.StateFile))
            return new SessionState();

        try
        {
            var json = File.ReadAllText(EarnedScreenPaths.StateFile);
            return JsonSerializer.Deserialize<SessionState>(json, JsonOptions) ?? new SessionState();
        }
        catch
        {
            return new SessionState();
        }
    }

    public void Save(SessionState state)
    {
        EarnedScreenPaths.EnsureDataDir();
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(EarnedScreenPaths.StateFile, json);
    }
}
