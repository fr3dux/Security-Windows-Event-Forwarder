namespace Darktrace.WindowsEventForwarder.Models;

public sealed record AccountCorrelationState(
    string AccountSid,
    string? AccountName,
    string? AccountDomain,
    DateTimeOffset CreatedAt,
    string CreatedOnHost,
    string? CreatedBy,
    DateTimeOffset? InitialLogonAt = null,
    string? InitialLogonHost = null,
    string? InitialLogonAccountSid = null,
    string? InitialLogonAccountName = null,
    string? InitialLogonType = null,
    string? InitialLogonIpAddress = null,
    DateTimeOffset? PrivilegedAt = null,
    string? PrivilegedOnHost = null,
    string? PrivilegedGroupSid = null,
    string? PrivilegedGroupName = null,
    string? PrivilegedBy = null);
