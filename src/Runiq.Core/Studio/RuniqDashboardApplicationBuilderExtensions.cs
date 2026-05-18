using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Runiq.Agents;
using Runiq.Agents.Tools;
using Runiq.Core.Agents;
using Runiq.Core.Dashboard;
using Runiq.Core.Metadata;
using System.Text.Json;
using Runiq.Agents.Tools;

namespace Runiq.Core;

/// <summary>
/// Runiq Dashboard'u host uygulama içinde yayınlayan extension metodlarını içerir.
/// </summary>
public static class RuniqDashboardApplicationBuilderExtensions
{

    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>
    /// Runiq Dashboard'u varsayılan ayarlarla yayınlar.
    /// </summary>
    public static IApplicationBuilder UseRuniqDashboard(this IApplicationBuilder app)
    {
        return app.UseRuniqDashboard(_ => { });
    }

    /// <summary>
    /// Runiq Dashboard'u verilen ayarlarla yayınlar.
    /// </summary>
    public static IApplicationBuilder UseRuniqDashboard(
        this IApplicationBuilder app,
        Action<RuniqDashboardOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RuniqDashboardOptions();
        configure(options);

        var basePath = NormalizePath(options.Path);

        var dashboardRoot = Path.Combine(
            AppContext.BaseDirectory,
            "Studio",
            "wwwroot");

        if (!Directory.Exists(dashboardRoot))
        {
            throw new DirectoryNotFoundException(
                $"Runiq Dashboard assets could not be found. Expected path: {dashboardRoot}");
        }

        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals(basePath, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect(basePath + "/");
                return;
            }

            await next();
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = basePath,
            FileProvider = new PhysicalFileProvider(dashboardRoot)
        });

        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments(basePath, out var remainingPath))
            {
                await next();
                return;
            }

            var relativePath = remainingPath.Value ?? string.Empty;

            if (relativePath.Equals("/metadata/agents", StringComparison.OrdinalIgnoreCase))
            {
                var metadataService =
                    context.RequestServices.GetRequiredService<IRuntimeMetadataService>();

                var agents = metadataService.GetAgents();

                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(agents, context.RequestAborted);
                return;
            }

            if (relativePath.StartsWith("/api/agents/", StringComparison.OrdinalIgnoreCase) &&
                relativePath.EndsWith("/chat/stream", StringComparison.OrdinalIgnoreCase) &&
                HttpMethods.IsPost(context.Request.Method))
            {
                await HandleAgentChatStreamAsync(context, relativePath);
                return;
            }

            if (relativePath.StartsWith("/api/agents/", StringComparison.OrdinalIgnoreCase) &&
                relativePath.EndsWith("/chat", StringComparison.OrdinalIgnoreCase) &&
                HttpMethods.IsPost(context.Request.Method))
            {
                await HandleAgentChatAsync(context, relativePath);
                return;
            }

            var requestPath = context.Request.Path.Value ?? string.Empty;

            var isStaticAsset =
                Path.HasExtension(requestPath) ||
                requestPath.Contains("/assets/", StringComparison.OrdinalIgnoreCase);

            if (isStaticAsset)
            {
                await next();
                return;
            }

            var indexPath = Path.Combine(dashboardRoot, "index.html");

            if (!File.Exists(indexPath))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("Runiq Dashboard index.html not found.");
                return;
            }

            var html = await File.ReadAllTextAsync(indexPath, context.RequestAborted);

            html = html
                .Replace("__RUNIQ_BASE_PATH__", basePath)
                .Replace("__RUNIQ_TITLE__", options.Title);

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html);
        });

        return app;
    }

    private static async Task HandleAgentChatAsync(
        HttpContext context,
        string relativePath)
    {
        var agentId = ExtractAgentIdFromChatPath(relativePath);

        if (string.IsNullOrWhiteSpace(agentId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new AgentChatResponse(
                    IsSuccess: false,
                    Message: null,
                    ErrorCode: "InvalidAgentChatPath",
                    ErrorMessage: "Agent chat path must be in '/api/agents/{agentId}/chat' format."),
                context.RequestAborted);
            return;
        }

        var agent = FindAgent(context, agentId);

        if (agent is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(
                new AgentChatResponse(
                    IsSuccess: false,
                    Message: null,
                    ErrorCode: "AgentNotFound",
                    ErrorMessage: $"Agent '{agentId}' was not found."),
                context.RequestAborted);
            return;
        }

        var request = await context.Request.ReadFromJsonAsync<AgentChatRequest>(
            cancellationToken: context.RequestAborted);

        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new AgentChatResponse(
                    IsSuccess: false,
                    Message: null,
                    ErrorCode: "MessageRequired",
                    ErrorMessage: "Message cannot be empty."),
                context.RequestAborted);
            return;
        }

        var result = await agent.ExecuteAsync(request.Message, context.RequestAborted);

        var response = new AgentChatResponse(
            IsSuccess: result.IsSuccess,
            Message: result.Message,
            ErrorCode: result.ErrorCode,
            ErrorMessage: result.ErrorMessage);

        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(response, context.RequestAborted);
    }

    private static async Task HandleAgentChatStreamAsync(
        HttpContext context,
        string relativePath)
    {
        var agentId = ExtractAgentIdFromChatStreamPath(relativePath);

        if (string.IsNullOrWhiteSpace(agentId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new AgentChatResponse(
                    IsSuccess: false,
                    Message: null,
                    ErrorCode: "InvalidAgentChatStreamPath",
                    ErrorMessage: "Agent chat stream path must be in '/api/agents/{agentId}/chat/stream' format."),
                context.RequestAborted);
            return;
        }

        var agent = FindAgent(context, agentId);

        if (agent is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(
                new AgentChatResponse(
                    IsSuccess: false,
                    Message: null,
                    ErrorCode: "AgentNotFound",
                    ErrorMessage: $"Agent '{agentId}' was not found."),
                context.RequestAborted);
            return;
        }

        var request = await context.Request.ReadFromJsonAsync<AgentChatRequest>(
            cancellationToken: context.RequestAborted);

        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new AgentChatResponse(
                    IsSuccess: false,
                    Message: null,
                    ErrorCode: "MessageRequired",
                    ErrorMessage: "Message cannot be empty."),
                context.RequestAborted);
            return;
        }

        context.Response.ContentType = "text/event-stream; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        var toolInvoker = new AgentToolInvoker(context.RequestServices);

        await foreach (var executionEvent in agent.ExecuteStreamAsync(
                           request.Message,
                           toolInvoker,
                           context.RequestAborted))
        {
            var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(executionEvent);
            var payload = JsonSerializer.Serialize(streamEvent, StreamJsonOptions);

            await context.Response.WriteAsync(
                $"data: {payload}\n\n",
                context.RequestAborted);

            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
        await context.Response.WriteAsync("data: [DONE]\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }

    private static Agent? FindAgent(HttpContext context, string agentId)
    {
        var agents = context.RequestServices.GetServices<Agent>();

        return agents.FirstOrDefault(item =>
            item.Id.Equals(agentId, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Dashboard path cannot be empty.", nameof(path));
        }

        var normalized = path.Trim();

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        normalized = normalized.TrimEnd('/');

        if (normalized == "/")
        {
            throw new ArgumentException("Dashboard root path '/' is not supported.", nameof(path));
        }

        return normalized;
    }

    private static string? ExtractAgentIdFromChatPath(string relativePath)
    {
        const string prefix = "/api/agents/";
        const string suffix = "/chat";

        if (!relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !relativePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var agentId = relativePath[prefix.Length..^suffix.Length];

        return string.IsNullOrWhiteSpace(agentId)
            ? null
            : Uri.UnescapeDataString(agentId.Trim('/'));
    }

    private static string? ExtractAgentIdFromChatStreamPath(string relativePath)
    {
        const string prefix = "/api/agents/";
        const string suffix = "/chat/stream";

        if (!relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !relativePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var agentId = relativePath[prefix.Length..^suffix.Length];

        return string.IsNullOrWhiteSpace(agentId)
            ? null
            : Uri.UnescapeDataString(agentId.Trim('/'));
    }
}