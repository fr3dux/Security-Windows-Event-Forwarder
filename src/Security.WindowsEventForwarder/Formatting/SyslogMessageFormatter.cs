using System.Text.Json;
using System.Globalization;
using Security.WindowsEventForwarder.Configuration;
using Security.WindowsEventForwarder.Models;
using Microsoft.Extensions.Options;

namespace Security.WindowsEventForwarder.Formatting;

public sealed class SyslogMessageFormatter(
    IOptions<AgentOptions> options,
    ISourceAddressResolver sourceAddressResolver)
{
    private readonly AgentOptions _options = options.Value;

    public async Task<string> FormatAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
        => await FormatAsync(securityEvent, _options.Tag, cancellationToken);

    public async Task<string> FormatAsync(
        SecurityEvent securityEvent,
        string tag,
        CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object?>
        {
            ["EventTime"] = securityEvent.EventTime,
            ["Hostname"] = securityEvent.Hostname,
            ["EventID"] = securityEvent.EventId,
            ["RecordId"] = securityEvent.RecordId,
            ["Provider"] = securityEvent.Provider
        };

        foreach (var pair in securityEvent.Data)
            body[pair.Key] = pair.Value;

        var json = JsonSerializer.Serialize(body);
        var sourceAddress = await sourceAddressResolver.ResolveAsync(securityEvent, cancellationToken);
        var message = $"{tag} src=\"{sourceAddress}\" message={json}";
        var timestamp = securityEvent.EventTime.LocalDateTime.ToString("MMM dd HH:mm:ss", CultureInfo.InvariantCulture);
        return $"<134>{timestamp} {securityEvent.Hostname} {message}";
    }
}
