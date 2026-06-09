using EarnedScreen.Core;

namespace EarnedScreen.Tests;

public sealed class HostsFileManagerTests : IDisposable
{
    private readonly string _hostsPath = Path.Combine(Path.GetTempPath(), $"earnedscreen-hosts-{Guid.NewGuid():N}");
    private readonly HostsFileManager _sut;

    public HostsFileManagerTests()
    {
        File.WriteAllText(_hostsPath, "127.0.0.1 localhost\r\n# existing entry\r\n");
        _sut = new HostsFileManager(_hostsPath);
    }

    public void Dispose()
    {
        if (File.Exists(_hostsPath)) File.Delete(_hostsPath);
    }

    [Fact]
    public void ApplyBlock_adds_section_with_domains_and_marks_blocked()
    {
        _sut.ApplyBlock(new[] { "netflix.com", "youtube.com" });

        Assert.True(_sut.IsBlocked());
        var text = File.ReadAllText(_hostsPath);
        Assert.Contains(HostsFileManager.BlockStart, text);
        Assert.Contains("0.0.0.0 netflix.com", text);
        Assert.Contains("0.0.0.0 youtube.com", text);
        // IPv6 must be sinkholed too, or sites with AAAA records still load.
        Assert.Contains(":: netflix.com", text);
        Assert.Contains(":: youtube.com", text);
        Assert.Contains(HostsFileManager.BlockEnd, text);
    }

    [Fact]
    public void ApplyBlock_preserves_existing_entries()
    {
        _sut.ApplyBlock(new[] { "netflix.com" });

        var text = File.ReadAllText(_hostsPath);
        Assert.Contains("127.0.0.1 localhost", text);
        Assert.Contains("# existing entry", text);
    }

    [Fact]
    public void ApplyBlock_is_idempotent_no_duplicate_sections()
    {
        _sut.ApplyBlock(new[] { "netflix.com" });
        _sut.ApplyBlock(new[] { "netflix.com", "hulu.com" });

        var text = File.ReadAllText(_hostsPath);
        Assert.Equal(1, CountOccurrences(text, HostsFileManager.BlockStart));
        Assert.Equal(1, CountOccurrences(text, "0.0.0.0 netflix.com"));
        Assert.Contains("0.0.0.0 hulu.com", text);
    }

    [Fact]
    public void RemoveBlock_clears_section_but_keeps_user_content()
    {
        _sut.ApplyBlock(new[] { "netflix.com" });
        _sut.RemoveBlock();

        Assert.False(_sut.IsBlocked());
        var text = File.ReadAllText(_hostsPath);
        Assert.DoesNotContain(HostsFileManager.BlockStart, text);
        Assert.DoesNotContain("0.0.0.0 netflix.com", text);
        Assert.Contains("127.0.0.1 localhost", text);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
        {
            count++;
            i += needle.Length;
        }
        return count;
    }
}
