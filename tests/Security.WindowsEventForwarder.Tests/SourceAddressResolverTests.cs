using Security.WindowsEventForwarder.Configuration;
using Security.WindowsEventForwarder.Formatting;
using Security.WindowsEventForwarder.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Security.WindowsEventForwarder.Tests;

public sealed class SourceAddressResolverTests
{
    [Fact]
    public async Task UsesConfiguredAddressForDirectCollection()
    {
        var resolver = CreateResolver(new AgentOptions
        {
            SourceAddress = "10.0.0.10",
            SourceAddressMode = "Configured"
        });

        Assert.Equal("10.0.0.10", await resolver.ResolveAsync(Event("server.example.local"), CancellationToken.None));
    }

    [Fact]
    public async Task UsesHostnameOverrideBeforeDnsForForwardedEvents()
    {
        var resolver = CreateResolver(new AgentOptions
        {
            SourceAddress = "10.0.0.20",
            SourceAddressMode = "ResolveEventComputer",
            SourceAddressOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["server.example.local"] = "10.0.0.30"
            }
        });

        Assert.Equal("10.0.0.30", await resolver.ResolveAsync(Event("SERVER.EXAMPLE.LOCAL"), CancellationToken.None));
    }

    private static SourceAddressResolver CreateResolver(AgentOptions options) =>
        new(Options.Create(options), NullLogger<SourceAddressResolver>.Instance);

    private static SecurityEvent Event(string hostname) => new(
        "ForwardedEvents",
        DateTimeOffset.UtcNow,
        hostname,
        4732,
        1,
        "Microsoft-Windows-Security-Auditing",
        new Dictionary<string, string?>(),
        "<Bookmark />");
}
