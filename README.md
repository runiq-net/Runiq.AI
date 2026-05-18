# Runiq

## Code-first AI agents for .NET.

Runiq is a .NET framework for building AI agents with strongly typed tools, provider-based model execution, streaming responses, and an embedded runtime Dashboard.
It is designed for .NET teams who want to add agentic capabilities to real ASP.NET Core applications without introducing a separate low-code platform or leaving the C# ecosystem.
Runiq lets you define agents in code, attach typed tools, stream model responses, inspect tool calls, and test agent behavior through a dashboard served by your own application.

## Why Runiq?

Modern AI agent frameworks are often designed around JavaScript, external runtimes, or low-code platforms. Runiq takes a different path.
Runiq is built for teams that already use .NET, ASP.NET Core, dependency injection, strongly typed models, and production-oriented backend architecture.
With Runiq, agents are not created through YAML files, JSON configuration, or a visual designer. They are defined in C# and registered at application startup.

Runiq focuses on:
- Code-first agent definitions
- Strongly typed C# tools
- ASP.NET Core native hosting
- Provider-based model execution
- Streaming responses
- Tool-call visibility
- Embedded runtime Dashboard
- Production-oriented developer experience

Runiq is not a no-code agent builder.
It is a developer-first framework for building, running, and observing AI agents inside .NET applications.

## What you can build
### Product agents
Embed AI agents into SaaS products, admin panels, internal platforms, or customer-facing applications.

### Internal copilots
Build assistants that understand your business domain and help teams work with internal data, APIs, and processes.

### Tool-using agents
Let agents call strongly typed C# tools such as weather services, CRM clients, document processors, search services, or internal business APIs.

### Agent playgrounds
Test agent behavior, stream model responses, inspect tool calls, and review runtime metadata through the embedded Dashboard.

### Workflow-driven AI processes
Coordinate agents, tools, and business logic through code-first workflows as the framework evolves.

## Quickstart
Register Runiq in your ASP.NET Core application:

```csharp
builder.Services.AddRuniqServer(options =>
{
    options.AddAgent(new Agent(
        id: "weather-agent",
        name: "Weather Agent",
        instructions: """
        You are a weather assistant.

        When the user asks for weather information,
        use the available weather tool and answer clearly.
        """,
        model: "openai/gpt-5",
        apiKey: builder.Configuration["OpenAI:ApiKey"]));
});
```
Map the Dashboard and runtime endpoints:

```csharp
app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Dashboard";
});
```
Run the application and open:

```csharp
/dashboard
```
From the Dashboard, you can inspect registered agents, open the agent playground, send messages, stream responses, and view tool calls.

