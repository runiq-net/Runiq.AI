using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Hosting.Memory;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Agents.Tests.Hosting.Agents;

public sealed class HttpMemoryIdentityTests
{
    [Theory]
    [InlineData(false, "caller", "tenant", false, false)]
    [InlineData(true, null, "tenant", false, false)]
    [InlineData(true, "caller", null, false, false)]
    [InlineData(true, "caller", "tenant", true, false)]
    [InlineData(true, "caller", "tenant", false, true)]
    // Only unambiguous claims from an authenticated identity may establish a tenant-scoped caller.
    public async Task Claims_RequireAuthenticatedUnambiguousContext(bool authenticated, string? caller, string? tenant,
        bool duplicate, bool allowed)
    {
        var context = new DefaultHttpContext();
        var claims = new List<Claim>();
        if (caller is not null) claims.Add(new Claim("sub", caller));
        if (tenant is not null) claims.Add(new Claim("tenant", tenant));
        if (duplicate) claims.Add(new Claim("tenant", "other"));
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null));
        context.Request.Headers["X-User-Id"] = "spoofed";
        context.Request.Headers["X-Tenant-Id"] = "spoofed";
        var resolver = new HttpMemoryIdentityResolver(new HttpContextAccessor { HttpContext = context },
            new HttpMemoryIdentityOptions(tenantClaimType: "tenant", callerClaimType: "sub"));
        var result = await resolver.ResolveAsync(default);
        Assert.Equal(allowed, result is not null);
        if (result is not null) Assert.Equal(new MemoryIdentity("caller", "tenant"), result);
    }

    [Fact]
    // Single-tenant mapping uses a host-specific boundary and remains scoped in dependency injection.
    public async Task SingleTenant_UsesExplicitHostBoundary()
    {
        var services = new ServiceCollection().AddRuniqHttpMemoryIdentity(new HttpMemoryIdentityOptions("application"));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "caller")], "test"))
        };
        Assert.Equal(new MemoryIdentity("caller", "application"), await scope.ServiceProvider.GetRequiredService<IMemoryIdentityResolver>().ResolveAsync(default));
        Assert.Throws<ArgumentException>(() => new HttpMemoryIdentityOptions());
        Assert.Throws<ArgumentException>(() => new HttpMemoryIdentityOptions("application", "tenant"));
        using var second = provider.CreateScope();
        Assert.NotSame(scope.ServiceProvider.GetRequiredService<IMemoryIdentityResolver>(), second.ServiceProvider.GetRequiredService<IMemoryIdentityResolver>());
    }

    [Fact]
    // Async HTTP flows keep caller identity separate even when they share the same singleton accessor.
    public async Task ConcurrentRequests_DoNotLeakIdentity()
    {
        var accessor = new HttpContextAccessor();
        var options = new HttpMemoryIdentityOptions("application");
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async index =>
        {
            accessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, $"caller-{index}")], "test"))
            };
            await Task.Yield();
            return await new HttpMemoryIdentityResolver(accessor, options).ResolveAsync(default);
        }));
        Assert.Equal(20, results.Select(x => x!.CallerId).Distinct().Count());
    }

    [Theory]
    [InlineData("duplicate-caller")]
    [InlineData("multiple-authenticated")]
    [InlineData("unauthenticated-claim")]
    [InlineData("invalid-caller")]
    // Claims cannot be combined across identities or repaired from untrusted authentication sources.
    public async Task AmbiguousOrUntrustedClaims_DenyIdentity(string scenario)
    {
        var verified = new ClaimsIdentity([], "test");
        if (scenario != "unauthenticated-claim")
            verified.AddClaim(new Claim(ClaimTypes.NameIdentifier, scenario == "invalid-caller" ? " caller " : "caller"));
        if (scenario == "duplicate-caller") verified.AddClaim(new Claim(ClaimTypes.NameIdentifier, "other"));
        var principal = new ClaimsPrincipal(verified);
        if (scenario is "multiple-authenticated" or "unauthenticated-claim")
            principal.AddIdentity(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "other")],
                scenario == "multiple-authenticated" ? "second" : null));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        Assert.Null(await new HttpMemoryIdentityResolver(accessor, new HttpMemoryIdentityOptions("application")).ResolveAsync(default));
    }
}
