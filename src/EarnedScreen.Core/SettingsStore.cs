using System.Text.Json;

namespace EarnedScreen.Core;

/// <summary>Loads/creates the private settings file. Auto-writes defaults if none exists.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public Settings Load()
    {
        EarnedScreenPaths.EnsureDataDir();

        if (!File.Exists(EarnedScreenPaths.SettingsFile))
        {
            var def = Settings.CreateDefault();
            Save(def);
            return def;
        }

        try
        {
            var json = File.ReadAllText(EarnedScreenPaths.SettingsFile);
            return JsonSerializer.Deserialize<Settings>(json, JsonOptions) ?? Settings.CreateDefault();
        }
        catch
        {
            // Corrupt file: run on safe defaults but do NOT clobber the user's file automatically.
            return Settings.CreateDefault();
        }
    }

    public void Save(Settings settings)
    {
        EarnedScreenPaths.EnsureDataDir();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(EarnedScreenPaths.SettingsFile, json);
    }
}
