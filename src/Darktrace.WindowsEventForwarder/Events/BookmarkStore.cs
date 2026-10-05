using Darktrace.WindowsEventForwarder.Infrastructure;

namespace Darktrace.WindowsEventForwarder.Events;

public sealed class BookmarkStore(AgentPaths paths)
{
    public async Task<string?> LoadAsync(string channel, CancellationToken cancellationToken)
    {
        var path = paths.BookmarkFor(channel);
        if (!File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public async Task SaveAsync(string channel, string bookmarkXml, CancellationToken cancellationToken)
    {
        var path = paths.BookmarkFor(channel);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, bookmarkXml, cancellationToken);
        File.Move(temporary, path, true);
    }
}
