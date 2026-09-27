namespace Runiq.AI.Agents;

/// <summary>Reports safe counts for one model-context assembly, including a rejected invocation.</summary>
/// <param name="Invocation">The one-based attempted model invocation within this run.</param>
/// <param name="MaximumContextTokens">The effective total window.</param>
/// <param name="ResponseTokenReserve">The reserved response capacity.</param>
/// <param name="MandatoryPromptTokens">Instructions, tools, current input and active-turn continuation cost.</param>
/// <param name="SelectedEvidenceTokens">Formatted evidence cost, including message framing.</param>
/// <param name="SelectedHistoryTokens">Retained historical message cost.</param>
/// <param name="SelectedHistoryMessages">Number of retained historical messages.</param>
/// <param name="ExcludedHistoryMessages">Number of eligible messages excluded by limits or incomplete relationships.</param>
/// <param name="ExcludedEvidenceChunks">Number of accepted chunks excluded by context selection.</param>
/// <param name="MandatoryPromptOverflow">Whether mandatory content plus reserve exceeds the window.</param>
public sealed record AgentContextBudgetMetadata(int Invocation, int MaximumContextTokens, int ResponseTokenReserve,
    int MandatoryPromptTokens, int SelectedEvidenceTokens, int SelectedHistoryTokens,
    int SelectedHistoryMessages, int ExcludedHistoryMessages, int ExcludedEvidenceChunks, bool MandatoryPromptOverflow)
{
    /// <summary>Gets the accounting policy identifier; these counts are estimates, not provider tokenizer measurements.</summary>
    public string AccountingMode => Runtime.ContextTokenEstimator.AccountingMode;

    /// <summary>Gets the estimated cost of the assembled prompt, excluding the response reserve.</summary>
    public long EstimatedPromptTokens => (long)MandatoryPromptTokens + SelectedEvidenceTokens + SelectedHistoryTokens;
}
