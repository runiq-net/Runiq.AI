using Runiq.Workflows.Validations;
using Runiq.Workflows.Models;
using Runiq.Workflows.Domain;
using Runiq.Workflows.Infrastructure;

namespace Runiq.Workflows;

/// <summary>
/// Configures flow definitions registered through AddRuniqWorkflows.
/// </summary>
public sealed class FlowOptions
{
    private readonly FlowCatalog catalog = new();

    public IReadOnlyList<Flow> Flows => catalog.Flows;

    public FlowOptions AddFlow(Flow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var validationResult = FlowDefinitionValidator.Validate(flow);

        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(
                $"Flow '{flow.Id}' is invalid:{Environment.NewLine}" +
                string.Join(Environment.NewLine, validationResult.Errors));
        }

        catalog.AddFlow(flow);

        return this;
    }
}
