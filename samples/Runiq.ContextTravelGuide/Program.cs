using Runiq.ContextTravelGuide.Agents;
using Runiq.ContextTravelGuide.Context;
using Runiq.ContextTravelGuide.Services;
using Runiq.ContextTravelGuide.Tools;
using Runiq.Core;
using Runiq.Mcp.DependencyInjection;
using Runiq.Mcp.Routing;

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


builder.Services.AddScoped<ITravelSummaryService, TravelSummaryService>();

builder.Services.AddRuniqMcp();


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
    options.Authentication(auth =>
    {
        // Demo/sample only. Do not use AllowAnonymous in production.
        auth.AllowAnonymous();
    });
});

app.MapRuniqMcp();

app.Run();
