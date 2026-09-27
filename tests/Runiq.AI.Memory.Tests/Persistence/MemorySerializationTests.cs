using System.Text.Json;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.Serialization;

namespace Runiq.AI.Memory.Tests.Persistence;

public sealed class MemorySerializationTests
{
    [Fact]
    // Runtime reasoning stays available to the provider while durable snapshots and append identity retain the exact v1 shape.
    public void Continuation_IsExcludedFromDurableMessagesAndRequestIdentity()
    {
        const string legacy = """{"MessageId":"m","RunId":"r","Timestamp":"1970-01-01T00:00:00+00:00","Message":{"Role":2,"Content":"answer","ToolCallId":null,"ToolCalls":null}}""";
        var continuation = new ChatContinuation("openai.responses.output",
            JsonSerializer.SerializeToElement(new[] { new { type = "reasoning", encrypted_content = "secret" } }), 100);
        var runtime = new ChatMessage(ChatRole.Assistant, "answer") { Continuation = continuation };
        var message = new MemoryMessage("m", "r", DateTimeOffset.UnixEpoch, runtime);
        Assert.Same(continuation, runtime.Continuation);
        Assert.Null(message.Message.Continuation);
        Assert.Equal(legacy, MemoryMessageSerializer.Serialize(message));
        Assert.Equal("{\"ExpectedVersion\":0,\"Messages\":[" + legacy + "]}",
            MemoryMessageSerializer.RequestPayload(new("key", 0, [message])));
        Assert.Null(MemoryMessageSerializer.Deserialize(legacy, 1).Message.Continuation);
        Assert.Equal(JsonSerializer.Serialize(runtime with { Continuation = null }), JsonSerializer.Serialize(runtime));
        Assert.DoesNotContain("Continuation", JsonSerializer.Serialize(runtime));
        var update = new ChatStreamingUpdate(ChatStreamingUpdateKind.Completed) { Continuation = continuation };
        Assert.DoesNotContain("Continuation", JsonSerializer.Serialize(update));
        Assert.Same(continuation, update.Continuation);
    }

    [Theory]
    [InlineData("{}", 1)]
    [InlineData("null", 1)]
    [InlineData("not json", 1)]
    [InlineData("{}", 2)]
    // Turn payload versions and required identity fields fail closed independently of message format versions.
    public void InvalidTurnPayload_FailsExplicitly(string payload, int version)
    {
        Assert.Equal(MemoryStoreError.InvalidPayload,
            Assert.Throws<MemoryStoreException>(() => MemoryTurnSerializer.Deserialize(payload, version)).Error);
    }

    [Fact]
    // A terminal turn round trip preserves original offsets and rejects unknown or duplicate fields.
    public void TurnPayload_RetainsLifecycleAndRejectsAmbiguity()
    {
        var time = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.FromHours(3)).AddTicks(1234567);
        var turn = new MemoryTurn("turn", "run", 17, time, MemoryTurnStatus.Cancelled, time.AddSeconds(1));
        var payload = MemoryTurnSerializer.Serialize(turn);
        var restored = MemoryTurnSerializer.Deserialize(payload, 1);
        Assert.Equal(turn, restored);
        Assert.True(turn.StartedAt.EqualsExact(restored.StartedAt));
        Assert.Throws<MemoryStoreException>(() => MemoryTurnSerializer.Deserialize("{\"Extra\":true," + payload[1..], 1));
        Assert.Throws<MemoryStoreException>(() => MemoryTurnSerializer.Deserialize("{\"TurnId\":\"other\"," + payload[1..], 1));
    }

    [Fact]
    // The initial format remains readable without depending on the SQL migration version.
    public void VersionOneFixture_RetainsAllFields()
    {
        const string fixture = """{"MessageId":"m","RunId":"r","Timestamp":"2026-09-27T12:00:00.1234567+03:00","Message":{"Role":2,"Content":"","ToolCallId":null,"ToolCalls":[{"Id":"c","Name":"tool","ArgumentsJson":"{}"}]}}""";
        var message = MemoryMessageSerializer.Deserialize(fixture, 1);
        Assert.Equal("m", message.MessageId);
        Assert.Equal("r", message.RunId);
        Assert.Equal(TimeSpan.FromHours(3), message.Timestamp.Offset);
        Assert.Equal("c", Assert.Single(message.Message.ToolCalls!).Id);
        Assert.Equal(fixture, MemoryMessageSerializer.Serialize(message));
    }

    [Theory]
    [InlineData("{}", 1)]
    [InlineData("null", 1)]
    [InlineData("not json", 1)]
    [InlineData("{}", 0)]
    [InlineData("{}", 2)]
    // Missing data and unsupported formats must fail instead of silently defaulting message fields.
    public void InvalidPayload_FailsExplicitly(string payload, int version)
    {
        Assert.Equal(MemoryStoreError.InvalidPayload,
            Assert.Throws<MemoryStoreException>(() => MemoryMessageSerializer.Deserialize(payload, version)).Error);
    }

    [Fact]
    // Invalid identifiers, roles, tool shapes and batches fail before entering a storage transaction.
    public void InvalidRequests_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => new MemoryMessage(" ", "run", default, new(ChatRole.User, "hello")));
        Assert.Throws<ArgumentException>(() => new MemoryMessage("id", "run", default, new((ChatRole)99, "hello")));
        Assert.Throws<ArgumentException>(() => new MemoryMessage("id", "run", default, new(ChatRole.Tool, "result")));
        Assert.Throws<ArgumentException>(() => new MemoryAppendRequest("key", 0, []));
        var message = new MemoryMessage("id", "run", default, new(ChatRole.User, "hello"));
        Assert.Throws<ArgumentException>(() => new MemoryAppendRequest("key", 0, [message, message]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryAppendRequest("key", -1, [message]));
    }

    [Theory]
    [InlineData("\"Extra\":true,")]
    [InlineData("\"MessageId\":\"duplicate\",")]
    // Unknown and duplicate JSON fields cannot silently override or discard persisted information.
    public void AmbiguousOrUnknownFields_AreRejected(string extra)
    {
        var original = MemoryMessageSerializer.Serialize(new("id", "run", DateTimeOffset.UnixEpoch, new(ChatRole.User, "text")));
        Assert.Equal(MemoryStoreError.InvalidPayload, Assert.Throws<MemoryStoreException>(() =>
            MemoryMessageSerializer.Deserialize("{" + extra + original[1..], 1)).Error);
    }

    [Fact]
    // Invalid surrogate sequences must fail before serializer replacement can corrupt message or request identity.
    public void MalformedUnicode_IsRejectedWithoutChangingValidUnicode()
    {
        Assert.Throws<ArgumentException>(() => new MemoryReference("\uD800"));
        Assert.ThrowsAny<ArgumentException>(() => new MemoryMessage("id", "run", default, new(ChatRole.User, "\uD800")));
        var message = new MemoryMessage("\U00010000", "run", DateTimeOffset.UnixEpoch, new(ChatRole.User, "😀"));
        var roundTrip = MemoryMessageSerializer.Deserialize(MemoryMessageSerializer.Serialize(message), 1);
        Assert.Equal(message, roundTrip);
    }
}
