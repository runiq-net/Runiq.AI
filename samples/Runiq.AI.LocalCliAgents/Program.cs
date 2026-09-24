using Microsoft.AspNetCore.Builder;
using Runiq.AI.LocalCliAgents.Agents;
using Runiq.AI.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRuniqServer(options =>
{
    options.AddAgent(QuickProjectAssistant.Create());
    options.AddAgent(CodexAgent.Create());
    options.AddAgent(ClaudeProjectAssistant.Create());
});

var app = builder.Build();

app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Local CLI Agents";
    options.Authentication(authentication => authentication.AllowAnonymous());
});

app.Run();
