using System.Diagnostics;
using System.Text;

namespace EarnedScreen.Core;

/// <summary>
/// Owns a clearly-delimited block inside the Windows hosts file. All operations are idempotent:
/// applying the block always replaces any previous EarnedScreen section, never duplicates it.
/// Writing requires admin/SYSTEM rights, which is why only the service calls the mutating methods.
/// </summary>
public sealed class HostsFileManager
{
    public const string BlockStart = "# === EarnedScreen BLOCK START ===";
    public const string BlockEnd = "# === EarnedScreen BLOCK END ===";

    private readonly string _hostsPath;

    public HostsFileManager(string? hostsPath = null)
        => _hostsPath = hostsPath ?? EarnedScreenPaths.HostsFile;

    public bool IsBlocked()
    {
        if (!File.Exists(_hostsPath)) return false;
        foreach (var line in File.ReadLines(_hostsPath))
            if (line.Trim() == BlockStart) return true;
        return false;
    }

    /// <summary>Ensures the block section exists with exactly the supplied domains.</summary>
    public void ApplyBlock(IEnumerable<string> domains)
    {
        var lines = ReadLinesWithoutSection();
        lines.Add(BlockStart);
        foreach (var raw in domains)
        {
            var domain = raw.Trim();
            if (domain.Length == 0) continue;
            // Sinkhole BOTH families: 0.0.0.0 alone leaves AAAA (IPv6) records resolvable, so sites
            // with IPv6 (e.g. YouTube) would still load. ":: " sinkholes the IPv6 lookup too.
            lines.Add($"0.0.0.0 {domain}");
            lines.Add($":: {domain}");
        }
        lines.Add(BlockEnd);
        Write(lines);
    }

    /// <summary>Removes the EarnedScreen section, leaving the rest of the hosts file untouched.</summary>
    public void RemoveBlock()
    {
        if (!File.Exists(_hostsPath)) return;
        Write(ReadLinesWithoutSection());
    }

    private List<string> ReadLinesWithoutSection()
    {
        var result = new List<string>();
        if (!File.Exists(_hostsPath)) return result;

        var inSection = false;
        foreach (var line in File.ReadAllLines(_hostsPath))
        {
            var trimmed = line.Trim();
            if (trimmed == BlockStart) { inSection = true; continue; }
            if (trimmed == BlockEnd) { inSection = false; continue; }
            if (!inSection) result.Add(line);
        }

        // Drop trailing blank lines so repeated apply/remove cycles don't grow the file.
        while (result.Count > 0 && result[^1].Trim().Length == 0)
            result.RemoveAt(result.Count - 1);

        return result;
    }

    private void Write(List<string> lines)
    {
        var content = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        // hosts must stay ASCII/UTF-8 without BOM, or Windows ignores it.
        File.WriteAllText(_hostsPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void FlushDns()
    {
        try
        {
            var psi = new ProcessStartInfo("ipconfig", "/flushdns")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
        }
        catch
        {
            // A failed flush isn't fatal: cached entries expire on their own; the hosts change still stands.
        }
    }
}
