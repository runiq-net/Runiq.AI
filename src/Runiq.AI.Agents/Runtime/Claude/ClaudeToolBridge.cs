using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime.Cli;
using Runiq.AI.Agents.Tools;

namespace Runiq.AI.Agents.Runtime.Claude;

/// <summary>Connects Claude to run-local tool bindings without persisting credentials or bypassing permissions.</summary>
internal static class ClaudeToolBridge
{
    internal static async Task<CliToolBridge> StartAsync(Agent agent, AgentToolInvoker invoker,
        ProcessStartInfo command, ClaudeExecutorOptions limits, ChannelWriter<AgentExecutionEvent> events,
        CancellationToken cancellationToken)
    {
        // MCP names must not introduce wildcard or permission-rule syntax into allowedTools.
        if (agent.Tools.Any(tool => string.IsNullOrEmpty(tool.Name) ||
            tool.Name.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-' and not '.')))
            throw new ClaudeException("ClaudeConfigurationInvalid");
        var bridge = await CliToolBridge.StartAsync(agent, invoker, limits.MaxEventCharacters,
            limits.MaxOutputCharacters, events, cancellationToken);
        command.Environment[CliToolBridge.TokenVariable] = bridge.Token;
        command.ArgumentList.Add("--mcp-config");
        command.ArgumentList.Add(JsonSerializer.Serialize(new
        {
            mcpServers = new
            {
                runiq_agent_tools = new
                {
                    type = "http", url = bridge.Endpoint,
                    headers = new { Authorization = "Bearer ${" + CliToolBridge.TokenVariable + "}" }
                }
            }
        }));
        // dontAsk remains in effect; authorize only the tools explicitly attached by the host.
        command.ArgumentList.Add("--allowedTools");
        command.ArgumentList.Add(string.Join(",", agent.Tools.Select(tool => "mcp__runiq_agent_tools__" + tool.Name)));
        return bridge;
    }
}
