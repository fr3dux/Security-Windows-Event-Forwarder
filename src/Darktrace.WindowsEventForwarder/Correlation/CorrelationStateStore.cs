using System.Text.Json;
using Darktrace.WindowsEventForwarder.Infrastructure;
using Darktrace.WindowsEventForwarder.Models;

namespace Darktrace.WindowsEventForwarder.Correlation;

public sealed class CorrelationStateStore(AgentPaths paths)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<CorrelationStateDocument> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.CorrelationState))
            return new CorrelationStateDocument();

        var json = await File.ReadAllTextAsync(paths.CorrelationState, cancellationToken);
        var state = JsonSerializer.Deserialize<CorrelationStateDocument>(json)
            ?? new CorrelationStateDocument();
        state.Accounts = new Dictionary<string, AccountCorrelationState>(
            state.Accounts,
            StringComparer.OrdinalIgnoreCase);
        state.LogonSessions = new Dictionary<string, LogonSessionState>(
            state.LogonSessions,
            StringComparer.OrdinalIgnoreCase);
        return state;
    }

    public async Task SaveAsync(
        CorrelationStateDocument state,
        CancellationToken cancellationToken)
    {
        var temporaryPath = paths.CorrelationState + ".tmp";
        await File.WriteAllTextAsync(
            temporaryPath,
            JsonSerializer.Serialize(state, JsonOptions),
            cancellationToken);
        File.Move(temporaryPath, paths.CorrelationState, true);
    }
}
