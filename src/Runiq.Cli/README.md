# Runiq CLI

Runiq CLI creates ready-to-run ASP.NET Core projects for Runiq.Net. It sets up a Visual Studio friendly solution, adds Runiq packages through NuGet, wires the generated API project, and can include a small starter sample with agents, tools, prompts, workflows, Dashboard, and MCP.

## Installation

Install the CLI as a .NET tool:

```powershell
dotnet tool install --global Runiq.Cli --prerelease
```

Update an existing installation:

```powershell
dotnet tool update --global Runiq.Cli --prerelease
```

Verify the command is available:

```powershell
runiq --help
runiq --version
```

## Create A Project

Run:

```powershell
runiq init
```

Or provide the project name up front:

```powershell
runiq init MyRuniqApp
```

The wizard asks for:

- Project name, when it is not provided in the command
- Default AI provider
- Provider API key setup
- Starter sample code
- Dashboard
- MCP

Supported provider choices:

- OpenAI
- Azure OpenAI
- Ollama
- Anthropic

You can skip API key setup during generation and configure it later. If you choose to enter a key, the CLI stores it with .NET user secrets for the generated API project.

## Generated Output

For a project named `Sample04`, the CLI creates a solution like this:

```text
Sample04/
  Sample04.sln
  README.md
  src/
    Sample04.Api/
      Sample04.Api.csproj
      Program.cs
      Agents/
      Tools/
      Prompts/
      Workflows/
      Mcp/
  tests/
```

The starter artifacts live inside `src/{ProjectName}.Api`, so they are visible directly under the API project in Visual Studio.

Generated projects use NuGet package references only. They do not use local Runiq source or project references.

## Run The Generated API

After generation:

```powershell
cd MyRuniqApp
dotnet run
```

If Dashboard is enabled, open:

```text
https://localhost:{port}/dashboard
```

If MCP is enabled, the MCP endpoint is available at:

```text
https://localhost:{port}/mcp
```

The CLI prints the detected Dashboard and MCP URLs when the generated launch settings expose an application URL.

## Starter Sample

When starter sample code is enabled, the generated project includes a compact Travel Assistant scenario.

Try this in the Dashboard:

```text
Can you suggest a 2-day trip plan in Istanbul for 3 people?
```

The sample includes:

- `TravelPlannerAgent`
- `BudgetAdvisorAgent`
- `WeatherTool`
- `TripCostTool`
- `travel-planner.md`
- `budget-advisor.md`
- `IstanbulTripWorkflow.cs`
- `Mcp/README.md` when MCP is enabled

The conceptual flow is:

```text
TravelPlannerAgent
  -> WeatherTool
  -> BudgetAdvisorAgent
  -> TripCostTool
  -> final suggestion
```

The sample is intentionally small. It is meant to show where agents, tools, prompts, and workflows belong without adding large datasets or complex business logic.

## Sample Tools

`WeatherTool` returns a simple hardcoded weather response:

```text
Istanbul weather is mild and partly cloudy, around 18 C.
```

`TripCostTool` estimates a starter trip cost:

```text
peopleCount * days * 75 USD
```

For 3 people over 2 days, the estimate is:

```text
450 USD
```

## MCP

When MCP is enabled, the generated project references `Runiq.Mcp`, registers MCP services, and maps `/mcp`.

The starter tools include MCP metadata for:

- `weather.get`
- `trip.cost.estimate`

The generated `Mcp/README.md` explains the intended MCP tool examples.

## Generated Project README

The generated project README is user-facing. It gives prompt examples instead of documenting internal folder structure.

Example prompts:

```text
Can you suggest a 2-day trip plan in Istanbul for 3 people?
```

```text
What is the weather like in Istanbul for this sample trip?
```

```text
Estimate the trip cost for 3 people over 2 days.
```

```text
Create a short Istanbul itinerary and include a simple budget estimate.
```

## Notes

- The CLI currently provides the `init` command.
- The wizard is interactive.
- Generated projects are intended to run immediately after creation.
- Starter workflow output is a compile-safe C# outline, not a YAML file.
- The starter sample does not create helper, utility, service, CRM, order, or customer examples.

## Related Packages

Runiq.Net is modular. Generated projects may use:

- `Runiq.Core`
- `Runiq.Mcp`

Future project templates can add other Runiq packages as the generated scenario grows.
