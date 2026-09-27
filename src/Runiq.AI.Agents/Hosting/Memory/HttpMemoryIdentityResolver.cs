using Microsoft.AspNetCore.Http;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Agents.Hosting.Memory;

internal sealed class HttpMemoryIdentityResolver(IHttpContextAccessor accessor, HttpMemoryIdentityOptions options)
    : IMemoryIdentityResolver
{
    /// <inheritdoc />
    public ValueTask<MemoryIdentity?> ResolveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identities = accessor.HttpContext?.User.Identities.Where(x => x.IsAuthenticated).ToArray();
        // Do not combine claims from unauthenticated or ambiguous authentication identities.
        if (identities is not { Length: 1 }) return ValueTask.FromResult<MemoryIdentity?>(null);
        var identity = identities[0];
        string? UniqueClaim(string type)
        {
            var claims = identity.FindAll(type).ToArray();
            return claims.Length == 1 ? claims[0].Value : null;
        }
        var caller = UniqueClaim(options.CallerClaimType);
        var boundary = options.ApplicationBoundary ?? UniqueClaim(options.TenantClaimType!);
        if (caller is null || boundary is null) return ValueTask.FromResult<MemoryIdentity?>(null);
        try { return ValueTask.FromResult<MemoryIdentity?>(new MemoryIdentity(caller, boundary)); }
        catch (ArgumentException) { return ValueTask.FromResult<MemoryIdentity?>(null); }
    }
}
