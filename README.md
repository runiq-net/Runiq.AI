# \# Runiq.Net

# 

# Runiq.Net is a code-first .NET framework for building AI agents, strongly typed tools, and an embedded developer dashboard for runtime visibility and playground testing.

# 

# It is designed for .NET teams who want an agent framework that feels native to ASP.NET Core, keeps definitions in C#, and exposes a developer-friendly dashboard without turning the system into a low-code workflow designer.

# 

# \## Why Runiq.Net?

# 

# Most AI agent frameworks are either Python/JavaScript-first or depend heavily on external configuration and workflow layers.

# 

# Runiq.Net focuses on:

# 

# \- Code-first agent definitions

# \- Strongly typed tool execution

# \- ASP.NET Core native hosting

# \- Runtime metadata visibility

# \- Embedded Dashboard UI

# \- Agent playground

# \- Streaming chat responses

# \- Tool-call visibility

# \- Provider/model based agent execution

# 

# \## Current Status

# 

# Runiq.Net is currently in active development.

# 

# Implemented so far:

# 

# \- Agent registration

# \- Provider/model configuration

# \- OpenAI Responses API integration

# \- OpenAI-compatible provider support

# \- Streaming chat endpoint

# \- Strongly typed tool execution

# \- OpenAI function-call continuation flow

# \- Runtime metadata API

# \- Embedded Dashboard UI

# \- Agent playground

# \- Collapsible tool-call cards in chat UI

# 

# \## Core Concepts

# 

# \### Agents

# 

# Agents are defined in C#.

# 

# &#x20;   opt.AddAgent(new Agent(

# &#x20;       id: "weather-agent",

# &#x20;       name: "Weather Agent",

# &#x20;       instructions: "You answer weather-related questions using the weather tool.",

# &#x20;       model: "openai/gpt-5",

# &#x20;       apiKey: configuration\["OpenAI:ApiKey"]

# &#x20;   ));

# 

# An agent contains the core runtime information needed to execute a model request:

# 

# \- Stable agent id

# \- Display name

# \- Instructions / system prompt

# \- Provider and model reference

# \- Optional API key

# \- Attached tools

# 

# \### Tools

# 

# Tools are strongly typed C# classes. A tool declares its input and output models, and Runiq executes it when the model requests a function call.

# 

# &#x20;   \[RuniqTool(

# &#x20;       name: "weather",

# &#x20;       description: "Gets current weather information for a city.")]

# &#x20;   public sealed class WeatherTool : IRuniqTool<WeatherInput, WeatherOutput>

# &#x20;   {

# &#x20;       /// <inheritdoc />

# &#x20;       public Task<WeatherOutput> ExecuteAsync(

# &#x20;           WeatherInput input,

# &#x20;           CancellationToken cancellationToken = default)

# &#x20;       {

# &#x20;           ArgumentNullException.ThrowIfNull(input);

# 

# &#x20;           if (string.IsNullOrWhiteSpace(input.City))

# &#x20;           {

# &#x20;               throw new ArgumentException(

# &#x20;                   "City is required.",

# &#x20;                   nameof(input));

# &#x20;           }

# 

# &#x20;           var city = input.City.Trim();

# 

# &#x20;           var output = new WeatherOutput(

# &#x20;               City: city,

# &#x20;               TemperatureCelsius: 23,

# &#x20;               Condition: "Sunny",

# &#x20;               Summary: $"{city} için hava güneşli ve yaklaşık 23 derece.");

# 

# &#x20;           return Task.FromResult(output);

# &#x20;       }

# &#x20;   }

# 

# &#x20;   public sealed record WeatherInput(string City);

# 

# &#x20;   public sealed record WeatherOutput(

# &#x20;       string City,

# &#x20;       int TemperatureCelsius,

# &#x20;       string Condition,

# &#x20;       string Summary);

# 

# \### Dashboard

# 

