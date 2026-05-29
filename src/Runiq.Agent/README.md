# Runiq.Agents

Agent runtime, tool execution, provider integration, and streaming primitives for Runiq.Net.

`Runiq.Agents` contains the core code-first agent model used by the Runiq runtime. Use it to define agents, attach strongly typed tools, configure model providers, and consume structured execution results or streaming execution events.

## Install

```powershell
dotnet add package Runiq.Agents --version 0.1.0-preview.1
```

## Basic Agent

```csharp
using Runiq.Agents;

var agent = new Agent(
    id: "weather-agent",
    name: "Weather Agent",
    instructions: "Answer weather questions using the available tools.",
    model: "openai/gpt-5",
    apiKey: configuration["OpenAI:ApiKey"]);
```

## Tool Example

```csharp
using Runiq.Agents.Tools;

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

Attach the tool during agent configuration:

```csharp
var agent = new Agent(
        id: "weather-agent",
        name: "Weather Agent",
        instructions: "Use tools when weather data is requested.",
        model: "openai/gpt-5",
        apiKey: configuration["OpenAI:ApiKey"])
    .AddTool<WeatherTool>();
```

## Related Packages

- `Runiq.Core` hosts agents and the embedded dashboard in ASP.NET Core.
- `Runiq.ContextSpaces` adds context and source reading primitives.
- `Runiq.Workflows` orchestrates agents in code-first workflows.

## Documentation

Full documentation is available at [runiq.net/docs](https://runiq.net/docs).
