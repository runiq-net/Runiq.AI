namespace Runiq.Mcp.Options;

public sealed class RuniqMcpOptions
{
    public string Path { get; set; } = "/mcp";

    public bool ExposeTools { get; set; } = true;

    public bool ExposeResources { get; set; } = false;

    public bool ExposePrompts { get; set; } = false;
}