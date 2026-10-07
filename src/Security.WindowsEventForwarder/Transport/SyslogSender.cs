using System.Net.Sockets;
using System.Text;
using Security.WindowsEventForwarder.Configuration;
using Microsoft.Extensions.Options;

namespace Security.WindowsEventForwarder.Transport;

public sealed class SyslogSender(IOptions<SyslogOptions> options) : ISyslogSender
{
    private readonly SyslogOptions _options = options.Value;

    public async Task SendAsync(string payload, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(payload + "\n");
        if (_options.Protocol.Equals("Udp", StringComparison.OrdinalIgnoreCase))
        {
            using var udp = new UdpClient();
            await udp.SendAsync(bytes, _options.Host, _options.Port, cancellationToken);
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.ConnectTimeoutSeconds)));
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(_options.Host, _options.Port, timeout.Token);
        await using var stream = tcp.GetStream();
        await stream.WriteAsync(bytes, timeout.Token);
        await stream.FlushAsync(timeout.Token);
    }
}
