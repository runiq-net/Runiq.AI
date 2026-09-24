
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

using Runiq.AI.Agents.Tools;
using Runiq.AI.Core.Metadata;

namespace Runiq.AI.Agents.Runtime.Cli;

/// <summary>Exposes only one run's tool bindings through an authenticated loopback MCP transport.</summary>
internal sealed class CliToolBridge : IAsyncDisposable
{
    internal const string TokenVariable = "RUNIQ_CLI_TOOL_TOKEN";
    internal string Endpoint { get; private set; } = string.Empty;
    internal string Token { get; private set; } = string.Empty;
    private readonly WebApplication application;
    private readonly CancellationTokenSource lifetime;
    private readonly SemaphoreSlim invocationGate = new(1);

    private CliToolBridge(WebApplication application, CancellationTokenSource lifetime)
    {
        this.application = application;
        this.lifetime = lifetime;
    }

    internal static async Task<CliToolBridge> StartAsync(Agent agent, AgentToolInvoker invoker,
        int maxEventCharacters, int maxOutputCharacters, ChannelWriter<AgentExecutionEvent> events,
        CancellationToken cancellationToken)
    {
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            Args = [], ApplicationName = typeof(CliToolBridge).Assembly.FullName, EnvironmentName = "Production"
        });
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.Listen(IPAddress.Loopback, 0);
            server.Limits.MaxRequestBodySize = maxEventCharacters;
        });
        CliToolBridge? bridge = null;
        long outputCharacters = 0;
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithListToolsHandler((_, _) => ValueTask.FromResult(new ListToolsResult
            {
                Tools = agent.Tools.Select(tool => new Tool
                {
                    Name = tool.Name, Description = tool.Description,
                    InputSchema = JsonSerializer.SerializeToElement(ToolJsonSchemaGenerator.CreateSchema(tool.InputType))
                }).ToList()
            }))
            .WithCallToolHandler(async (request, token) =>
            {
                using var callLifetime = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
                var ct = callLifetime.Token;
                // A run's scoped dependencies may not be thread-safe (for example DbContext).
                await bridge!.invocationGate.WaitAsync(ct);
                try
                {
                    var name = request.Params?.Name ?? string.Empty;
                    var arguments = request.Params?.Arguments is { } values ? JsonSerializer.Serialize(values) : "{}";
                    var id = Guid.NewGuid().ToString("N");
                    await events.WriteAsync(AgentExecutionEvent.ToolCallStarted(id, name, arguments), ct);
                    var result = arguments.Length > maxEventCharacters
                        ? AgentToolInvocationResult.Failure("ToolInputTooLarge", "Tool input exceeded the configured limit.")
                        : await invoker.InvokeAsync(agent, name, arguments, ct);
                    if (result.OutputJson is { } output && (output.Length > maxEventCharacters ||
                        Interlocked.Add(ref outputCharacters, output.Length) > maxOutputCharacters))
                        result = AgentToolInvocationResult.Failure("ToolOutputTooLarge", "Tool output exceeded the configured limit.");
                    if (result.IsSuccess)
                        await events.WriteAsync(AgentExecutionEvent.ToolCallCompleted(id, name, result.OutputJson!), ct);
                    else
                        await events.WriteAsync(AgentExecutionEvent.ToolCallFailed(id, name, result.ErrorMessage!, result.ErrorCode!), ct);
                    return new CallToolResult
                    {
                        IsError = !result.IsSuccess,
                        Content = [new TextContentBlock { Text = result.IsSuccess ? result.OutputJson! : JsonSerializer.Serialize(new { result.ErrorCode, result.ErrorMessage }) }]
                    };
                }
                finally { bridge!.invocationGate.Release(); }
            });
        var app = builder.Build();
        bridge = new CliToolBridge(app, lifetime);
        app.Use(async (context, next) =>
        {
            // Browser origins are never needed; the per-run credential stays in the child environment.
            if (context.Request.Headers.ContainsKey("Origin") ||
                context.Request.Headers.Authorization.ToString() != "Bearer " + token)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            await next(context);
        });
        app.MapMcp("/mcp");
        try
        {
            await app.StartAsync(cancellationToken);
            bridge.Endpoint = app.Urls.Single() + "/mcp";
            bridge.Token = token;
            return bridge;
        }
        catch (Exception exception)
        {
            await bridge.DisposeAsync();
            if (exception is IOException or System.Net.Sockets.SocketException or InvalidOperationException)
                throw new CliProcessException("ToolBridgeFailed");
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await lifetime.CancelAsync();
        try { await application.StopAsync(CancellationToken.None); }
        finally
        {
            await application.DisposeAsync();
            // Do not let the caller dispose its DI scope while an invocation is still using it.
            await invocationGate.WaitAsync();
            invocationGate.Release();
            invocationGate.Dispose();
            lifetime.Dispose();
        }
    }
}
