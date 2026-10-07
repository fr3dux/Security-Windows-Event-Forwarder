using Security.WindowsEventForwarder.Configuration;
using Security.WindowsEventForwarder.Correlation;
using Security.WindowsEventForwarder.Infrastructure;
using Security.WindowsEventForwarder.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Security.WindowsEventForwarder.Tests;

public sealed class AccountPrivilegeCorrelationEngineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dtef-correlation-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EmitsRawStagesAndCorrelatedEventForTheSameAccountSid()
    {
        var engine = CreateEngine();
        var now = DateTimeOffset.UtcNow;
        const string accountSid = "S-1-5-21-1-2-3-1200";

        var initialLogon = await engine.ProcessAsync(Event(4624, now, new()
        {
            ["TargetUserSid"] = "S-1-5-21-1-2-3-1100",
            ["TargetUserName"] = "operator",
            ["TargetDomainName"] = "EXAMPLE",
            ["TargetLogonId"] = "0x12345",
            ["LogonType"] = "10",
            ["IpAddress"] = "192.0.2.30"
        }), CancellationToken.None);

        var created = await engine.ProcessAsync(Event(4720, now.AddSeconds(10), new()
        {
            ["TargetSid"] = accountSid,
            ["TargetUserName"] = "new-admin",
            ["TargetDomainName"] = "EXAMPLE",
            ["SubjectUserName"] = "operator",
            ["SubjectDomainName"] = "EXAMPLE",
            ["SubjectUserSid"] = "S-1-5-21-1-2-3-1100",
            ["SubjectLogonId"] = "0x12345"
        }), CancellationToken.None);

        var privileged = await engine.ProcessAsync(Event(4728, now.AddMinutes(1), new()
        {
            ["MemberSid"] = accountSid,
            ["TargetSid"] = "S-1-5-21-1-2-3-512",
            ["TargetUserName"] = "Domain Admins",
            ["SubjectUserName"] = "operator",
            ["SubjectDomainName"] = "EXAMPLE"
        }), CancellationToken.None);

        var logon = await engine.ProcessAsync(Event(4624, now.AddMinutes(2), new()
        {
            ["TargetUserSid"] = accountSid,
            ["TargetUserName"] = "new-admin",
            ["LogonType"] = "10",
            ["IpAddress"] = "192.0.2.40"
        }), CancellationToken.None);

        Assert.Single(created);
        Assert.Single(privileged);
        Assert.Equal(2, logon.Count);
        Assert.All(initialLogon.Concat(created).Concat(privileged).Concat(logon), item =>
            Assert.Equal("Windows_AD_Events", item.Tag));
        var correlated = Assert.Single(logon.Where(item =>
            item.Event.Data.ContainsKey("CorrelationType")));
        Assert.Equal(accountSid, correlated.Event.Data["AccountSid"]);
        Assert.Equal("Domain Admins", correlated.Event.Data["PrivilegedGroupName"]);
        Assert.Equal("10", correlated.Event.Data["LogonType"]);
        Assert.Equal("true", correlated.Event.Data["InitialLogonObserved"]);
        Assert.Equal("EXAMPLE\\operator", correlated.Event.Data["InitialLogonAccountName"]);
        Assert.Equal(
            "InitialLogonThenCreatedAccountAddedToPrivilegedGroupThenNewAccountLoggedOn",
            correlated.Event.Data["CorrelationType"]);
    }

    [Fact]
    public async Task DoesNotCorrelateDifferentAccountSid()
    {
        var engine = CreateEngine();
        var now = DateTimeOffset.UtcNow;
        await engine.ProcessAsync(Event(4720, now, new()
        {
            ["TargetSid"] = "S-1-5-21-1-2-3-1200",
            ["TargetUserName"] = "new-admin"
        }), CancellationToken.None);
        await engine.ProcessAsync(Event(4728, now.AddMinutes(1), new()
        {
            ["MemberSid"] = "S-1-5-21-1-2-3-1200",
            ["TargetSid"] = "S-1-5-21-1-2-3-512"
        }), CancellationToken.None);

        var logon = await engine.ProcessAsync(Event(4624, now.AddMinutes(2), new()
        {
            ["TargetUserSid"] = "S-1-5-21-1-2-3-1300",
            ["LogonType"] = "2"
        }), CancellationToken.None);

        Assert.Single(logon);
        Assert.DoesNotContain(logon, item => item.Event.Data.ContainsKey("CorrelationType"));
    }

    private AccountPrivilegeCorrelationEngine CreateEngine()
    {
        var paths = new AgentPaths(_root);
        paths.EnsureDirectories();
        return new AccountPrivilegeCorrelationEngine(
            Options.Create(new AgentOptions { Tag = "Windows_AD_Events" }),
            Options.Create(new CorrelationOptions()),
            new CorrelationStateStore(paths),
            NullLogger<AccountPrivilegeCorrelationEngine>.Instance);
    }

    private static SecurityEvent Event(
        int eventId,
        DateTimeOffset eventTime,
        Dictionary<string, string?> data) =>
        new("Security", eventTime, "DC01.example.local", eventId, eventId, "Microsoft-Windows-Security-Auditing", data, "<Bookmark />");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
