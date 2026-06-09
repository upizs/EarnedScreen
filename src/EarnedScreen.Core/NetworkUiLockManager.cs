using Microsoft.Win32;

namespace EarnedScreen.Core;

/// <summary>
/// Hides/locks the Windows network-settings UI so the user can't change DNS by hand:
///  - SettingsPageVisibility (HKLM) hides the modern Settings "Network &amp; Internet" pages.
///  - NC_LanChangeProperties / NC_LanProperties = 0 disable the TCP/IPv4 Properties dialog in the
///    legacy ncpa.cpl (where DNS is normally edited).
/// These are User-Configuration policies, so they're written to HKLM (best-effort) and to every loaded
/// real-user hive under HKEY_USERS. Fully reversible via <see cref="Unlock"/> (called on uninstall).
/// Requires admin/SYSTEM.
/// </summary>
public sealed class NetworkUiLockManager
{
    private const string ExplorerPolicyKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer";
    private const string SettingsValue = "SettingsPageVisibility";
    private const string NetConnSubKey = @"Software\Policies\Microsoft\Windows\Network Connections";

    private const string HideNetworkPages =
        "hide:network;network-proxy;network-ethernet;network-wifi;network-status;network-cellular;network-dialup;network-vpn;network-mobilehotspot";

    public void Lock()
    {
        TrySet(() =>
        {
            using var k = Registry.LocalMachine.CreateSubKey(ExplorerPolicyKey, writable: true);
            k?.SetValue(SettingsValue, HideNetworkPages, RegistryValueKind.String);
        });

        // 0 = prohibit the LAN properties UI (per the Network Connections policy convention).
        SetNcPolicies(0);
    }

    public void Unlock()
    {
        TrySet(() =>
        {
            using var k = Registry.LocalMachine.OpenSubKey(ExplorerPolicyKey, writable: true);
            if (k?.GetValue(SettingsValue) is not null)
                k.DeleteValue(SettingsValue, throwOnMissingValue: false);
        });

        RemoveNcPolicies();
    }

    private void SetNcPolicies(int value)
    {
        WriteNc(Registry.LocalMachine, value);
        foreach (var sid in LoadedUserSids())
            TrySet(() =>
            {
                using var root = Registry.Users.OpenSubKey(sid, writable: true);
                if (root is null) return;
                using var nc = root.CreateSubKey(NetConnSubKey, writable: true);
                nc?.SetValue("NC_LanChangeProperties", value, RegistryValueKind.DWord);
                nc?.SetValue("NC_LanProperties", value, RegistryValueKind.DWord);
            });
    }

    private static void WriteNc(RegistryKey baseKey, int value)
        => TrySet(() =>
        {
            using var nc = baseKey.CreateSubKey(NetConnSubKey, writable: true);
            nc?.SetValue("NC_LanChangeProperties", value, RegistryValueKind.DWord);
            nc?.SetValue("NC_LanProperties", value, RegistryValueKind.DWord);
        });

    private void RemoveNcPolicies()
    {
        TrySet(() =>
        {
            using var nc = Registry.LocalMachine.OpenSubKey(NetConnSubKey, writable: true);
            RemoveNcValues(nc);
        });

        foreach (var sid in LoadedUserSids())
            TrySet(() =>
            {
                using var root = Registry.Users.OpenSubKey(sid, writable: true);
                using var nc = root?.OpenSubKey(NetConnSubKey, writable: true);
                RemoveNcValues(nc);
            });
    }

    private static void RemoveNcValues(RegistryKey? nc)
    {
        if (nc is null) return;
        if (nc.GetValue("NC_LanChangeProperties") is not null) nc.DeleteValue("NC_LanChangeProperties", false);
        if (nc.GetValue("NC_LanProperties") is not null) nc.DeleteValue("NC_LanProperties", false);
    }

    // Real interactive user hives look like S-1-5-21-...; skip service SIDs and the _Classes hives.
    private static IEnumerable<string> LoadedUserSids()
    {
        string[] names;
        try { names = Registry.Users.GetSubKeyNames(); }
        catch { return Array.Empty<string>(); }

        return names.Where(n =>
            n.StartsWith("S-1-5-21-", StringComparison.Ordinal) &&
            !n.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase));
    }

    private static void TrySet(Action action)
    {
        try { action(); } catch { /* unelevated or restricted hive — skip */ }
    }
}
