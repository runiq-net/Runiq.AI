using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.PostgreSql.TestHost;

// This executable is an isolated test host with a fixed trusted identity, never a production authentication adapter.
internal sealed class TestAccessPolicy : IMemoryAccessPolicy
{
    public ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(identity.CallerId == "test-host" && identity.BoundaryId == "tenant" && resourceId == "resource");
}
