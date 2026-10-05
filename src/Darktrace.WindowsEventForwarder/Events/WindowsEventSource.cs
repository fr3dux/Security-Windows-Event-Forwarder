using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using System.Threading.Channels;
using Darktrace.WindowsEventForwarder.Configuration;
using Darktrace.WindowsEventForwarder.Models;
using Microsoft.Extensions.Options;

namespace Darktrace.WindowsEventForwarder.Events;

[SupportedOSPlatform("windows")]
public sealed class WindowsEventSource : IWindowsEventSource
{
    private readonly AgentOptions _options;
    private readonly BookmarkStore _bookmarkStore;
    private readonly ILogger<WindowsEventSource> _logger;
    private readonly Channel<SecurityEvent> _channel = Channel.CreateUnbounded<SecurityEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private EventLogWatcher? _watcher;

    public WindowsEventSource(
        IOptions<AgentOptions> options,
        BookmarkStore bookmarkStore,
        ILogger<WindowsEventSource> logger)
    {
        _options = options.Value;
        _bookmarkStore = bookmarkStore;
        _logger = logger;
    }

    public async IAsyncEnumerable<SecurityEvent> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows Security Event Log is available only on Windows.");

        var bookmarkXml = await _bookmarkStore.LoadAsync(_options.Channel, cancellationToken);
        var bookmark = string.IsNullOrWhiteSpace(bookmarkXml) ? null : new EventBookmark(bookmarkXml);
        var eventIds = string.Join(" or ", _options.EventIds.Distinct().Order().Select(id => $"EventID={id}"));
        var xpath = $"*[System[({eventIds})]]";
        var query = new EventLogQuery(_options.Channel, PathType.LogName, xpath);

        _watcher = new EventLogWatcher(
            query,
            bookmark,
            bookmark is null && _options.ReadExistingEventsOnFirstStart);
        _watcher.EventRecordWritten += HandleEvent;
        _watcher.Enabled = true;
        _logger.LogInformation("Monitoring {Channel} log for event IDs: {EventIds}",
            _options.Channel, string.Join(",", _options.EventIds));

        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
            yield return item;
    }

    private void HandleEvent(object? sender, EventRecordWrittenEventArgs args)
    {
        if (args.EventException is not null)
        {
            _logger.LogError(args.EventException, "Windows Event Log subscription failed.");
            return;
        }

        using var record = args.EventRecord;
        if (record is null) return;

        try
        {
            var bookmarkXml = record.Bookmark?.BookmarkXml
                ?? throw new InvalidOperationException("Event does not contain a bookmark.");
            var parsed = EventXmlParser.Parse(record.ToXml(), bookmarkXml, _options.Channel);
            if (!_channel.Writer.TryWrite(parsed))
                _logger.LogError("Could not place event {RecordId} in the processing channel.", record.RecordId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not parse Windows event {RecordId}.", record.RecordId);
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_watcher is not null)
        {
            _watcher.Enabled = false;
            _watcher.EventRecordWritten -= HandleEvent;
            _watcher.Dispose();
        }
        _channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
