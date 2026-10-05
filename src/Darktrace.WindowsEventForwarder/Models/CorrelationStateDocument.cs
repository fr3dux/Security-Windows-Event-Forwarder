namespace Darktrace.WindowsEventForwarder.Models;

public sealed class CorrelationStateDocument
{
    public Dictionary<string, AccountCorrelationState> Accounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, LogonSessionState> LogonSessions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
