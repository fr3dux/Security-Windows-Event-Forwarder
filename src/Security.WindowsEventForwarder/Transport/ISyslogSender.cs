namespace Security.WindowsEventForwarder.Transport;

public interface ISyslogSender
{
    Task SendAsync(string payload, CancellationToken cancellationToken);
}
