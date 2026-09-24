using System.Diagnostics;
using System.Threading.Channels;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime.Cli;
using Runiq.AI.Agents.Tools;

namespace Runiq.AI.Agents.Runtime.Codex;

/// <summary>Configures Codex to use the run-owned Runiq MCP bridge.</summary>
internal static class CodexToolBridge
{
    internal const string TokenVariable = CliToolBridge.TokenVariable;

    internal static async Task<CliToolBridge> StartAsync(Agent agent, AgentToolInvoker invoker,
        ProcessStartInfo command, CodexExecutorOptions limits, ChannelWriter<AgentExecutionEvent> events,
        CancellationToken cancellationToken)
    {
        var bridge = await CliToolBridge.StartAsync(agent, invoker, limits.MaxEventCharacters,
            limits.MaxOutputCharacters, events, cancellationToken);
        command.Environment[TokenVariable] = bridge.Token;
        command.ArgumentList.Insert(command.ArgumentList.Count - 1, "-c");
        command.ArgumentList.Insert(command.ArgumentList.Count - 1,
            $"mcp_servers.runiq_agent_tools={{url=\"{bridge.Endpoint}\",bearer_token_env_var=\"{TokenVariable}\",required=true,default_tools_approval_mode=\"approve\",tool_timeout_sec={(long)Math.Ceiling(limits.Timeout.TotalSeconds)}}}");
        return bridge;
    }
}
