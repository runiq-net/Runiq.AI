using Runiq.AI.Cli.Infrastructure;
using Runiq.AI.Cli.Models;

namespace Runiq.AI.Cli.Generation;

public sealed class RuniqIntegrationGenerator
{
    private readonly IFileSystem _fileSystem;
    private readonly IProcessRunner _processRunner;

    public RuniqIntegrationGenerator(
        IFileSystem fileSystem,
        IProcessRunner processRunner)
    {
        _fileSystem = fileSystem;
        _processRunner = processRunner;
    }

    public void Generate(
        ProjectDefinition definition,
        string apiProjectPath)
    {
        AddPackages(definition, apiProjectPath);
        ConfigureUserSecrets(definition, apiProjectPath);
        UpdateProgram(definition, apiProjectPath);
    }

    private void AddPackages(
        ProjectDefinition definition,
        string apiProjectPath)
    {
        RunDotNet(
            [
                "add",
                apiProjectPath,
                "package",
                RuniqPackageNames.Core,
                "--prerelease"
            ],
            Directory.GetCurrentDirectory());

        if (definition.EnableMcp)
        {
            RunDotNet(
                [
                    "add",
                    apiProjectPath,
                    "package",
                    RuniqPackageNames.Mcp,
                    "--prerelease"
                ],
                Directory.GetCurrentDirectory());
        }
    }

    private void UpdateProgram(
        ProjectDefinition definition,
        string apiProjectPath)
    {
        var programPath = Path.Combine(
            Path.GetDirectoryName(apiProjectPath)
                ?? throw new InvalidOperationException("API project path has no parent directory."),
            "Program.cs");

        _fileSystem.WriteAllText(
            programPath,
            CreateProgramContent(definition));
    }

    private void ConfigureUserSecrets(
        ProjectDefinition definition,
        string apiProjectPath)
    {
        if (definition.ApiKeySetupMode != ApiKeySetupMode.UserSecrets)
        {
            return;
        }

        RunDotNet(
            [
                "user-secrets",
                "init",
                "--project",
                apiProjectPath
            ],
            Directory.GetCurrentDirectory());

        if (definition.Provider == AiProvider.AzureOpenAi)
        {
            SetUserSecret(
                apiProjectPath,
                "AzureOpenAI:Endpoint",
                definition.AzureOpenAiEndpoint
                    ?? throw new InvalidOperationException("Azure OpenAI endpoint is missing."));

            SetUserSecret(
                apiProjectPath,
                "AzureOpenAI:ApiKey",
                definition.ApiKeyValue
                    ?? throw new InvalidOperationException("Azure OpenAI API key is missing."));

            return;
        }

        SetUserSecret(
            apiProjectPath,
            GetApiKeyName(definition.Provider),
            definition.ApiKeyValue
                ?? throw new InvalidOperationException("Provider API key is missing."));
    }

    private void SetUserSecret(
        string apiProjectPath,
        string key,
        string value)
    {
        RunDotNet(
            [
                "user-secrets",
                "set",
                key,
                value,
                "--project",
                apiProjectPath
            ],
            Directory.GetCurrentDirectory(),
            [
                "user-secrets",
                "set",
                key,
                "<redacted>",
                "--project",
                apiProjectPath
            ]);
    }

    private static string CreateProgramContent(ProjectDefinition definition)
    {
        var usingStatements = new List<string>
        {
            "using Runiq.AI.Core;"
        };

        if (definition.IncludeSampleCode)
        {
            usingStatements.Add($"using {definition.Name}.Api.Agents;");
            usingStatements.Add($"using {definition.Name}.Api.Tools;");
        }

        if (definition.EnableMcp)
        {
            usingStatements.Add("using Runiq.AI.Mcp;");
        }

        var mcpServices = definition.EnableMcp
            ? "\nbuilder.Services.AddRuniqMcp();"
            : string.Empty;

        var addRuniqServer = definition.IncludeSampleCode
            ? $$"""
              builder.Services.AddRuniqServer(options =>
              {
                  var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];

                  options.AddAgent(TravelPlannerAgent.Create(openAiApiKey));
                  options.AddAgent(BudgetAdvisorAgent.Create(openAiApiKey));
                  options.AddTool<WeatherTool>();
                  options.AddTool<TripCostTool>();
              });
              """
            : """
              builder.Services.AddRuniqServer();
              """;

        var dashboardMiddleware = definition.EnableDashboard
            ? $$"""

              app.UseRuniqDashboard(options =>
              {
                  options.Path = "/dashboard";
                  options.Title = "{{definition.Name}}";
                  options.Authentication(auth =>
                  {
                      // Development default for generated projects.
                      auth.AllowAnonymous();
                  });
              });
              """
            : string.Empty;

        var mcpEndpoints = definition.EnableMcp
            ? "\n\napp.MapRuniqMcp();"
            : string.Empty;

        return $$"""
               {{string.Join('\n', usingStatements)}}

               var builder = WebApplication.CreateBuilder(args);

               builder.Services.AddOpenApi();
               {{addRuniqServer}}{{mcpServices}}

               var app = builder.Build();

               if (app.Environment.IsDevelopment())
               {
                   app.MapOpenApi();
               }

               app.UseHttpsRedirection();

               app.MapGet("/", () => "{{definition.Name}} API is running.");{{dashboardMiddleware}}{{mcpEndpoints}}

               app.Run();
               """;
    }

    private static string GetApiKeyName(AiProvider provider)
    {
        return provider switch
        {
            AiProvider.OpenAi => "OpenAI:ApiKey",
            AiProvider.Anthropic => "Anthropic:ApiKey",
            _ => throw new InvalidOperationException(
                $"Provider '{provider}' does not support API key setup.")
        };
    }

    private void RunDotNet(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyList<string>? displayArguments = null)
    {
        var result = _processRunner.Run(
            "dotnet",
            arguments,
            workingDirectory);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet {string.Join(' ', displayArguments ?? arguments)} failed with exit code {result.ExitCode}.{Environment.NewLine}{result.StandardOutput}{result.StandardError}");
        }
    }
}

