using Runiq.Core;
using Runiq.TeamTravelPlanner.Agents;
using Runiq.TeamTravelPlanner.Teams;

var builder = WebApplication.CreateBuilder(args);

var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];

builder.Services.AddRuniqServer(options =>
{
    options.AddAgent(TravelPlanningAgents.CreateWeatherAgent(openAiApiKey));
    options.AddAgent(TravelPlanningAgents.CreateBudgetAgent(openAiApiKey));
    options.AddAgent(TravelPlanningAgents.CreatePlacesAgent(openAiApiKey));
    options.AddAgent(TravelPlanningAgents.CreatePlannerAgent(openAiApiKey));

    options.AddTeam(TravelPlanningTeam.Create());
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
    options.Title = "Runiq Team Travel Planner";
});

app.Run();
