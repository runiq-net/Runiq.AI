using Runiq.AI.Core;
using Runiq.AI.CLI.Agents.Agents;
using Runiq.AI.CLI.Agents.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IIncidentDataSource>(_ =>
    new JsonIncidentDataSource(Path.Combine(builder.Environment.ContentRootPath, "Fixtures")));

builder.Services.AddRuniqServer(options =>
{
    options.AddAgent(IncidentTriageAgents.CreateCodex());
    options.AddAgent(IncidentTriageAgents.CreateClaude());
});

var app = builder.Build();

app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Incident Triage Assistant";
    options.Authentication(authentication =>
    {
        // This anonymous dashboard is intentionally limited to the local sample experience.
        authentication.AllowAnonymous();
    });
});

app.Run();
