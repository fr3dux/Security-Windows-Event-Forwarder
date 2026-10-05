using Darktrace.WindowsEventForwarder.Models;

namespace Darktrace.WindowsEventForwarder.Queue;

public interface IMessageQueue
{
    Task EnqueueAsync(QueuedMessage message, CancellationToken cancellationToken);
    Task<QueuedMessage?> PeekAsync(CancellationToken cancellationToken);
    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
