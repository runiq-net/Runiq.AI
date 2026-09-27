using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.MemoryConversation.Security;

// This console is the trusted host. Production adapters must resolve authenticated identity from their host context.
internal sealed class DemoIdentity : IMemoryIdentityResolver
{
    /// <inheritdoc />
    public ValueTask<MemoryIdentity?> ResolveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<MemoryIdentity?>(new("demo-user", "demo-tenant"));
    }
}
