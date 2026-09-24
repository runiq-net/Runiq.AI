using Microsoft.AspNetCore.Builder;
using Runiq.AI.CodexAgent.Agents;
using Runiq.AI.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRuniqServer(options => options.AddAgent(CodexAgent.Create()));

var app = builder.Build();

app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Codex Repository Assistant";
    options.Authentication(authentication => authentication.AllowAnonymous());
});

app.Run();
