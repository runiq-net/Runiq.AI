using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.Tests;

public sealed class MemoryIdentityTests
{
    [Fact]
    // Host identity requires an explicit application or tenant boundary and never implies resource ownership.
    public void Identity_RequiresSeparateCallerAndBoundary()
    {
        Assert.Throws<ArgumentException>(() => new MemoryIdentity("", "tenant"));
        Assert.Throws<ArgumentException>(() => new MemoryIdentity("caller", " "));
        var identity = new MemoryIdentity("caller", "application");
        Assert.Equal("caller", identity.CallerId);
        Assert.Equal("application", identity.BoundaryId);
        Assert.NotEqual(identity, new MemoryIdentity("caller", "Application"));
    }
}
