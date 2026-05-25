using Runiq.ContextTravelGuide.Agents;
using Runiq.ContextTravelGuide.Context;
using Runiq.ContextTravelGuide.Tools;
using Runiq.Core;

var builder = WebApplication.CreateBuilder(args);

var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];

if (string.IsNullOrWhiteSpace(openAiApiKey))
{
    throw new InvalidOperationException(
        "OpenAI API key is missing. Set OpenAI:ApiKey in appsettings.Development.json, user secrets, or environment variables.");
}

builder.Services.AddRuniqServer(options =>
{
    options.AddTool<ServerTimeTool>();
    options.AddContextSpace(TravelPlanningContext.Create());

    options.AddAgent(TravelGuideAgent.Create(openAiApiKey));
    options.AddAgent(PlainAgent.Create(openAiApiKey));
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
    options.Title = "Runiq Context Travel Guide";
});

app.Run();
