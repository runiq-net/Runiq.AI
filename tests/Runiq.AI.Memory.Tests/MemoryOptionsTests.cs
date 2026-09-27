using Runiq.AI.Memory.Configuration;

namespace Runiq.AI.Memory.Tests;

public sealed class MemoryOptionsTests
{
    [Fact]
    // Default opt-in isolates threads and does not imply a persistence provider or sharing policy.
    public void Defaults_AreThreadIsolated()
    {
        var options = new MemoryOptions();
        Assert.Equal(MemoryScope.Thread, options.Scope);
        Assert.Null(options.SharingGroup);
        options.Validate();
        new MemoryOptions(MemoryScope.Resource, "support").Validate();
    }

    [Fact]
    // Invalid scope and sharing input must fail before an agent can retain the configuration.
    public void InvalidSettings_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => new MemoryOptions((MemoryScope)42));
        Assert.Throws<ArgumentException>(() => new MemoryOptions(sharingGroup: " "));
    }
}