# Runiq Dashboard is embedded into the host ASP.NET Core application.

# 

# &#x20;   app.UseRuniqDashboard(opt =>

# &#x20;   {

# &#x20;       opt.Path = "/dashboard";

# &#x20;       opt.Title = "My Runiq Dashboard";

# &#x20;   });

# 

# The dashboard provides:

# 

# \- Agent list

# \- Agent overview

# \- Runtime behavior view

# \- Agent playground

# \- Streaming chat

# \- Tool-call visualization

# 

# \## Example Tool Flow

# 

# The current agent tool flow works as follows:

# 

# 1\. The user sends a message from the Dashboard playground.

# 2\. The frontend opens a streaming chat request.

# 3\. Runiq sends the agent instructions, model, and tool schema to the provider.

# 4\. The model emits a function call.

# 5\. Runiq parses the function call.

# 6\. Runiq executes the matching strongly typed .NET tool.

# 7\. The tool result is streamed to the UI as a typed tool event.

# 8\. Runiq sends the tool output back to the model using continuation.

# 9\. The model generates the final assistant response.

# 10\. The Dashboard renders the tool call as a collapsible card and displays the final response below it.

# 

# \## Streaming Event Model

# 

# Runiq streams typed agent execution events to the Dashboard.

# 

# Current event types include:

# 

# \- `assistant\_delta`

# \- `tool\_call\_started`

# \- `tool\_call\_completed`

# \- `tool\_call\_failed`

# 

# Tool events can include:

# 

# \- Tool call id

# \- Tool name

# \- Arguments JSON

# \- Output JSON

# \- Error code

# \- Error message

# 

# The frontend does not append raw tool event payloads into the assistant message. Only assistant deltas are appended to message content. Tool events are rendered separately as tool cards.

# 

# \## Design Principles

# 

# Runiq.Net follows a few strict design principles:

# 

# \- Code-first, not YAML-first

# \- C# definitions over external workflow configuration

# \- Runtime visibility, not low-code workflow design

# \- Strong typing over dynamic payload handling

# \- ASP.NET Core native hosting

# \- Developer experience first

# \- Startup-time registration

# \- Read-only runtime metadata

# \- No runtime mutation of registered agents or tools

# 

# \## Running Locally

# 

# Build the solution:

# 

# &#x20;   dotnet build Runiq.Net.slnx

# 

# Run tests:

# 

# &#x20;   dotnet test Runiq.Net.slnx

# 

# Build the Dashboard client:

# 

# &#x20;   cd src/Runiq.Dashboard.Client

# &#x20;   npm install

# &#x20;   npm run build

# 

# Run the sample application:

# 

# &#x20;   dotnet run --project samples/SampleApp

# 

# Open the Dashboard:

# 

# &#x20;   http://localhost:5241/dashboard

# 

# \## Recommended Development Check

# 

# Before committing runtime or dashboard changes, run:

# 

# &#x20;   dotnet test Runiq.Net.slnx

# 

# And for Dashboard client changes:

# 

# &#x20;   cd src/Runiq.Dashboard.Client

# &#x20;   npm run build

# 

# \## Roadmap

# 

# Near-term roadmap:

# 

# \- Tool card UI polish

# \- Cleaner execution logging with ILogger

# \- Real weather sample using Open-Meteo

# \- Tool continuation tests

# \- Tool error-flow tests

# \- More provider adapters

# \- Agent memory foundation

# \- Workflow foundation

# \- Runtime trace visibility

# 

# \## Project Direction

# 

# Runiq.Net aims to bring a Mastra-like developer experience to the .NET ecosystem while staying idiomatic to ASP.NET Core and C#.

# 

# It is not intended to become a drag-and-drop workflow builder or a no-code automation platform. The goal is to keep agent, tool, workflow, and runtime definitions in code while providing enough visibility through the Dashboard to make development and debugging practical.

# 

# \## License

# 

# TBD



