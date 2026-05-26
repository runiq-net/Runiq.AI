namespace Runiq.Teams.Models.Execution;

/// <summary>
/// Agent team yürütmesinde hangi üyelerin hangi sırayla çalıştırılacağını tanımlayan planı temsil eder.
/// </summary>
public sealed record TeamExecutionPlan
{
    /// <summary>
    /// Yeni bir team execution planı oluşturur.
    /// </summary>
    public TeamExecutionPlan(
        IReadOnlyList<TeamExecutionPlanStep> steps,
        string finalAgentId,
        string? planningSummary = null)
    {
        ArgumentNullException.ThrowIfNull(steps);

        if (steps.Count == 0)
        {
            throw new ArgumentException("Plan must have at least one step.", nameof(steps));
        }

        if (string.IsNullOrWhiteSpace(finalAgentId))
        {
            throw new ArgumentException("Final agent id cannot be empty.", nameof(finalAgentId));
        }

        var normalizedFinalAgentId = finalAgentId.Trim();

        if (!steps.Any(step =>
                string.Equals(step.AgentId, normalizedFinalAgentId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "Final agent id must match one of the plan steps.",
                nameof(finalAgentId));
        }

        Steps = steps
            .OrderBy(step => step.Order)
            .ToArray();
        FinalAgentId = normalizedFinalAgentId;
        PlanningSummary = string.IsNullOrWhiteSpace(planningSummary)
            ? null
            : planningSummary.Trim();
    }

    /// <summary>
    /// Plan kapsamında çalıştırılacak agent adımlarıdır.
    /// </summary>
    public IReadOnlyList<TeamExecutionPlanStep> Steps { get; }

    /// <summary>
    /// Kullanıcıya dönecek final cevabı üretecek agent kimliğidir.
    /// </summary>
    public string FinalAgentId { get; }

    /// <summary>
    /// Planın neden bu şekilde oluşturulduğunu özetleyen açıklamadır.
    /// </summary>
    public string? PlanningSummary { get; }
}
