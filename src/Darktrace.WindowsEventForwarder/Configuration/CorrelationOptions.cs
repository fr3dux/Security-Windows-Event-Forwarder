namespace Darktrace.WindowsEventForwarder.Configuration;

public sealed class CorrelationOptions
{
    public const string SectionName = "Correlation";

    public bool Enabled { get; set; } = true;
    public int WindowMinutes { get; set; } = 1440;
    public int[] AllowedLogonTypes { get; set; } = [2, 10];
    public string[] PrivilegedGroupSids { get; set; } = ["S-1-5-32-544"];
    public int[] PrivilegedDomainGroupRids { get; set; } = [512, 518, 519];
}
