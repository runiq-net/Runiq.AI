using Microsoft.Extensions.DependencyInjection;
using Runiq.Agents.Providers.OpenAI;
using Runiq.Agents.Runtime;
using Runiq.Agents.Tools;
using Runiq.Agents.Validation;
using Runiq.ContextSpaces.Models.Sources;
using Runiq.ContextSpaces.Services;
using Runiq.Core.Agents;
using Runiq.Core.Configuration;
using Runiq.Core.ContextSpaces;
using Runiq.Core.Metadata;
using Runiq.Core.Teams;
using Runiq.Core.Tools;
using Runiq.Core.Validation;
using Runiq.Teams.Execution;
using Runiq.Teams.Models.Teams;

namespace Runiq.Core;

/// <summary>
/// Runiq server tarafı servis kayıtlarını host uygulamanın DI container'ına ekleyen extension metodlarını içerir.
/// </summary>
public static class RuniqServerServiceCollectionExtensions
{
    /// <summary>
    /// Runiq Dashboard ve runtime metadata için gerekli server servislerini kaydeder.
    /// </summary>
    public static IServiceCollection AddRuniqServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IRuntimeMetadataService, RuntimeMetadataService>();

        services.AddSingleton<IContextSpaceSkillDiscoveryService, ContextSpaceSkillDiscoveryService>();
        services.AddSingleton<IContextSpaceSourceReader, ContextSpaceFileSystemSourceReader>();

        services.AddHttpClient<OpenAIResponsesClient>();
        services.AddHttpClient<OpenAICompatibleClient>();
        services.AddSingleton<AgentToolInvoker>();

        // Tools
        services.AddScoped<ToolRunApiHandler>();
        services.AddScoped<AgentExecutionRuntime>();
        services.AddScoped<AgentChatApiHandler>();

        // Agent Teams 
        services.AddScoped<TeamExecutionRuntime>();
        services.AddScoped<TeamChatApiHandler>();

        // Source context
        services.AddScoped<ContextSpaceSourceDocumentApiHandler>();
        services.AddScoped<ContextSpaceSkillDocumentApiHandler>();

        return services;
    }

    /// <summary>
    /// Runiq Dashboard, runtime metadata ve host uygulama tarafından tanımlanan agent kayıtlarını ekler.
    /// </summary>
    public static IServiceCollection AddRuniqServer(
        this IServiceCollection services,
        Action<RuniqServerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RuniqServerOptions();

        configure(options);

        AgentValidator.ValidateRegisteredAgents(options.Agents);
        RuniqServerRegistrationValidator.Validate(options);

        services.AddSingleton<IReadOnlyList<ContextSpace>>(
            options.ContextSpaces.ToArray());

        services.AddSingleton<IReadOnlyList<AgentTeam>>(
            options.Teams.ToArray());

        services.AddSingleton<IReadOnlyList<AgentToolRegistration>>(
            BuildRegisteredToolRegistry(options));

        foreach (var agent in options.Agents)
        {
            services.AddSingleton(agent);
        }

        services.AddRuniqServer();

        return services;
    }

    private static IReadOnlyList<AgentToolRegistration> BuildRegisteredToolRegistry(
        RuniqServerOptions options)
    {
        var toolsByName = new Dictionary<string, AgentToolRegistration>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var tool in options.Tools)
        {
            AddToolRegistration(toolsByName, tool);
        }

        foreach (var tool in options.Agents.SelectMany(agent => agent.Tools))
        {
            AddToolRegistration(toolsByName, tool);
        }

        return toolsByName
            .Values
            .OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddToolRegistration(
        Dictionary<string, AgentToolRegistration> toolsByName,
        AgentToolRegistration tool)
    {
        if (!toolsByName.TryGetValue(tool.Name, out var existing))
        {
            toolsByName[tool.Name] = tool;
            return;
        }

        if (existing.ToolType == tool.ToolType)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Tool name '{tool.Name}' is registered by multiple tool types: " +
            $"'{existing.ToolType.FullName}' and '{tool.ToolType.FullName}'. " +
            "Tool names must be unique.");
    }
}
