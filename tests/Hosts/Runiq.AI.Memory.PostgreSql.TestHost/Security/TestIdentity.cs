using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.PostgreSql.TestHost.Security;

internal sealed class TestIdentity : IMemoryIdentityResolver
{
    /// <inheritdoc />
    public ValueTask<MemoryIdentity?> ResolveAsync(CancellationToken cancellationToken) => ValueTask.FromResult<MemoryIdentity?>(new("test-host", "tenant"));
}

