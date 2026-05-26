namespace Runiq.Workflows;

/// <summary>
/// Workflow tanımlarının temel yapısal kurallarını doğrular.
/// </summary>
public static class WorkflowValidator
{
    /// <summary>
    /// Verilen workflow tanımını doğrular.
    /// </summary>
    public static WorkflowValidationResult Validate(Workflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var errors = new List<string>();

        if (workflow.Steps.Count == 0)
        {
            errors.Add("Workflow must contain at least one step.");
            return WorkflowValidationResult.Failure(errors);
        }

        var duplicateStepIds = workflow.Steps
            .GroupBy(step => step.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        foreach (var duplicateStepId in duplicateStepIds)
        {
            errors.Add($"Workflow contains duplicate step id '{duplicateStepId}'.");
        }

        var stepIds = workflow.Steps
            .Select(step => step.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var step in workflow.Steps)
        {
            if (!string.IsNullOrWhiteSpace(step.SuccessStepId) && !stepIds.Contains(step.SuccessStepId))
            {
                errors.Add($"Step '{step.Id}' has unknown success target '{step.SuccessStepId}'.");
            }

            if (!string.IsNullOrWhiteSpace(step.FailureStepId) && !stepIds.Contains(step.FailureStepId))
            {
                errors.Add($"Step '{step.Id}' has unknown failure target '{step.FailureStepId}'.");
            }
        }


        return errors.Count == 0
            ? WorkflowValidationResult.Success()
            : WorkflowValidationResult.Failure(errors);
    }
}