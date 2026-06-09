using System.Diagnostics;
using System.Text;

namespace EarnedScreen.Core;

/// <summary>
/// Pins the active network adapter's DNS to a CleanBrowsing filter (default Family), so the whole
/// machine gets family-safe resolution. Uses PowerShell's Dns/NetAdapter cmdlets, which target the
/// adapter by InterfaceIndex — so it never touches disconnected, virtual, or Bluetooth adapters.
/// Requires admin/SYSTEM; only the service calls the mutating methods.
/// </summary>
public sealed class DnsFilterManager
{
    /// <summary>Maps a named CleanBrowsing filter to its (IPv4, IPv6) servers.</summary>
    public static (List<string> V4, List<string> V6) ResolveServers(string filter) => filter?.Trim().ToLowerInvariant() switch
    {
        "adult" => (new() { "185.228.168.10", "185.228.169.11" },
                    new() { "2a0d:2a00:1::1", "2a0d:2a00:2::1" }),
        "security" => (new() { "185.228.168.9", "185.228.169.9" },
                       new() { "2a0d:2a00:1::2", "2a0d:2a00:2::2" }),
        // "family" and anything unknown fall back to the strongest (Family) filter.
        _ => (new() { "185.228.168.168", "185.228.169.168" },
              new() { "2a0d:2a00:1::", "2a0d:2a00:2::" }),
    };

    /// <summary>Interface indexes of adapters that are Up and actually routing internet (have a gateway).</summary>
    public IReadOnlyList<int> GetActiveInterfaceIndexes()
    {
        const string script =
            "(Get-NetIPConfiguration | Where-Object { $_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up' }).InterfaceIndex";
        var output = RunPowerShell(script);
        var indexes = new List<int>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(line, out var idx))
                indexes.Add(idx);
        return indexes;
    }

    /// <summary>Sets the configured DNS servers on every active adapter, then flushes the cache.</summary>
    public void Apply(DnsFilterSettings cfg)
    {
        var v4 = cfg.Servers ?? new List<string>();
        var v6 = cfg.ServersV6 ?? new List<string>();
        var servers = v4.Concat(v6).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (servers.Count == 0) return;

        var quoted = string.Join(",", servers.Select(s => $"'{s}'"));
        foreach (var idx in GetActiveInterfaceIndexes())
            RunPowerShell($"Set-DnsClientServerAddress -InterfaceIndex {idx} -ServerAddresses {quoted}");

        FlushDns();
    }

    /// <summary>Reverts every active adapter to automatic (DHCP) DNS, then flushes. Used on uninstall.</summary>
    public void Clear()
    {
        foreach (var idx in GetActiveInterfaceIndexes())
            RunPowerShell($"Set-DnsClientServerAddress -InterfaceIndex {idx} -ResetServerAddresses");

        FlushDns();
    }

    private static void FlushDns()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("ipconfig", "/flushdns")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            p?.WaitForExit(5000);
        }
        catch { /* non-fatal */ }
    }

    private static string RunPowerShell(string script)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("powershell.exe",
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p is null) return "";
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(15000);
            return stdout;
        }
        catch
        {
            return "";
        }
    }
}
