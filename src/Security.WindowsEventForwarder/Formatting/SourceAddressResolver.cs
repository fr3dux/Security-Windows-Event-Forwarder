using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Security.WindowsEventForwarder.Configuration;
using Security.WindowsEventForwarder.Models;
using Microsoft.Extensions.Options;

namespace Security.WindowsEventForwarder.Formatting;

public sealed class SourceAddressResolver(
    IOptions<AgentOptions> options,
    ILogger<SourceAddressResolver> logger) : ISourceAddressResolver
{
    private readonly AgentOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> ResolveAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        if (!_options.SourceAddressMode.Equals("ResolveEventComputer", StringComparison.OrdinalIgnoreCase))
            return _options.SourceAddress;

        if (_options.SourceAddressOverrides.TryGetValue(securityEvent.Hostname, out var overridden))
            return overridden;
        if (_cache.TryGetValue(securityEvent.Hostname, out var cached))
            return cached;

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(securityEvent.Hostname, cancellationToken);
            var address = addresses.FirstOrDefault(candidate =>
                candidate.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(candidate));
            if (address is not null)
            {
                var resolved = address.ToString();
                _cache[securityEvent.Hostname] = resolved;
                return resolved;
            }
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException)
        {
            logger.LogWarning(exception, "Could not resolve source computer {Hostname}; using fallback {SourceAddress}.",
                securityEvent.Hostname, _options.SourceAddress);
        }

        return _options.SourceAddress;
    }
}
