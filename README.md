# Runiq.Net

[![CI](https://github.com/runiq-net/Runiq.net/actions/workflows/ci.yml/badge.svg)](https://github.com/runiq-net/Runiq.net/actions/workflows/ci.yml)
![Tests](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/runiq-net/Runiq.net/main/badges/tests.json)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![NuGet Version](https://img.shields.io/nuget/vpre/Runiq.Core?label=nuget)
![License](https://img.shields.io/badge/license-MIT-blue)

Runiq.Net is a code-first agent runtime for .NET applications.

It gives ASP.NET Core teams a native way to define AI agents in C#, attach strongly typed tools, stream model responses, connect reusable context sources, orchestrate workflows, and inspect runtime activity through an embedded dashboard.

> Preview package: APIs may evolve before the first stable release.

## Packages

| Package | Purpose |
| --- | --- |
| `Runiq.Agents` | Agent definitions, tool execution, provider integration, streaming events, and execution results. |
| `Runiq.ContextSpaces` | Context spaces, source readers, skill discovery, and document preview primitives. |
| `Runiq.Core` | ASP.NET Core hosting extensions, runtime endpoints, and the embedded dashboard. |
| `Runiq.Workflows` | Code-first workflow orchestration primitives for agent runtime and dashboard scenarios. |

## Installation

Install the packages you need:

```powershell
dotnet add package Runiq.Core --prerelease
dotnet add package Runiq.Agents --prerelease
dotnet add package Runiq.ContextSpaces --prerelease
dotnet add package Runiq.Workflows --prerelease
```

For most ASP.NET Core applications, start with `Runiq.Core`; it references the runtime pieces needed to host agents and the dashboard.

## Quickstart

Register Runiq and define an agent:

```csharp
using Runiq.Agents;
using Runiq.Core;

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
});
```

Run the application and open `/dashboard` to inspect registered agents, test conversations, and review runtime activity.

## Tool Example

Tools are plain C# types with strongly typed input and output:

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

Source code and issue tracking are available on [GitHub](https://github.com/runiq-net/Runiq.net).

## License

Runiq.Net is licensed under the [MIT License](LICENSE).
