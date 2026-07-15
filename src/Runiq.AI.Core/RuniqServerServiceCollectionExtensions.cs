using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Agents.Validation;
using Runiq.AI.ContextSpaces.Models.Sources;
using Runiq.AI.ContextSpaces.Services;
using Runiq.AI.Core.Agents;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Core.ContextSpaces;
using Runiq.AI.Core.Metadata;
using Runiq.AI.Core.Mcp;
using Runiq.AI.Core.Rag;
using Runiq.AI.Core.Tools;
using Runiq.AI.Core.Validation;

namespace Runiq.AI.Core;

/// <summary>
/// Runiq server tarafi servis kayitlarini host uygulamanin DI container'ina ekleyen extension metodlarini içerir.
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
        services.AddScoped<RuniqMcpToolRunApiHandler>();
        services.AddScoped<AgentExecutionRuntime>();
        services.AddScoped<AgentChatApiHandler>();

        // Source context
        services.AddScoped<ContextSpaceSourceDocumentApiHandler>();
        services.AddScoped<ContextSpaceSkillDocumentApiHandler>();

        // RAG visibility (read-only; replaceable by host registrations)
        services.TryAddScoped<IRuniqRagInfoProvider, RuniqRagInfoReader>();

        return services;
    }

    /// <summary>
    /// Runiq Dashboard, runtime metadata ve host uygulama tarafindan tanimlanan agent kayitlarini ekler.
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

