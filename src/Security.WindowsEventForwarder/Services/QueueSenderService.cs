using Security.WindowsEventForwarder.Configuration;
using Security.WindowsEventForwarder.Queue;
using Security.WindowsEventForwarder.Transport;
using Microsoft.Extensions.Options;

namespace Security.WindowsEventForwarder.Services;

public sealed class QueueSenderService(
    IMessageQueue queue,
    ISyslogSender sender,
    IOptions<StorageOptions> options,
    ILogger<QueueSenderService> logger) : BackgroundService
{
    private readonly StorageOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retrySeconds = Math.Max(1, _options.RetryInitialSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await queue.PeekAsync(stoppingToken);
            if (message is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                continue;
            }

            try
            {
                await sender.SendAsync(message.Payload, stoppingToken);
                await queue.DeleteAsync(message.Id, stoppingToken);
                logger.LogInformation("Sent event record {EventRecordId} to the configured destination.", message.EventRecordId);
                retrySeconds = Math.Max(1, _options.RetryInitialSeconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not send event record {EventRecordId}; retrying in {RetrySeconds}s.",
                    message.EventRecordId, retrySeconds);
                await Task.Delay(TimeSpan.FromSeconds(retrySeconds), stoppingToken);
                retrySeconds = Math.Min(retrySeconds * 2, Math.Max(1, _options.RetryMaximumSeconds));
            }
        }
    }
}
