using System.Text.Json;
using Darktrace.WindowsEventForwarder.Configuration;
using Darktrace.WindowsEventForwarder.Formatting;
using Darktrace.WindowsEventForwarder.Models;
using Microsoft.Extensions.Options;

namespace Darktrace.WindowsEventForwarder.Tests;

public sealed class DarktraceMessageFormatterTests
{
    [Fact]
    public async Task ProducesTheExistingDarktraceTelemetryContract()
    {
        var options = Options.Create(new AgentOptions
        {
            SourceAddress = "192.0.2.10",
            Tag = "Windows_AD_Events"
        });
        var formatter = new DarktraceMessageFormatter(options, new FixedSourceAddressResolver("192.0.2.10"));
        var securityEvent = new SecurityEvent(
            "Security",
            new DateTimeOffset(2026, 10, 3, 12, 30, 0, TimeSpan.Zero),
            "DC01.example.local",
            4732,
            4219,
            "Microsoft-Windows-Security-Auditing",
            new Dictionary<string, string?> { ["TargetUserName"] = "VPN-Users" },
            "<Bookmark />");

        var result = await formatter.FormatAsync(securityEvent, CancellationToken.None);

        Assert.Contains("Windows_AD_Events src=\"192.0.2.10\" message=", result);
        var json = result[(result.IndexOf("message=", StringComparison.Ordinal) + "message=".Length)..];
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(4732, parsed.RootElement.GetProperty("EventID").GetInt32());
        Assert.Equal("VPN-Users", parsed.RootElement.GetProperty("TargetUserName").GetString());
    }

    private sealed class FixedSourceAddressResolver(string address) : ISourceAddressResolver
    {
        public Task<string> ResolveAsync(SecurityEvent securityEvent, CancellationToken cancellationToken) =>
            Task.FromResult(address);
    }
}
