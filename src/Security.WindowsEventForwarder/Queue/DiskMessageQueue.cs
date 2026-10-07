using System.Text.Json;
using Security.WindowsEventForwarder.Configuration;
using Security.WindowsEventForwarder.Infrastructure;
using Security.WindowsEventForwarder.Models;
using Microsoft.Extensions.Options;

namespace Security.WindowsEventForwarder.Queue;

public sealed class DiskMessageQueue(
    AgentPaths paths,
    IOptions<StorageOptions> options,
    ILogger<DiskMessageQueue> logger) : IMessageQueue
{
    private readonly StorageOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task EnqueueAsync(QueuedMessage message, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureCapacity();
            var finalPath = GetPath(message.Id);
            var temporaryPath = finalPath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(message), cancellationToken);
            File.Move(temporaryPath, finalPath, true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QueuedMessage?> PeekAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var path = Directory.EnumerateFiles(paths.Queue, "*.json")
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .FirstOrDefault();
            if (path is null) return null;

            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<QueuedMessage>(json)
                ?? throw new InvalidDataException($"Invalid queue record: {path}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            File.Delete(GetPath(id));
        }
        finally
        {
            _gate.Release();
        }
    }

    private string GetPath(string id) => Path.Combine(paths.Queue, id + ".json");

    private void EnsureCapacity()
    {
        var maximumBytes = Math.Max(1, _options.MaximumQueueSizeMB) * 1024L * 1024L;
        var files = Directory.EnumerateFiles(paths.Queue, "*.json")
            .Select(path => new FileInfo(path))
            .OrderBy(file => file.Name, StringComparer.Ordinal)
            .ToList();
        var currentBytes = files.Sum(file => file.Length);
        if (currentBytes < maximumBytes) return;

        logger.LogWarning("Queue reached {MaximumQueueSizeMB} MB; deleting the oldest record.", _options.MaximumQueueSizeMB);
        if (files.Count > 0) files[0].Delete();
    }
}
