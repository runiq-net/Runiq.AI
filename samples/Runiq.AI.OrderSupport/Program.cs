using Runiq.AI.Core;
using Runiq.AI.OrderSupport.Agents;
using Runiq.AI.OrderSupport.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<OrderSampleData>();

var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];
builder.Services.AddRuniqServer(options =>
{
    options.AddAgent(OrderStatusAgent.Create(openAiApiKey));
    options.AddAgent(ReturnEligibilityAgent.Create(openAiApiKey));
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
    options.Title = "Runiq Order Support";
    options.Authentication(authentication =>
    {
        // This anonymous dashboard is intentionally limited to the local sample experience.
        authentication.AllowAnonymous();
    });
});

app.Run();
