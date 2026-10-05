using Darktrace.WindowsEventForwarder.Models;

namespace Darktrace.WindowsEventForwarder.Formatting;

public interface ISourceAddressResolver
{
    Task<string> ResolveAsync(SecurityEvent securityEvent, CancellationToken cancellationToken);
}
