using Microsoft.Extensions.Options;
using Runiq.Agents;
using Runiq.Agents.Configuration;
using Runiq.Agents.Tools;
using Runiq.Core;
using SampleApp.MyTools;



var builder = WebApplication.CreateBuilder(args);

var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];

if (string.IsNullOrWhiteSpace(openAiApiKey))
{
    throw new InvalidOperationException(
        "OpenAI API key is missing. Set OpenAI:ApiKey in appsettings.Development.json, user secrets, or environment variables.");
}

builder.Services.AddRuniqServer(opt =>
{

    opt.AddTool<ServerTimeTool>();

    opt.AddTool<WeatherTool>();

    opt.AddAgent(new Agent(
    id: "broken-compatible-agent",
    name: "Broken Compatible Agent",
    instructions: "You are a test assistant.",
    model: "openrouter/openai/gpt-4o-mini",
    apiKey: "wrong-key"));

    opt.AddAgent(new Agent(
    id: "travel-agent",
    name: "Travel Agent",
    instructions: """
    You are a practical city trip planning assistant.

    When the user asks for a short city trip plan:
    1. First use the weather tool to get the current weather for the requested city.
    2. Then use the trip_plan tool.
    3. When calling trip_plan, pass the city, requested duration, weather condition, and temperature from the weather result.
    4. Combine both tool results into a short, practical final answer.

    Do not answer from memory when these tools are available.
    """,
    model: "openai/gpt-5",
    apiKey: builder.Configuration["OpenAI:ApiKey"])
    .AddTool<WeatherTool>()
    .AddTool<TripPlanTool>());

});



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRuniqDashboard(
    opt =>
    {
        opt.Path = "/dashboard";
        opt.Title = "My Runiq Dashboard";
    }
    );


app.Run();

