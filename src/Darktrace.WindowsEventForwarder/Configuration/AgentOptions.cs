namespace Darktrace.WindowsEventForwarder.Configuration;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public string SourceAddress { get; set; } = string.Empty;
    public string SourceAddressMode { get; set; } = "Configured";
    public string Channel { get; set; } = "Security";
    public string Tag { get; set; } = "WIN_AD_GROUP_CHANGE";
    public bool ReadExistingEventsOnFirstStart { get; set; }
    public int[] EventIds { get; set; } = [4728, 4732, 4756, 4729, 4733, 4757];
    public Dictionary<string, string> SourceAddressOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
