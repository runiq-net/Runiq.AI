using System.Text.Json;
using Runiq.AI.Core.AI.Chat;

namespace Runiq.AI.Agents.Runtime;

/// <summary>Provides one deterministic, explicitly approximate accounting policy for every prompt contributor.</summary>
internal static class ContextTokenEstimator
{
    internal const string AccountingMode = "EstimatedUnicodeRuns";

    // Count Unicode letter/digit runs and individual punctuation marks, not provider tokenizer tokens.
    internal static int EstimateText(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        var count = 0;
        var inWord = false;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (!inWord) count++;
                inWord = true;
            }
            else
            {
                inWord = false;
                if (!char.IsWhiteSpace(character)) count++;
            }
        }
        return count;
    }

    // Four framing units plus the role; call/result framing is counted separately from exact payload text.
    internal static int EstimateMessage(ChatMessage message) => message.Continuation is { } continuation
        // Provider output replaces the assistant wire message. Reported output includes hidden reasoning;
        // retain it as a floor rather than treating an encrypted blob as one Unicode word.
        ? checked(5 + Math.Max(EstimateText(continuation.Payload.GetRawText()), continuation.OutputTokens ?? 0))
        : checked(5 + EstimateText(message.Content) +
        (message.ToolCallId is null ? 0 : 2 + EstimateText(message.ToolCallId)) +
        (message.ToolCalls?.Sum(call => checked(4 + EstimateText(call.Id) + EstimateText(call.Name) + EstimateText(call.ArgumentsJson))) ?? 0));

    internal static int EstimateTools(IReadOnlyList<ChatToolDefinition> tools) =>
        tools.Count == 0 ? 0 : EstimateText(JsonSerializer.Serialize(tools));

    internal static int EstimateEvidence(string? formattedEvidence) =>
        formattedEvidence is null ? 0 : EstimateMessage(new(ChatRole.User, formattedEvidence));
}
