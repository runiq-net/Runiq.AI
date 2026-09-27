using System.Text.Json;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Runtime;

using Runiq.AI.Memory.PostgreSql.TestHost.Providers;

namespace Runiq.AI.Memory.PostgreSql.TestHost.Runtime;

internal sealed class ConversationProbe(AgentExecutionRuntime runtime, ProbeModel model)
{
    internal async Task RunAsync(string[] args)
    {
        var query = new AgentQuery(args[0] == "model-first" ? "My name is Ada" : "What is my name?")
        { Memory = new("resource", args.Length > 2 ? args[2] : null), MemoryTurnId = args[0] };
        AgentExecutionResult result;
        if (args[0] == "model-stream")
        {
            var builder = new AgentExecutionResultBuilder();
            await foreach (var item in runtime.ExecuteStreamAsync("agent", query)) builder.Apply(item);
            result = builder.Build();
        }
        else result = await runtime.ExecuteAsync("agent", query);
        if (!result.IsSuccess) throw new InvalidOperationException(result.ErrorCode);
        Console.WriteLine(JsonSerializer.Serialize(new { result.ThreadId, result.Message, model.Messages, model.ToolCalls }));
    }
}
