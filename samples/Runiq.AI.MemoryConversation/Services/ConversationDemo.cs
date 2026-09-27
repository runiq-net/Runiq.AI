using Runiq.AI.Agents;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.MemoryConversation.Services;

internal sealed class ConversationDemo(AgentExecutionRuntime runtime)
{
    internal async Task RunAsync()
    {
        var first = await runtime.ExecuteAsync("conversation", new AgentQuery("My name is Ada.")
        {
            Memory = new MemoryReference("demo-workspace"),
            MemoryTurnId = "introduce-name"
        });
        if (!first.IsSuccess || first.ThreadId is null) throw new InvalidOperationException(first.ErrorCode);
        Console.WriteLine($"Thread: {first.ThreadId}");
        Console.WriteLine(first.Message);

        var result = new AgentExecutionResultBuilder();
        await foreach (var item in runtime.ExecuteStreamAsync("conversation", new AgentQuery("What is my name?")
        {
            Memory = new MemoryReference("demo-workspace", first.ThreadId),
            MemoryTurnId = "recall-name"
        }))
        {
            result.Apply(item);
            if (item.Kind == AgentExecutionEventKind.AssistantDelta) Console.Write(item.Content);
        }
        if (!result.Build().IsSuccess) throw new InvalidOperationException(result.Build().ErrorCode);
        Console.WriteLine();
    }
}
