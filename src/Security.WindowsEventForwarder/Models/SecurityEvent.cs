namespace Security.WindowsEventForwarder.Models;

public sealed record SecurityEvent(
    string Channel,
    DateTimeOffset EventTime,
    string Hostname,
    int EventId,
    long? RecordId,
    string Provider,
    IReadOnlyDictionary<string, string?> Data,
    string BookmarkXml);
