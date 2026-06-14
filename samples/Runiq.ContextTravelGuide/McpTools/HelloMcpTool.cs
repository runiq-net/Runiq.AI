using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Runiq.ContextTravelGuide.McpTools;

[McpServerToolType]
public sealed class HelloMcpTool
{
    [McpServerTool]
    [Description("Returns a simple hello message from the Runiq MCP server.")]
    public string SayHello(
        [Description("The name to greet.")] string name)
    {
        return $"Hello {name}, this response came from Runiq.Mcp.";
    }
}