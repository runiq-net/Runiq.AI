using System.Xml.Linq;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.Tests;

public sealed class MemoryContractTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" user")]
    [InlineData("user ")]
    [InlineData("a\nb")]
    // Reject malformed identifiers instead of normalizing them into another owner's identifier.
    public void Identifiers_RejectInvalidValues(string value)
    {
        Assert.Throws<ArgumentException>(() => new MemoryReference(value));
        Assert.Throws<ArgumentException>(() => new MemoryReference("resource", value));
        Assert.Throws<ArgumentException>(() => new MemoryAccessScope(value, "resource", "agent"));
        Assert.Throws<ArgumentException>(() => new MemoryAccessScope("tenant", value, "agent"));
        Assert.Throws<ArgumentException>(() => new MemoryAccessScope("tenant", "resource", value));
    }

    [Fact]
    // Structural scopes prevent delimiter collisions while preserving identity-specific comparison rules.
    public void Scopes_PreserveIndependentIdentities()
    {
        Assert.NotEqual(new MemoryAccessScope("a:b", "c", "agent"), new MemoryAccessScope("a", "b:c", "agent"));
        Assert.Equal(new MemoryAccessScope("tenant", "resource", "Agent"), new MemoryAccessScope("tenant", "resource", "agent"));
        Assert.NotEqual(new MemoryReference("Resource", "thread"), new MemoryReference("resource", "thread"));
        Assert.Null(new MemoryReference("resource").ThreadId);
        Assert.Throws<ArgumentException>(() => new MemoryReference(new string('a', 257)));
    }

    [Fact]
    // Inspect project references even when a forbidden dependency is not used by a compiled type.
    public void Projects_KeepMemoryIndependentOfAgentsAndProviders()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Runiq.AI.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        string[] References(string project) => XDocument.Load(Path.Combine(root.FullName, "src", project, project + ".csproj"))
            .Descendants("ProjectReference").Select(x => x.Attribute("Include")!.Value.Replace('\\', '/')).ToArray();
        Assert.Equal(["../Runiq.AI.Core/Runiq.AI.Core.csproj"], References("Runiq.AI.Memory"));
        Assert.DoesNotContain(References("Runiq.AI.Core"), x => x.Contains("Memory") || x.Contains("Agents"));
        Assert.Contains("../Runiq.AI.Memory/Runiq.AI.Memory.csproj", References("Runiq.AI.Agents"));
        Assert.DoesNotContain(References("Runiq.AI.Agents"), x => x.Contains("Memory.PostgreSql"));
        Assert.Equal(["../Runiq.AI.Memory/Runiq.AI.Memory.csproj"], References("Runiq.AI.Memory.PostgreSql"));
    }
}
