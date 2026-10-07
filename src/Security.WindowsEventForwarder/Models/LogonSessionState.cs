namespace Security.WindowsEventForwarder.Models;

public sealed record LogonSessionState(
    string SessionKey,
    string LogonId,
    DateTimeOffset LogonAt,
    string Hostname,
    string? AccountSid,
    string? AccountName,
    string? AccountDomain,
    string? LogonType,
    string? IpAddress);
