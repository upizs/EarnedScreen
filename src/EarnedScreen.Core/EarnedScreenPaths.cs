namespace EarnedScreen.Core;

/// <summary>
/// Central location for every path EarnedScreen touches. Private data lives in
/// %ProgramData%\EarnedScreen so the SYSTEM service and the user app can both reach it,
/// and so it never ends up inside the git repo.
/// </summary>
public static class EarnedScreenPaths
{
    /// <summary>
    /// C:\ProgramData\EarnedScreen by default. Override with the EARNEDSCREEN_DATA env var
    /// (used for tests / running the service unelevated against a sandbox).
    /// </summary>
    public static string DataDir { get; } =
        Environment.GetEnvironmentVariable("EARNEDSCREEN_DATA") is { Length: > 0 } dataOverride
            ? dataOverride
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EarnedScreen");

    public static string SettingsFile => Path.Combine(DataDir, "settings.json");

    public static string StateFile => Path.Combine(DataDir, "state.json");

    /// <summary>
    /// C:\Windows\System32\drivers\etc\hosts by default. Override with the EARNEDSCREEN_HOSTS env var
    /// (used for tests / running the service unelevated against a sandbox hosts file).
    /// </summary>
    public static string HostsFile { get; } =
        Environment.GetEnvironmentVariable("EARNEDSCREEN_HOSTS") is { Length: > 0 } hostsOverride
            ? hostsOverride
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

    public static void EnsureDataDir() => Directory.CreateDirectory(DataDir);
}
