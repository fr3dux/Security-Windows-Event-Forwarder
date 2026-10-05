namespace Darktrace.WindowsEventForwarder.Models;

public sealed record QueuedMessage(
    string Id,
    DateTimeOffset CreatedAt,
    long? EventRecordId,
    string Payload);
