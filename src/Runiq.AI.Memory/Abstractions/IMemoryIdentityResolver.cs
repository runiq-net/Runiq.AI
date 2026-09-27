using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.Abstractions;

/// <summary>Resolves identity from trusted host context for each invocation.</summary>
/// <remarks>Register as scoped. Never deserialize client identity as verified context or cache callers on singleton agents.</remarks>
public interface IMemoryIdentityResolver
{
    /// <summary>Resolves the current verified caller without consulting untrusted query identity fields.</summary>
    /// <param name="cancellationToken">Cancels host identity resolution.</param>
    /// <returns>The verified identity, or null when authentication or tenant context is missing or ambiguous.</returns>
    ValueTask<MemoryIdentity?> ResolveAsync(CancellationToken cancellationToken);
}
