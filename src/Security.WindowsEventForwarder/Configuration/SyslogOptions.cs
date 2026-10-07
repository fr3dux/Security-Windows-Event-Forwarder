namespace Security.WindowsEventForwarder.Configuration;

public sealed class SyslogOptions
{
    public const string SectionName = "Syslog";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 1514;
    public string Protocol { get; set; } = "Tcp";
    public int ConnectTimeoutSeconds { get; set; } = 10;
}
