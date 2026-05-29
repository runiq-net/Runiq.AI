using Runiq.ContextSpaces.Models.Sources;
using Runiq.Core.Configuration;

namespace Runiq.Core.Tests.Configuration;

public sealed class RuniqServerOptionsTests
{
    [Fact]
    public void AddContextSpace_ShouldRegisterContextSpace()
    {
        // RuniqServerOptions iÃ§ine ContextSpace kaydÄ±nÄ±n eklenebildiÄŸini doÄŸrular.
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
        // AynÄ± ContextSpace id deÄŸerinin case-insensitive olarak ikinci kez eklenemeyeceÄŸini doÄŸrular.
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
        // AddContextSpace metodunun fluent configuration iÃ§in aynÄ± options Ã¶rneÄŸini dÃ¶ndÃ¼rdÃ¼ÄŸÃ¼nÃ¼ doÄŸrular.
        var options = new RuniqServerOptions();

        var result = options.AddContextSpace(new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning Context"));

        Assert.Same(options, result);
    }
}
