# Runiq.AI.Agents

![NuGet Version](https://img.shields.io/nuget/vpre/Runiq.AI.Agents?label=nuget)

Code-first AI agents for .NET.

`Runiq.AI.Agents` provides the core agent model for Runiq AI. Use it to define agents in C#, attach strongly typed tools, configure model providers, and build agent-based applications with structured execution support.

## Why Runiq.AI.Agents?

Runiq.AI.Agents is designed for .NET developers who want to build AI agents without leaving the C# ecosystem.

It focuses on:

- Code-first agent definitions
- Strongly typed tool execution
- Provider-aware model configuration
- Runtime-friendly agent composition
- Streaming and structured execution support
- Integration with the broader Runiq AI platform

## Install

```powershell
dotnet add package Runiq.AI.Agents --prerelease
```

## Create an Agent

```csharp
using Runiq.AI.Agents;

var agent = new Agent(
    id: "weather-agent",
    name: "Weather Agent",
    instructions: "Answer weather questions using the available tools.",
    model: "openai/gpt-5",
    apiKey: configuration["OpenAI:ApiKey"]);
```

An agent contains the basic runtime definition:

- `id`: stable identifier used by the runtime
- `name`: human-readable agent name
- `instructions`: system-level behavior definition
- `model`: target model identifier
- `apiKey`: provider credential

## Add a Tool

Tools allow agents to call strongly typed C# code.

```csharp
using Runiq.AI.Agents.Tools;

[RuniqTool("get_weather", "Gets the current weather for a city.")]
public sealed class WeatherTool : IRuniqTool<WeatherInput, WeatherOutput>
{
    public Task<WeatherOutput> ExecuteAsync(
        WeatherInput input,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            new WeatherOutput(input.City, "Clear"));
    }
}

public sealed record WeatherInput(string City);

public sealed record WeatherOutput(string City, string Condition);
```

Attach the tool to the agent:

```csharp
var agent = new Agent(
        id: "weather-agent",
        name: "Weather Agent",
        instructions: "Use tools when weather data is requested.",
        model: "openai/gpt-5",
        apiKey: configuration["OpenAI:ApiKey"])
    .AddTool<WeatherTool>();
```

## RAG Execution and Grounding Policies

Configure framework-owned retrieval and grounding through `UseRag`:

```csharp
using Runiq.AI.Agents.Configuration;

var agent = new Agent(
        id: "policy-assistant",
        name: "Policy Assistant",
        instructions: "Answer employee policy questions.",
        model: "openai/gpt-5",
        apiKey: configuration["OpenAI:ApiKey"])
    .UseRag(rag =>
    {
        rag.IndexName = "company-policies";
        rag.Mode = RagExecutionMode.Required;
        rag.NoContextBehavior = RagNoContextBehavior.ReturnNotFound;
        rag.MinimumRelevanceScore = 0.75;
    });
```

`Open` is the default execution mode. It uses accepted document context when available and, with the default
`AnswerNormally` behavior, preserves normal agent execution when retrieval returns no accepted context.
`Grounded` treats accepted documents as the primary source, requires unsupported information to be separated,
forbids invented company policies, and requires conflicting sources to be identified. `Required` constrains the
answer to accepted context and therefore cannot be combined with `AnswerNormally`; that combination fails during
configuration and is validated again before retrieval at runtime.

No-context behavior is selected independently for every valid combination:

| Behavior | Outcome when no context is accepted |
|---|---|
| `AnswerNormally` | Invokes the model without accepted context. Invalid with `Required`. |
| `ReturnNotFound` | Returns a successful framework-owned not-found response and skips the model. |
| `FailExecution` | Returns `RagContextUnavailable` as an execution failure and skips the model. |

`MinimumRelevanceScore` is optional and vector-store specific. Candidates below the threshold are excluded from
the accepted context. A successful empty retrieval reports `NoResults`; candidates rejected by the threshold
report `BelowRelevanceThreshold`. The current retriever contract cannot distinguish an empty index from a valid
query with zero matches, so it does not claim that distinction. Retrieval exceptions remain
`RagRetrievalFailed` and are never converted into a no-context result or normal answer.

The runtime emits framework grounding rules as authoritative instructions and sends accepted document text only
inside a separate `<untrusted-external-context>` user message. Document instructions are treated as untrusted
data and cannot be promoted into system, developer, agent, or framework instruction authority. This boundary is
a prompt-injection mitigation, not a guarantee that a model can never be manipulated.

`AgentExecutionResult.Rag`, terminal `AgentExecutionEvent.Rag`, and Agent Chat result/SSE terminal events expose
the applied mode, accepted-context status, applied no-context behavior and reason, whether model invocation was
skipped, and whether the framework constrained the answer to accepted context. `IsAnswerGrounded` reports the
applied framework policy; it is not independent semantic verification of model output.

## Tool Design

A Runiq tool is a regular C# class that implements:

```csharp
IRuniqTool<TInput, TOutput>
```

This gives you:

- Strongly typed input models
- Strongly typed output models
- Testable business logic
- Clean separation between agent behavior and application code

## Typical Use Cases

Use `Runiq.AI.Agents` when you want to build:

- AI assistants for .NET applications
- Tool-using agents
- Domain-specific agents
- Agent workflows
- Internal automation agents
- Dashboard-observable agent runtimes
- MCP-compatible agent experiences

## Related Packages

Runiq AI is modular. `Runiq.AI.Agents` can be used together with other Runiq packages:

| Package | Purpose |
|---|---|
| `Runiq.AI.Core` | Hosts agents and the embedded dashboard in ASP.NET Core |
| `Runiq.AI.Rag` | Owns document-based retrieval, vector indexes, and RAG query primitives |
| `Runiq.AI.Workflows` | Orchestrates agents in code-first workflows |
| `Runiq.AI.Mcp` | Exposes ASP.NET Core applications through MCP-compatible tools |

## Documentation

Full documentation is available at:

https://runiq.net/docs

## Status

Runiq AI is currently in preview.

APIs may change before the first stable release.

The main direction is clear:

> Build code-first AI agents, tools, workflows, context sources, MCP endpoints, and dashboards for .NET applications.

## License

MIT
