using System.Security.Claims;

namespace Runiq.AI.Agents.Hosting.Memory;

/// <summary>Defines immutable host claim mappings and an explicit tenancy mode.</summary>
public sealed class HttpMemoryIdentityOptions
{
    /// <summary>Configures either single-application isolation or a verified tenant claim.</summary>
    /// <param name="applicationBoundary">A host-specific single-tenant boundary; mutually exclusive with tenantClaimType.</param>
    /// <param name="tenantClaimType">The tenant claim required for multi-tenant hosts.</param>
    /// <param name="callerClaimType">The authenticated caller claim; defaults to NameIdentifier.</param>
    /// <exception cref="ArgumentException">Neither or both tenancy modes are supplied, or a value is invalid.</exception>
    public HttpMemoryIdentityOptions(string? applicationBoundary = null, string? tenantClaimType = null,
        string callerClaimType = ClaimTypes.NameIdentifier)
    {
        if ((applicationBoundary is null) == (tenantClaimType is null))
            throw new ArgumentException("Select exactly one Memory tenancy mode.");
        ArgumentException.ThrowIfNullOrWhiteSpace(callerClaimType);
        if (tenantClaimType is not null) ArgumentException.ThrowIfNullOrWhiteSpace(tenantClaimType);
        if (applicationBoundary is not null)
            _ = new Runiq.AI.Memory.Models.MemoryIdentity("configuration", applicationBoundary);
        ApplicationBoundary = applicationBoundary;
        TenantClaimType = tenantClaimType;
        CallerClaimType = callerClaimType;
    }
    /// <summary>Gets the explicit application boundary, or null for multi-tenant mode.</summary>
    public string? ApplicationBoundary { get; }
    /// <summary>Gets the required tenant claim type, or null for single-tenant mode.</summary>
    public string? TenantClaimType { get; }
    /// <summary>Gets the claim containing the verified caller identifier.</summary>
    public string CallerClaimType { get; }
}
