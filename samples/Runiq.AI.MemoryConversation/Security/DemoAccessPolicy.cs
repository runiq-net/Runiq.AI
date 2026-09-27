using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.MemoryConversation.Security;

internal sealed class DemoAccessPolicy : IMemoryAccessPolicy
{
    /// <inheritdoc />
    public ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(identity == new MemoryIdentity("demo-user", "demo-tenant") && resourceId == "demo-workspace");
    }
}
