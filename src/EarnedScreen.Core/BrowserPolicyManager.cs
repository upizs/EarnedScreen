using Microsoft.Win32;

namespace EarnedScreen.Core;

/// <summary>
/// Disables DNS-over-HTTPS (DoH) in Chrome and Edge via Windows policy registry keys.
/// Browsers with DoH enabled bypass the OS hosts file entirely, making hosts-file blocking
/// ineffective. Setting the policy to "off" forces them back through the system resolver.
/// Requires SYSTEM/admin rights — only the service should call this.
/// </summary>
public sealed class BrowserPolicyManager
{
    // HKLM policies apply to all users and can't be overridden by per-user settings.
    private static readonly (string KeyPath, string ValueName)[] PolicyKeys = new[]
    {
        (@"SOFTWARE\Policies\Google\Chrome",          "DnsOverHttpsMode"),
        (@"SOFTWARE\Policies\Microsoft\Edge",         "DnsOverHttpsMode"),
        (@"SOFTWARE\Policies\Mozilla\Firefox",        "DNSOverHTTPS"),   // Firefox uses a sub-key, handled below
    };

    /// <summary>Disables DoH in Chrome and Edge. Safe to call repeatedly.</summary>
    public void DisableDoH()
    {
        try
        {
            // Chrome + Edge: REG_SZ "DnsOverHttpsMode" = "off"
            foreach (var (keyPath, valueName) in PolicyKeys.Take(2))
            {
                using var key = Registry.LocalMachine.CreateSubKey(keyPath, writable: true);
                key?.SetValue(valueName, "off", RegistryValueKind.String);
            }

            // Firefox: HKLM\SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS → Enabled = 0
            using var ff = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS", writable: true);
            ff?.SetValue("Enabled", 0, RegistryValueKind.DWord);
        }
        catch
        {
            // Running unelevated (e.g. smoke test) — skip silently. The hosts block still works
            // if the browser happens not to use DoH.
        }
    }

    /// <summary>Removes the policy keys written by <see cref="DisableDoH"/>.</summary>
    public void RestoreDoH()
    {
        try
        {
            foreach (var (keyPath, valueName) in PolicyKeys.Take(2))
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
                if (key?.GetValue(valueName) is not null)
                    key.DeleteValue(valueName, throwOnMissingValue: false);
            }

            using var ff = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS", writable: true);
            ff?.DeleteValue("Enabled", throwOnMissingValue: false);
        }
        catch { }
    }
}
