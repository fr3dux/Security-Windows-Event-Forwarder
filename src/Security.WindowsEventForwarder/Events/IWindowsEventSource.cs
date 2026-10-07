using Security.WindowsEventForwarder.Models;

namespace Security.WindowsEventForwarder.Events;

public interface IWindowsEventSource : IAsyncDisposable
{
    IAsyncEnumerable<SecurityEvent> ReadAllAsync(CancellationToken cancellationToken);
}
