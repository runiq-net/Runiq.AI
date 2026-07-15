using Runiq.AI.Cli.Models;
using Spectre.Console;

namespace Runiq.AI.Cli.Interactive;

public sealed class ProjectWizard
{
    public ProjectDefinition Run(string projectName)
    {
        PrintAnswer("Project name", projectName);

        var providerName = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("◇ Select a default provider:")
                .AddChoices(
                    FormatProvider(AiProvider.OpenAi),
                    FormatProvider(AiProvider.AzureOpenAi),
                    FormatProvider(AiProvider.Ollama),
                    FormatProvider(AiProvider.Anthropic)));
        var provider = ParseProvider(providerName);
        PrintAnswer("Select a default provider:", providerName);

        var apiKeySetup = AskApiKeySetup(provider);

        var includeSampleCode = AskIncludeSampleCode();

        var enableDashboard = AskBoolean("Enable Dashboard?", true);

        var enableMcp = AskBoolean("Enable MCP?", true);

        return new ProjectDefinition
        {
            Name = projectName,
            Provider = provider,
            ApiKeySetupMode = apiKeySetup.Mode,
            ApiKeyValue = apiKeySetup.ApiKeyValue,
            AzureOpenAiEndpoint = apiKeySetup.AzureOpenAiEndpoint,
            IncludeSampleCode = includeSampleCode,
            EnableDashboard = enableDashboard,
            EnableMcp = enableMcp
        };
    }

    public string AskProjectName()
    {
        AnsiConsole.MarkupLine("◇ What do you want to name your project?");

        return AnsiConsole.Prompt(
            new TextPrompt<string>("  ")
                .Validate(name =>
                    ProjectNameValidator.TryValidate(name, out var error)
                        ? ValidationResult.Success()
                        : ValidationResult.Error(error)));
    }

    private static bool AskBoolean(string question, bool defaultValue)
    {
        var defaultAnswer = defaultValue ? "Yes" : "No";
        var otherAnswer = defaultValue ? "No" : "Yes";

        var answer = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"◇ {question}")
                .AddChoices(defaultAnswer, otherAnswer));

        PrintAnswer(question, answer);

        return answer == "Yes";
    }

    private static bool AskIncludeSampleCode()
    {
        const string recommendedAnswer = "Yes (recommended)";

        var answer = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("◇ Include starter sample code?")
                .AddChoices(recommendedAnswer, "No"));

        PrintAnswer("Include starter sample code?", answer);

        return answer == recommendedAnswer;
    }

    private static ApiKeySetup AskApiKeySetup(AiProvider provider)
    {
        if (provider == AiProvider.Ollama)
        {
            PrintAnswer("Configure provider API key?", "Ollama does not require an API key.");

            return new ApiKeySetup(ApiKeySetupMode.Skip, null, null);
        }

        var setupChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("◇ Configure provider API key?")
                .AddChoices("Skip for now", "Enter API key"));
        PrintAnswer("Configure provider API key?", setupChoice);

        if (setupChoice == "Skip for now")
        {
            return new ApiKeySetup(ApiKeySetupMode.Skip, null, null);
        }

        if (provider == AiProvider.AzureOpenAi)
        {
            AnsiConsole.MarkupLine("◇ Azure OpenAI endpoint");
            var endpoint = AnsiConsole.Prompt(
                new TextPrompt<string>("  "));

            AnsiConsole.MarkupLine("◇ Azure OpenAI API key");
            var key = AnsiConsole.Prompt(
                new TextPrompt<string>("  ")
                    .Secret());
            PrintAnswer("Azure OpenAI API key", "Stored in user-secrets");

            return new ApiKeySetup(ApiKeySetupMode.UserSecrets, key, endpoint);
        }

        AnsiConsole.MarkupLine($"◇ {FormatProvider(provider)} API key");
        var apiKey = AnsiConsole.Prompt(
            new TextPrompt<string>("  ")
                .Secret());
        PrintAnswer($"{FormatProvider(provider)} API key", "Stored in user-secrets");

        return new ApiKeySetup(ApiKeySetupMode.UserSecrets, apiKey, null);
    }

    private static void PrintAnswer(string question, string answer)
    {
        AnsiConsole.MarkupLine($"◇ {question}");
        AnsiConsole.MarkupLine($"  [green]{answer}[/]");
        AnsiConsole.WriteLine();
    }

    private static string FormatProvider(AiProvider provider)
    {
        return provider switch
        {
            AiProvider.OpenAi => "OpenAI",
            AiProvider.AzureOpenAi => "Azure OpenAI",
            AiProvider.Ollama => "Ollama",
            AiProvider.Anthropic => "Anthropic",
            _ => provider.ToString()
        };
    }

    private static AiProvider ParseProvider(string provider)
    {
        return provider switch
        {
            "OpenAI" => AiProvider.OpenAi,
            "Azure OpenAI" => AiProvider.AzureOpenAi,
            "Ollama" => AiProvider.Ollama,
            "Anthropic" => AiProvider.Anthropic,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };
    }

    private sealed record ApiKeySetup(
        ApiKeySetupMode Mode,
        string? ApiKeyValue,
        string? AzureOpenAiEndpoint);
}

