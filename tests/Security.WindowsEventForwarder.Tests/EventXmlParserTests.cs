using Security.WindowsEventForwarder.Events;

namespace Security.WindowsEventForwarder.Tests;

public sealed class EventXmlParserTests
{
    [Fact]
    public void ParsesNamedEventDataWithoutDependingOnLocalizedMessage()
    {
        const string xml = """
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <System>
                <Provider Name="Microsoft-Windows-Security-Auditing" />
                <EventID>4732</EventID>
                <TimeCreated SystemTime="2026-10-03T12:30:00.0000000Z" />
                <EventRecordID>4219</EventRecordID>
                <Computer>DC01.example.local</Computer>
              </System>
              <EventData>
                <Data Name="MemberName">CN=alice,CN=Users,DC=example,DC=local</Data>
                <Data Name="TargetUserName">VPN-Users</Data>
                <Data Name="TargetSid">S-1-5-21-1-2-3-1104</Data>
                <Data Name="SubjectUserName">Administrator</Data>
              </EventData>
            </Event>
            """;

        var result = EventXmlParser.Parse(xml, "<Bookmark />");

        Assert.Equal(4732, result.EventId);
        Assert.Equal("Security", result.Channel);
        Assert.Equal(4219, result.RecordId);
        Assert.Equal("DC01.example.local", result.Hostname);
        Assert.Equal("VPN-Users", result.Data["TargetUserName"]);
        Assert.Equal("Administrator", result.Data["SubjectUserName"]);
    }
}
