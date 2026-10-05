using Darktrace.WindowsEventForwarder.Events;
using Darktrace.WindowsEventForwarder.Formatting;
using Darktrace.WindowsEventForwarder.Models;
using Darktrace.WindowsEventForwarder.Queue;

namespace Darktrace.WindowsEventForwarder.Services;

public sealed class EventCollectorService(
    IWindowsEventSource source,
    DarktraceMessageFormatter formatter,
    IMessageQueue queue,
    BookmarkStore bookmarkStore,
    ILogger<EventCollectorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var securityEvent in source.ReadAllAsync(stoppingToken))
        {
            var id = $"{securityEvent.EventTime.UtcTicks:D19}-{securityEvent.RecordId ?? 0:D19}-{Guid.NewGuid():N}";
            var payload = await formatter.FormatAsync(securityEvent, stoppingToken);
            var queued = new QueuedMessage(id, DateTimeOffset.UtcNow, securityEvent.RecordId, payload);

            await queue.EnqueueAsync(queued, stoppingToken);
            await bookmarkStore.SaveAsync(securityEvent.Channel, securityEvent.BookmarkXml, stoppingToken);
            logger.LogInformation("Queued Windows event {EventId}/{RecordId}.", securityEvent.EventId, securityEvent.RecordId);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await source.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
