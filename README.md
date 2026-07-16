# Runiq AI

[![CI](https://github.com/runiq-net/Runiq.AI/actions/workflows/ci.yml/badge.svg)](https://github.com/runiq-net/Runiq.AI/actions/workflows/ci.yml)
![Tests](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/runiq-net/Runiq.AI/main/badges/tests.json)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![NuGet Version](https://img.shields.io/nuget/vpre/Runiq.AI.Core?label=nuget)
![License](https://img.shields.io/badge/license-MIT-blue)

Runiq AI is a code-first agent runtime for .NET applications.

It gives ASP.NET Core teams a native way to define AI agents in C#, attach strongly typed tools, stream model responses, use RAG retrieval, orchestrate workflows, and inspect runtime activity through an embedded dashboard.

> Preview package: APIs may evolve before the first stable release.

## Packages

| Package | Purpose |
| --- | --- |
| `Runiq.AI.Agents` | Agent definitions, tool execution, provider integration, streaming events, and execution results. |
| `Runiq.AI.Core` | ASP.NET Core hosting extensions, runtime endpoints, and the embedded dashboard. |
| `Runiq.AI.Rag` | Document chunking, embeddings, vector storage, and retrieval for document-based knowledge. |
| `Runiq.AI.Workflows` | Code-first workflow orchestration primitives for agent runtime and dashboard scenarios. |

## Installation

Install the packages you need:

```powershell
dotnet add package Runiq.AI.Core --prerelease
dotnet add package Runiq.AI.Agents --prerelease
dotnet add package Runiq.AI.Workflows --prerelease
```

For most ASP.NET Core applications, start with `Runiq.AI.Core`; it references the runtime pieces needed to host agents and the dashboard.

## Quickstart

Register Runiq and define an agent:

```csharp
using Runiq.AI.Agents;
using Runiq.AI.Core;

builder.Services.AddRuniqServer(options =>
{
    options.AddAgent(new Agent(
        id: "weather-agent",
        name: "Weather Agent",
        instructions: "Answer weather questions using the available tools.",
        model: "openai/gpt-5",
        apiKey: builder.Configuration["OpenAI:ApiKey"]));
});
```

Map the dashboard:

```csharp
app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Dashboard";
    options.Authentication(auth =>
    {
        // Demo or sample only. Do not use AllowAnonymous in production.
        auth.AllowAnonymous();
    });
});
```

Run the application and open `/dashboard` to inspect registered agents, test conversations, and review runtime activity.

## RAG Grounding Policies

RAG-enabled agents can choose an explicit execution mode and no-context outcome:

```csharp
using Runiq.AI.Agents.Configuration;

options.AddAgent(new Agent(
        id: "policy-assistant",
        name: "Policy Assistant",
        instructions: "Answer employee policy questions.",
        model: "openai/gpt-5",
        apiKey: builder.Configuration["OpenAI:ApiKey"])
    .UseRag(rag =>
    {
        rag.IndexName = "company-policies";
        rag.Mode = RagExecutionMode.Grounded;
        rag.NoContextBehavior = RagNoContextBehavior.ReturnNotFound;
    }));
```

The default is `Open` with `AnswerNormally`, preserving normal model behavior when retrieval succeeds without
accepted context. `Grounded` makes documents the primary source; `Required` allows answers only from accepted
context and must use `ReturnNotFound` or `FailExecution`. Retrieval failures remain failures in every mode.
See the [Agents package guide](src/Runiq.AI.Agents/README.md#rag-execution-and-grounding-policies) for the complete
policy matrix, relevance acceptance, trust boundary, and structured runtime outcome.

## Tool Example

Tools are plain C# types with strongly typed input and output:

```csharp
using Runiq.AI.Agents.Tools;

[RuniqTool("get_weather", "Gets the current weather for a city.")]
public sealed class WeatherTool : IRuniqTool<WeatherInput, WeatherOutput>
{
    public Task<WeatherOutput> ExecuteAsync(
        WeatherInput input,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new WeatherOutput(input.City, "Clear"));
    }
}

public sealed record WeatherInput(string City);

public sealed record WeatherOutput(string City, string Condition);
```

Attach the tool to an agent:

```csharp
options.AddAgent(new Agent(
        id: "weather-agent",
        name: "Weather Agent",
        instructions: "Use tools when weather data is requested.",
        model: "openai/gpt-5",
        apiKey: builder.Configuration["OpenAI:ApiKey"])
    .AddTool<WeatherTool>());
```

## Documentation

Full documentation, guides, and examples are available at [runiq.net/docs](https://runiq.net/docs).

## Repository

Source code and issue tracking are available on [GitHub](https://github.com/runiq-net/Runiq.AI).

## License

Runiq AI is licensed under the [MIT License](LICENSE).
