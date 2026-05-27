# Runiq Workflow Travel Planner

This sample demonstrates deterministic workflow execution in Runiq.Net.

The registered workflow is:

- `Travel Planning Workflow`: `weather -> places -> planner`

Each workflow step runs an agent. Tools remain attached to agents and are invoked only inside the agent execution.

## Run

From the repository root:

```powershell
dotnet run --project samples/Runiq.WorkflowTravelPlanner/Runiq.WorkflowTravelPlanner.csproj
```

Dashboard:

```text
http://localhost:5127/dashboard
```

Workflow metadata:

```text
http://localhost:5127/dashboard/api/workflows
```

## Example Prompt

```text
Istanbul icin 2 kisilik, 1 gunluk pratik tarihi gezi plani hazirla. Cok yorucu olmasin.
```

Expected workflow trace: Weather Agent, Places Agent, then Planner Agent.

## Configuration

The agents use the repository's current sample model naming style, `openai/gpt-5`. To execute model calls, configure `OpenAI:ApiKey` through user secrets, `appsettings.Development.json`, or environment variables:

```json
{
  "OpenAI": {
    "ApiKey": "..."
  }
}
```
