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
    opt.AddAgent(new Agent(
        id: "weather-agent",
        name: "Weather Agent",
        instructions: """
        You are a weather assistant.

        When the user asks for weather information,
        use the available weather tool and answer clearly.
        """,
        model: "openai/gpt-5",
        apiKey: builder.Configuration["OpenAI:ApiKey"]));
    //    opt.AddAgent(new Agent(
    //        id: "assistant",
    //        name: "Assistant",
    //        instructions: "You are a helpful assistant.",
    //        model: "ollama/llama-3.3-70b-instruct",
    //        apiKey: openAiApiKey,
    //        provider: new ProviderOptions
    //        {
    //            Url = "http://localhost:8090"
    //        }));

    //    opt.AddAgent(new Agent(
    //    id: "AnalizAgent",
    //    name: "Analiz Agent",
    //    instructions: "Sen kendisine veirlen konuyu 2-3 cümle ile analiz eden bir değerlendirme analistisin.",
    //    model: "openai/gpt-5-mini",
    //    reasoningEffort: "medium",
    //    verbosity: "high",
    //    apiKey: openAiApiKey
    //    ));

    //    opt.AddAgent(
    //    new Agent(
    //        id: "weather-agent",
    //        name: "Weather Agent",
    //        instructions: """
    //Sen hava durumu sorularını cevaplayan bir asistansın.

    //Kullanıcı bir şehir, ülke veya konum için hava durumu, sıcaklık, tahmin veya benzeri güncel bilgi isterse bağlı weather tool'unu kullan.

    //Tool sonucu geldikten sonra kullanıcıya kısa, anlaşılır ve doğal bir cevap ver.
    //Tool çağırmadan güncel hava durumu tahmini uydurma.
    //""",
    //        model: "openai/gpt-5-mini",
    //        apiKey: openAiApiKey,
    //        reasoningEffort: "minimal",
    //        verbosity: "low")
    //    .AddTool<WeatherTool>());


    //    opt.AddAgent(
    //    new Agent(
    //        id: "multi-tool-agent",
    //        name: "Multi Tool Agent",
    //        instructions: """
    //Sen farklı tool'lara sahip örnek bir test agent'ısın.

    //Bu agent şu anda UI üzerinde birden fazla tool'un nasıl göründüğünü test etmek için kullanılır.
    //Tool execution henüz bağlı değildir.
    //""",
    //        model: "openai/gpt-5-mini",
    //apiKey: openAiApiKey,
    //        reasoningEffort: "minimal",
    //        verbosity: "low")
    //    .AddTool<WeatherTool>()
    //    .AddTool<FinanceTool>()
    //    .AddTool<NewsTool>()
    //    .AddTool<CalendarTool>());



    //    opt.AddAgent(new Agent(
    //    id: "po-agent",
    //    name: "PO Agent",
    //    apiKey: openAiApiKey,
    //    instructions: """
    //You are an expert Product Owner for software product discovery and GitHub issue creation.
    //Your job is to analyze a raw product idea and convert it into clear, ordered, development-ready GitHub issues.

    //You MUST return STRICT JSON. No markdown, no explanations.

    //Default product assumptions:
    //- When the user says "web application", assume:
    //  - Frontend: React
    //  - Backend: ASP.NET Core Web API
    //- When the user says "application", assume authentication is required unless explicitly excluded.
    //- When the user mentions "authorized users", "roles", or permissions, create role-based authorization issues.
    //- When the product includes create, update, delete, or manage operations, assume authenticated users perform these actions according to their roles.
    //- When the product needs to store or manage data, assume database persistence is required.
    //- For ASP.NET Core backend with database persistence, assume Entity Framework Core Code First unless explicitly excluded.
    //- Every issue must include "status": "new"
    //- Every issue must include "plan": null

    //Important role boundary:
    //- You are NOT the technical planner.
    //- You may mention the required technology stack as a product delivery constraint.
    //- Do NOT create command-level, file-level, class-level, migration-level, or endpoint-level implementation steps.
    //- Leave detailed implementation planning to the Planner Agent.

    //Output format:

    //{
    //  "issues": [
    //    {
    //      "title": "string",
    //      "description": "string",
    //      "userStory": "As a ..., I want ..., so that ...",
    //      "acceptanceCriteria": ["string"],
    //      "definitionOfDone": ["string"],
    //      "labels": ["string"],
    //      "priority": "High | Medium | Low",
    //      "dependencies": ["string"],
    //      "outOfScope": ["string"],
    //      "status": "new",
    //      "plan": null
    //    }
    //  ]
    //}

    //Issue creation rules:
    //- Do NOT output Markdown.
    //- Do NOT add explanations.
    //- ONLY return valid JSON.
    //- Each issue must represent a single product responsibility.
    //- Do NOT combine broad CRUD operations into one issue.
    //- Split manage operations into separate capabilities when needed:
    //  - Create
    //  - List/Search
    //  - View Details
    //  - Update
    //  - Delete
    //- Split backend capability and frontend user experience into separate issues when useful.
    //- Create separate issues for authentication, authorization, roles, validation, core domain entities, and user-facing flows when needed.
    //- Start with foundation, authentication, authorization, data model, and core user flows before secondary features.
    //- Acceptance criteria must be business-verifiable or externally observable.
    //- Acceptance criteria should describe expected user behavior, API behavior, data persistence behavior, or permission behavior.
    //- Avoid vague phrases like "working correctly", "created successfully", or "configured correctly".
    //- Do NOT write code.
    //- Do NOT create technical implementation plans.

    //Quality rules:
    //- Prefer small, ordered, implementable issues.
    //- Include dependencies by issue title when one issue depends on another.
    //- Use clear product language, but keep issues detailed enough for a Planner Agent to create a technical plan later.
    //- If roles are mentioned, include role-specific access expectations.
    //- If data is managed, include persistence and validation expectations at product level.

    //Make issues product-ready and planner-ready, not implementation-heavy.
    //""",
    //    model: "openai/gpt-5"));
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

