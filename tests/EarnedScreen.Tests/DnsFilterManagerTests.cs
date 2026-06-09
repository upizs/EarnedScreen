using EarnedScreen.Core;

namespace EarnedScreen.Tests;

public sealed class DnsFilterManagerTests
{
    [Fact]
    public void Family_filter_resolves_to_family_ips()
    {
        var (v4, _) = DnsFilterManager.ResolveServers("Family");
        Assert.Equal(new[] { "185.228.168.168", "185.228.169.168" }, v4);
    }

    [Fact]
    public void Adult_filter_resolves_to_adult_ips()
    {
        var (v4, _) = DnsFilterManager.ResolveServers("Adult");
        Assert.Equal(new[] { "185.228.168.10", "185.228.169.11" }, v4);
    }

    [Fact]
    public void Security_filter_resolves_to_security_ips()
    {
        var (v4, _) = DnsFilterManager.ResolveServers("security");
        Assert.Equal(new[] { "185.228.168.9", "185.228.169.9" }, v4);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData(null)]
    public void Unknown_filter_falls_back_to_family(string? filter)
    {
        var (v4, _) = DnsFilterManager.ResolveServers(filter!);
        Assert.Equal(new[] { "185.228.168.168", "185.228.169.168" }, v4);
    }
}
