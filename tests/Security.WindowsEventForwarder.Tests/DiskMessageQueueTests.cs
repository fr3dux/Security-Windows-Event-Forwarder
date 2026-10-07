using Security.WindowsEventForwarder.Configuration;
using Security.WindowsEventForwarder.Infrastructure;
using Security.WindowsEventForwarder.Models;
using Security.WindowsEventForwarder.Queue;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Security.WindowsEventForwarder.Tests;

public sealed class DiskMessageQueueTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dtef-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PersistsMessagesInFilenameOrder()
    {
        var paths = new AgentPaths(_root);
        paths.EnsureDirectories();
        var queue = new DiskMessageQueue(
            paths,
            Options.Create(new StorageOptions { MaximumQueueSizeMB = 10 }),
            NullLogger<DiskMessageQueue>.Instance);
        var second = new QueuedMessage("002", DateTimeOffset.UtcNow, 2, "second");
        var first = new QueuedMessage("001", DateTimeOffset.UtcNow, 1, "first");

        await queue.EnqueueAsync(second, CancellationToken.None);
        await queue.EnqueueAsync(first, CancellationToken.None);

        var peeked = await queue.PeekAsync(CancellationToken.None);
        Assert.Equal("001", peeked?.Id);
        await queue.DeleteAsync("001", CancellationToken.None);
        Assert.Equal("002", (await queue.PeekAsync(CancellationToken.None))?.Id);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
