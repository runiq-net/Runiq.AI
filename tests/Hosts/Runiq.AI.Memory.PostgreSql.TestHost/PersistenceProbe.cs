using System.Text.Json;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Configuration;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.Services;

namespace Runiq.AI.Memory.PostgreSql.TestHost;

internal sealed class PersistenceProbe(IMemoryConversationStore store, MemoryAuthorizationService authorization, MemoryConversationService conversations)
{
    internal async Task RunAsync(string[] args)
    {
        var mode = args[0];
        var context = await authorization.AuthorizeAsync(new("test-host", "tenant"),
            new("resource", mode == "write" ? null : args[2]), "agent", new MemoryOptions())
            ?? throw new InvalidOperationException("Test host was not authorized.");
        if (mode == "write") await store.CreateAsync(context);
        if (mode == "race")
        {
            Console.WriteLine("READY");
            if (await Console.In.ReadLineAsync() != "GO") throw new InvalidOperationException("Missing race barrier.");
            try
            {
                var result = await store.AppendAsync(context, new(args[3], 3,
                    [new(args[3], "race", DateTimeOffset.UnixEpoch, new(ChatRole.User, args[3]))]));
                Console.WriteLine(JsonSerializer.Serialize(new { Success = true, result.Version }));
            }
            catch (MemoryStoreException exception) when (exception.Error == MemoryStoreError.VersionConflict)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { Success = false, Error = exception.Error.ToString() }));
            }
            return;
        }
        if (mode == "turn-race")
        {
            Console.WriteLine("READY");
            if (await Console.In.ReadLineAsync() != "GO") throw new InvalidOperationException("Missing race barrier.");
            try
            {
                await conversations.BeginAsync(context, args[3], args[3], args[3], DateTimeOffset.UtcNow);
                Console.WriteLine(JsonSerializer.Serialize(new { Success = true }));
            }
            catch (MemoryStoreException exception) when (exception.Error is MemoryStoreError.VersionConflict or MemoryStoreError.TurnConflict)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { Success = false, Error = exception.Error.ToString() }));
            }
            return;
        }
        // The identical payload is reconstructed in a new process, proving durable retries without static shared state.
        var time = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.FromHours(3)).AddTicks(1234567);
        var request = new MemoryAppendRequest("original-request", 0, [
            new("user", "run", time, new(ChatRole.User, "question")),
            new("assistant", "run", time.AddTicks(1), new(ChatRole.Assistant, "", ToolCalls: [new("call", "tool", "{\"x\":1}")])),
            new("tool", "run", time.AddTicks(2), new(ChatRole.Tool, "answer", "call"))]);
        var receipt = await store.AppendAsync(context, request);
        var conversation = await store.ReadAsync(context);
        var messages = await store.ReadMessagesAsync(context);
        Console.WriteLine(JsonSerializer.Serialize(new { Conversation = conversation, Messages = messages, Receipt = receipt }));
    }
}
