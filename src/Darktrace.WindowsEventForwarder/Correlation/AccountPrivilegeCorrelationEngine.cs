using Darktrace.WindowsEventForwarder.Configuration;
using Darktrace.WindowsEventForwarder.Models;
using Microsoft.Extensions.Options;

namespace Darktrace.WindowsEventForwarder.Correlation;

public sealed class AccountPrivilegeCorrelationEngine(
    IOptions<AgentOptions> agentOptions,
    IOptions<CorrelationOptions> correlationOptions,
    CorrelationStateStore stateStore,
    ILogger<AccountPrivilegeCorrelationEngine> logger)
{
    private static readonly HashSet<int> GroupAddEventIds = [4728, 4732, 4756];
    private static readonly HashSet<int> GroupChangeEventIds = [4728, 4732, 4756, 4729, 4733, 4757];
    private readonly AgentOptions _agentOptions = agentOptions.Value;
    private readonly CorrelationOptions _options = correlationOptions.Value;
    private CorrelationStateDocument? _state;

    public async Task<IReadOnlyList<TelemetryEvent>> ProcessAsync(
        SecurityEvent securityEvent,
        CancellationToken cancellationToken)
    {
        _state ??= await stateStore.LoadAsync(cancellationToken);
        var changed = PurgeExpired(DateTimeOffset.UtcNow);
        var output = new List<TelemetryEvent>();

        if (securityEvent.EventId == 4720)
        {
            TrackCreatedAccount(securityEvent);
            changed = true;
            output.Add(new TelemetryEvent(securityEvent, _agentOptions.Tag));
        }
        else if (GroupChangeEventIds.Contains(securityEvent.EventId))
        {
            output.Add(new TelemetryEvent(securityEvent, _agentOptions.Tag));
            if (_options.Enabled && GroupAddEventIds.Contains(securityEvent.EventId))
                changed |= TrackPrivilegedMembership(securityEvent);
        }
        else if (securityEvent.EventId == 4624)
        {
            if (IsAllowedLogon(securityEvent))
            {
                output.Add(new TelemetryEvent(securityEvent, _agentOptions.Tag));
                if (_options.Enabled)
                    changed |= TrackLogonSession(securityEvent);
            }
            var correlated = _options.Enabled ? CompleteOnLogon(securityEvent) : null;
            if (correlated is not null)
            {
                output.Add(new TelemetryEvent(correlated, _agentOptions.Tag));
                changed = true;
            }
        }

        if (changed)
            await stateStore.SaveAsync(_state, cancellationToken);

        return output;
    }

    private void TrackCreatedAccount(SecurityEvent securityEvent)
    {
        var sid = Get(securityEvent, "TargetSid");
        if (string.IsNullOrWhiteSpace(sid) || sid == "-")
        {
            logger.LogWarning("Event 4720/{RecordId} does not contain a usable TargetSid.", securityEvent.RecordId);
            return;
        }

        var subjectLogonId = Get(securityEvent, "SubjectLogonId");
        var initialLogon = string.IsNullOrWhiteSpace(subjectLogonId)
            ? null
            : _state!.LogonSessions.GetValueOrDefault(SessionKey(securityEvent.Hostname, subjectLogonId));
        var subjectSid = Get(securityEvent, "SubjectUserSid");
        if (initialLogon is not null
            && (initialLogon.LogonAt > securityEvent.EventTime
                || securityEvent.EventTime - initialLogon.LogonAt > CorrelationWindow
                || (!string.IsNullOrWhiteSpace(subjectSid)
                    && !string.IsNullOrWhiteSpace(initialLogon.AccountSid)
                    && !subjectSid.Equals(initialLogon.AccountSid, StringComparison.OrdinalIgnoreCase))))
            initialLogon = null;

        _state!.Accounts[sid] = new AccountCorrelationState(
            sid,
            Get(securityEvent, "TargetUserName"),
            Get(securityEvent, "TargetDomainName"),
            securityEvent.EventTime,
            securityEvent.Hostname,
            QualifiedSubject(securityEvent),
            initialLogon?.LogonAt,
            initialLogon?.Hostname,
            initialLogon?.AccountSid,
            QualifiedAccount(initialLogon),
            initialLogon?.LogonType,
            initialLogon?.IpAddress);
        logger.LogInformation("Tracking newly created account {AccountSid} for privilege correlation.", sid);
    }

    private bool TrackPrivilegedMembership(SecurityEvent securityEvent)
    {
        var accountSid = Get(securityEvent, "MemberSid");
        var groupSid = Get(securityEvent, "TargetSid");
        if (string.IsNullOrWhiteSpace(accountSid)
            || string.IsNullOrWhiteSpace(groupSid)
            || !IsPrivilegedGroup(groupSid)
            || !_state!.Accounts.TryGetValue(accountSid, out var existing)
            || securityEvent.EventTime < existing.CreatedAt
            || securityEvent.EventTime - existing.CreatedAt > CorrelationWindow)
            return false;

        _state.Accounts[accountSid] = existing with
        {
            PrivilegedAt = securityEvent.EventTime,
            PrivilegedOnHost = securityEvent.Hostname,
            PrivilegedGroupSid = groupSid,
            PrivilegedGroupName = Get(securityEvent, "TargetUserName"),
            PrivilegedBy = QualifiedSubject(securityEvent)
        };
        logger.LogWarning(
            "New account {AccountSid} was added to privileged group {GroupSid}; awaiting qualifying logon.",
            accountSid,
            groupSid);
        return true;
    }

    private SecurityEvent? CompleteOnLogon(SecurityEvent securityEvent)
    {
        var accountSid = Get(securityEvent, "TargetUserSid");
        var logonTypeText = Get(securityEvent, "LogonType");
        if (string.IsNullOrWhiteSpace(accountSid)
            || !int.TryParse(logonTypeText, out var logonType)
            || !_options.AllowedLogonTypes.Contains(logonType)
            || !_state!.Accounts.TryGetValue(accountSid, out var existing)
            || existing.PrivilegedAt is null
            || securityEvent.EventTime < existing.PrivilegedAt
            || securityEvent.EventTime - existing.CreatedAt > CorrelationWindow)
            return null;

        _state.Accounts.Remove(accountSid);
        var hasInitialLogon = existing.InitialLogonAt is not null;
        var data = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["CorrelationType"] = hasInitialLogon
                ? "InitialLogonThenCreatedAccountAddedToPrivilegedGroupThenNewAccountLoggedOn"
                : "CreatedAccountAddedToPrivilegedGroupThenLoggedOn",
            ["Risk"] = "High",
            ["AccountSid"] = existing.AccountSid,
            ["AccountName"] = existing.AccountName,
            ["AccountDomain"] = existing.AccountDomain,
            ["CreatedAt"] = existing.CreatedAt.ToString("O"),
            ["CreatedOnHost"] = existing.CreatedOnHost,
            ["CreatedBy"] = existing.CreatedBy,
            ["InitialLogonObserved"] = hasInitialLogon.ToString().ToLowerInvariant(),
            ["InitialLogonAt"] = existing.InitialLogonAt?.ToString("O"),
            ["InitialLogonHost"] = existing.InitialLogonHost,
            ["InitialLogonAccountSid"] = existing.InitialLogonAccountSid,
            ["InitialLogonAccountName"] = existing.InitialLogonAccountName,
            ["InitialLogonType"] = existing.InitialLogonType,
            ["InitialLogonIpAddress"] = existing.InitialLogonIpAddress,
            ["PrivilegedAt"] = existing.PrivilegedAt?.ToString("O"),
            ["PrivilegedOnHost"] = existing.PrivilegedOnHost,
            ["PrivilegedGroupSid"] = existing.PrivilegedGroupSid,
            ["PrivilegedGroupName"] = existing.PrivilegedGroupName,
            ["PrivilegedBy"] = existing.PrivilegedBy,
            ["LogonAt"] = securityEvent.EventTime.ToString("O"),
            ["LogonHost"] = securityEvent.Hostname,
            ["LogonType"] = logonTypeText,
            ["LogonIpAddress"] = Get(securityEvent, "IpAddress"),
            ["LogonWorkstationName"] = Get(securityEvent, "WorkstationName"),
            ["AuthenticationPackageName"] = Get(securityEvent, "AuthenticationPackageName"),
            ["SequenceWindowMinutes"] = _options.WindowMinutes.ToString()
        };

        logger.LogWarning(
            "Completed high-confidence privilege chain for {AccountSid} on {LogonHost}.",
            accountSid,
            securityEvent.Hostname);
        return securityEvent with { Data = data };
    }

    private bool IsAllowedLogon(SecurityEvent securityEvent) =>
        int.TryParse(Get(securityEvent, "LogonType"), out var logonType)
        && _options.AllowedLogonTypes.Contains(logonType);

    private bool TrackLogonSession(SecurityEvent securityEvent)
    {
        var logonId = Get(securityEvent, "TargetLogonId");
        if (string.IsNullOrWhiteSpace(logonId) || logonId == "0x0")
            return false;

        var key = SessionKey(securityEvent.Hostname, logonId);
        _state!.LogonSessions[key] = new LogonSessionState(
            key,
            logonId,
            securityEvent.EventTime,
            securityEvent.Hostname,
            Get(securityEvent, "TargetUserSid"),
            Get(securityEvent, "TargetUserName"),
            Get(securityEvent, "TargetDomainName"),
            Get(securityEvent, "LogonType"),
            Get(securityEvent, "IpAddress"));
        return true;
    }

    private bool PurgeExpired(DateTimeOffset now)
    {
        var expiredAccounts = _state!.Accounts
            .Where(pair => now - pair.Value.CreatedAt > CorrelationWindow)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var sid in expiredAccounts)
            _state.Accounts.Remove(sid);

        var expiredSessions = _state.LogonSessions
            .Where(pair => now - pair.Value.LogonAt > CorrelationWindow)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var key in expiredSessions)
            _state.LogonSessions.Remove(key);
        return expiredAccounts.Length > 0 || expiredSessions.Length > 0;
    }

    private bool IsPrivilegedGroup(string sid)
    {
        if (_options.PrivilegedGroupSids.Contains(sid, StringComparer.OrdinalIgnoreCase))
            return true;
        var lastSeparator = sid.LastIndexOf('-');
        return lastSeparator >= 0
               && int.TryParse(sid[(lastSeparator + 1)..], out var rid)
               && _options.PrivilegedDomainGroupRids.Contains(rid);
    }

    private TimeSpan CorrelationWindow => TimeSpan.FromMinutes(Math.Max(1, _options.WindowMinutes));

    private static string? Get(SecurityEvent securityEvent, string name) =>
        securityEvent.Data.TryGetValue(name, out var value) ? value : null;

    private static string? QualifiedSubject(SecurityEvent securityEvent)
    {
        var name = Get(securityEvent, "SubjectUserName");
        var domain = Get(securityEvent, "SubjectDomainName");
        if (string.IsNullOrWhiteSpace(name)) return null;
        return string.IsNullOrWhiteSpace(domain) ? name : $"{domain}\\{name}";
    }

    private static string SessionKey(string hostname, string logonId) =>
        $"{hostname.Trim().ToUpperInvariant()}|{logonId.Trim().ToUpperInvariant()}";

    private static string? QualifiedAccount(LogonSessionState? session)
    {
        if (session is null || string.IsNullOrWhiteSpace(session.AccountName)) return null;
        return string.IsNullOrWhiteSpace(session.AccountDomain)
            ? session.AccountName
            : $"{session.AccountDomain}\\{session.AccountName}";
    }
}
