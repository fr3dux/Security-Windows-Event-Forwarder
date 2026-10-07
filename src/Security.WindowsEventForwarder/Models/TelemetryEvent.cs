namespace Security.WindowsEventForwarder.Models;

public sealed record TelemetryEvent(SecurityEvent Event, string Tag);
