using Darktrace.WindowsEventForwarder.Models;

namespace Darktrace.WindowsEventForwarder.Events;

public interface IWindowsEventSource : IAsyncDisposable
{
    IAsyncEnumerable<SecurityEvent> ReadAllAsync(CancellationToken cancellationToken);
}
