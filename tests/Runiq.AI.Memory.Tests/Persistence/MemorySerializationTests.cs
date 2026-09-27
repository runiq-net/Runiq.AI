using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.Serialization;

namespace Runiq.AI.Memory.Tests.Persistence;

public sealed class MemorySerializationTests
{
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
