namespace Security.WindowsEventForwarder.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public int MaximumQueueSizeMB { get; set; } = 100;
    public int RetryInitialSeconds { get; set; } = 2;
    public int RetryMaximumSeconds { get; set; } = 300;
}
