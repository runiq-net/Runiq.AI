# Runiq.Workflows

Workflow orchestration primitives for Runiq.Net runtime and dashboard scenarios.

`Runiq.Workflows` lets you compose agents into code-first flows. It provides flow definitions, step builders, execution result models, validation primitives, and service registration for dashboard-visible workflow execution.

## Install

```powershell
dotnet add package Runiq.Workflows --version 0.1.0-preview.1
```

## Basic Workflow

```csharp
using Runiq.Workflows;
using Runiq.Workflows.Domain;

var flow = new Flow(
        id: "travel-planning-workflow",
        name: "Travel Planning Workflow")
    .Step<WeatherAgent>("weather")
        .OnSuccess("places")
        .OnFailureContinue("places")
    .Step<PlacesAgent>("places")
        .OnSuccess("planner")
        .OnFailureContinue("planner")
    .Step<PlannerAgent>("planner")
        .OnFailureStop()
    .Build();

builder.Services.AddRuniqWorkflows(options =>
{
    options.AddFlow(flow);
});
```

## Typical Use Cases

- Chain multiple agents into repeatable processes.
- Model success and failure transitions explicitly.
- Surface workflow metadata and execution results through the dashboard.
- Keep workflow definitions in code alongside application behavior.

## Related Packages

- `Runiq.Agents` provides the agent runtime primitives used by workflow steps.
- `Runiq.Core` hosts workflow dashboard endpoints.

## Documentation

Full documentation is available at [runiq.net/docs](https://runiq.net/docs).
