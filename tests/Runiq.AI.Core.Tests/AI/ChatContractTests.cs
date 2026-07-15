using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.Models;

namespace Runiq.AI.Core.Tests.AI;

/// <summary>
/// Verifies provider-neutral chat contract behavior.
/// </summary>
public sealed class ChatContractTests
{
    // Verifies that chat request validation preserves message order and accepts the required chat roles.
    [Fact]
    public void Validate_ShouldAcceptOrderedMessagesWithSupportedRoles()
    {
        var request = new ChatRequest(
            ModelReference.Parse("openai/gpt-5"),
            [
                new ChatMessage(ChatRole.System, "system"),
                new ChatMessage(ChatRole.User, "user"),
                new ChatMessage(ChatRole.Assistant, "assistant"),
                new ChatMessage(ChatRole.Tool, "tool", ToolCallId: "call-1")
            ]);

        var validated = request.Validate();

        Assert.Same(request, validated);
        Assert.Equal(
            [ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.Tool],
            request.Messages.Select(message => message.Role));
    }

    // Verifies that chat request validation rejects empty message batches before provider invocation.
    [Fact]
    public void Validate_ShouldRejectEmptyMessageList()
    {
        var request = new ChatRequest(
            ModelReference.Parse("openai/gpt-5"),
            []);

        Assert.Throws<ArgumentException>(() => request.Validate());
    }

    // Verifies that usage and finish reason models can represent completed non-streaming responses.
    [Fact]
    public void ChatResponse_ShouldCarryFinishReasonAndUsage()
    {
        var response = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "done"),
            ChatFinishReason.Stop,
            new ChatUsage(InputTokens: 2, OutputTokens: 3, TotalTokens: 5));

        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.Equal(5, response.Usage?.TotalTokens);
    }
}
