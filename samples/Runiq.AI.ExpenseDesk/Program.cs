using Runiq.AI.Core;
using Runiq.AI.ExpenseDesk.Agents;
using Runiq.AI.ExpenseDesk.Data;

var builder = WebApplication.CreateBuilder(args);

var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];

builder.Services.AddSingleton<ExpenseDeskDatabase>();

builder.Services.AddRuniqServer(options =>
{
    options.AddAgent(ExpenseDataAnalyst.Create(openAiApiKey));
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
    options.Title = "Runiq Expense Desk";
    options.Authentication(auth =>
    {
        // Demo/sample only. Do not use AllowAnonymous in production.
        auth.AllowAnonymous();
    });
});

app.Run();

