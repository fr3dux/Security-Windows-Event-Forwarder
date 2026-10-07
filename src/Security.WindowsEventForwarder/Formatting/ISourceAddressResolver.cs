using Security.WindowsEventForwarder.Models;

namespace Security.WindowsEventForwarder.Formatting;

public interface ISourceAddressResolver
{
    Task<string> ResolveAsync(SecurityEvent securityEvent, CancellationToken cancellationToken);
}
