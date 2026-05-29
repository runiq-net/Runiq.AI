# Runiq.Core

Embedded ASP.NET Core runtime and dashboard hosting layer for Runiq.Net agents.

`Runiq.Core` wires Runiq into ASP.NET Core applications. It provides service registration, runtime endpoints, dashboard hosting, agent metadata endpoints, context browsing endpoints, and workflow dashboard endpoints.

## Install

```powershell
dotnet add package Runiq.Core --version 0.1.0-preview.1
```

## Quickstart

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

app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Dashboard";
});
```

Open `/dashboard` in your application to inspect registered agents and runtime activity.

## What It Provides

- ASP.NET Core service registration for Runiq.
- Embedded dashboard static asset hosting.
- Runtime and dashboard metadata endpoints.
- Context space source and skill document endpoints.
- Workflow dashboard endpoint integration.

## Related Packages

- `Runiq.Agents` defines agents, tools, provider integration, and stream events.
- `Runiq.ContextSpaces` provides context and source reading primitives.
- `Runiq.Workflows` provides workflow orchestration primitives.

## Documentation

Full documentation is available at [runiq.net/docs](https://runiq.net/docs).
