using Runiq.Core;
using Runiq.WorkflowTravelPlanner.Agents;
using Runiq.WorkflowTravelPlanner.Flows;
using Runiq.Workflows;

var builder = WebApplication.CreateBuilder(args);

var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];

builder.Services.AddRuniqServer(options =>
{
    options.AddAgent(WeatherAgent.Create(openAiApiKey));
    options.AddAgent(PlacesAgent.Create(openAiApiKey));
    options.AddAgent(PlannerAgent.Create(openAiApiKey));
});

builder.Services.AddRuniqWorkflows(options =>
{
    options.AddFlow(TravelPlanningFlow.Create());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Flow Travel Planner";
});

app.Run();
