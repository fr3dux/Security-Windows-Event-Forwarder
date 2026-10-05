namespace Darktrace.WindowsEventForwarder.Infrastructure;

public sealed class AgentPaths
{
    public AgentPaths(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "DarktraceEventForwarder");
    }

    public string Root { get; }
    public string Queue => Path.Combine(Root, "queue");
    public string Log => Path.Combine(Root, "logs", "agent.log");
    public string Configuration => Path.Combine(Root, "agentsettings.json");
    public string CorrelationState => Path.Combine(Root, "correlation-state.json");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Queue);
        Directory.CreateDirectory(Path.GetDirectoryName(Log)!);
    }

    public string BookmarkFor(string channel)
    {
        var safeChannel = string.Concat(channel.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
        return Path.Combine(Root, $"bookmark-{safeChannel}.xml");
    }
}
