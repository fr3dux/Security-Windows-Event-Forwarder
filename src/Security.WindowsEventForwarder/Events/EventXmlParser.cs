using System.Xml.Linq;
using Security.WindowsEventForwarder.Models;

namespace Security.WindowsEventForwarder.Events;

public static class EventXmlParser
{
    public static SecurityEvent Parse(string xml, string bookmarkXml, string channel = "Security")
    {
        var document = XDocument.Parse(xml);
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;
        var system = document.Root?.Element(ns + "System")
            ?? throw new FormatException("Windows event does not contain a System element.");

        var eventId = int.Parse(system.Element(ns + "EventID")?.Value
            ?? throw new FormatException("Windows event does not contain EventID."));
        var recordIdText = system.Element(ns + "EventRecordID")?.Value;
        long? recordId = long.TryParse(recordIdText, out var parsedRecordId) ? parsedRecordId : null;
        var created = system.Element(ns + "TimeCreated")?.Attribute("SystemTime")?.Value;
        var eventTime = DateTimeOffset.TryParse(created, out var parsedTime)
            ? parsedTime
            : DateTimeOffset.UtcNow;

        var data = document.Root?
            .Element(ns + "EventData")?
            .Elements(ns + "Data")
            .Where(item => item.Attribute("Name") is not null)
            .GroupBy(item => item.Attribute("Name")!.Value)
            .ToDictionary(group => group.Key, group => (string?)group.Last().Value)
            ?? new Dictionary<string, string?>();

        return new SecurityEvent(
            channel,
            eventTime,
            system.Element(ns + "Computer")?.Value ?? Environment.MachineName,
            eventId,
            recordId,
            system.Element(ns + "Provider")?.Attribute("Name")?.Value ?? string.Empty,
            data,
            bookmarkXml);
    }
}
