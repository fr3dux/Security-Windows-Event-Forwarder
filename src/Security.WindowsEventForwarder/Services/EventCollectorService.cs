using Security.WindowsEventForwarder.Events;
using Security.WindowsEventForwarder.Correlation;
using Security.WindowsEventForwarder.Formatting;
using Security.WindowsEventForwarder.Models;
using Security.WindowsEventForwarder.Queue;

namespace Security.WindowsEventForwarder.Services;

public sealed class EventCollectorService(
    IWindowsEventSource source,
    AccountPrivilegeCorrelationEngine correlationEngine,
    SyslogMessageFormatter formatter,
    IMessageQueue queue,
    BookmarkStore bookmarkStore,
    ILogger<EventCollectorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var securityEvent in source.ReadAllAsync(stoppingToken))
        {
            var telemetryEvents = await correlationEngine.ProcessAsync(securityEvent, stoppingToken);
            foreach (var telemetryEvent in telemetryEvents)
            {
                var id = $"{securityEvent.EventTime.UtcTicks:D19}-{securityEvent.RecordId ?? 0:D19}-{Guid.NewGuid():N}";
                var payload = await formatter.FormatAsync(telemetryEvent.Event, telemetryEvent.Tag, stoppingToken);
                var queued = new QueuedMessage(id, DateTimeOffset.UtcNow, securityEvent.RecordId, payload);
                await queue.EnqueueAsync(queued, stoppingToken);
                logger.LogInformation(
                    "Queued Windows event {EventId}/{RecordId} as {TelemetryTag}.",
                    securityEvent.EventId,
                    securityEvent.RecordId,
                    telemetryEvent.Tag);
            }
            await bookmarkStore.SaveAsync(securityEvent.Channel, securityEvent.BookmarkXml, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await source.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
