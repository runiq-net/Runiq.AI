using Microsoft.Extensions.DependencyInjection;
using Runiq.ContextSpaces.Models.Sources;
using Runiq.Core.Configuration;

namespace Runiq.Core.Tests.Configuration;

public sealed class RuniqServerOptionsTests
{
    [Fact]
    public void AddContextSpace_ShouldRegisterContextSpace()
    {
        // RuniqServerOptions içine ContextSpace kaydının eklenebildiğini doğrular.
        var options = new RuniqServerOptions();

        options.AddContextSpace(new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning Context"));

        var contextSpace = Assert.Single(options.ContextSpaces);

        Assert.Equal("travel-planning", contextSpace.Id);
        Assert.Equal("Travel Planning Context", contextSpace.Name);
    }

    [Fact]
    public void AddContextSpace_ShouldThrow_WhenContextSpaceIdAlreadyExistsIgnoringCase()
    {
        // Aynı ContextSpace id değerinin case-insensitive olarak ikinci kez eklenemeyeceğini doğrular.
        var options = new RuniqServerOptions();

        options.AddContextSpace(new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning Context"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            options.AddContextSpace(new ContextSpace(
                id: "TRAVEL-PLANNING",
                name: "Duplicate Travel Planning Context")));

        Assert.Contains("travel-planning", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddContextSpace_ShouldReturnSameOptionsInstance()
    {
        // AddContextSpace metodunun fluent configuration için aynı options örneğini döndürdüğünü doğrular.
        var options = new RuniqServerOptions();

        var result = options.AddContextSpace(new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning Context"));

        Assert.Same(options, result);
    }
}