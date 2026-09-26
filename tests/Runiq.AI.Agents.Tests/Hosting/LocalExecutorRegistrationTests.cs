using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Runtime.Cli;
using Runiq.AI.Agents.Runtime.Codex;
using Runiq.AI.Agents.Runtime.Claude;
using Runiq.AI.Core;
using Runiq.AI.Agents.Tools;

namespace Runiq.AI.Agents.Tests.Hosting;

public sealed class LocalExecutorRegistrationTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    // Verifies runtime dispatch independently selects custom executors or built-in fallbacks in either registration order.
    public async Task CustomOverrides_AreIndependentOfRegistrationOrder(bool customCodex, bool customClaude, bool customBefore)
    {
        var services = new ServiceCollection().AddLogging();
        if (customBefore) RegisterCustom();
        for (var index = 0; index < 2; index++)
        {
            services.AddRuniqServer(options =>
            {
                options.AddAgent(new Agent($"codex-{index}", "Codex", "").UseCodex(o => o.Model = "model"));
                options.AddAgent(new Agent($"claude-{index}", "Claude", "").UseClaude(claude => claude.Model = "sonnet"));
            });
        }
        if (!customBefore) RegisterCustom();
        var missingExecutable = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe");
        services.Configure<CodexExecutorOptions>(o => o.ExecutablePath = missingExecutable);
        services.Configure<ClaudeExecutorOptions>(o => o.ExecutablePath = missingExecutable);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var resolver = scope.ServiceProvider.GetRequiredService<AgentExecutorResolver>();
        foreach (var (kind, custom) in new[] { (AgentExecutorKind.Codex, customCodex), (AgentExecutorKind.Claude, customClaude) })
        {
            if (custom) Assert.IsType<CustomExecutor>(resolver.Resolve(kind));
            else Assert.IsAssignableFrom<IFallbackAgentExecutor>(resolver.Resolve(kind));
            for (var index = 0; index < 2; index++)
            {
                var result = await runtime.ExecuteAsync($"{kind.ToString().ToLowerInvariant()}-{index}", "hello");
                if (custom)
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal($"custom-{kind}", result.Message);
                }
                else Assert.Equal($"{kind}ExecutableNotFound", result.ErrorCode);
            }
        }

        void RegisterCustom()
        {
            if (customCodex) services.AddScoped<IAgentExecutor>(_ => new CustomExecutor(AgentExecutorKind.Codex));
            if (customClaude) services.AddScoped<IAgentExecutor>(_ => new CustomExecutor(AgentExecutorKind.Claude));
        }
    }

    [Theory]
    [InlineData(AgentExecutorKind.Codex, false)]
    [InlineData(AgentExecutorKind.Codex, true)]
    [InlineData(AgentExecutorKind.Claude, false)]
    [InlineData(AgentExecutorKind.Claude, true)]
    // Verifies two distinct custom implementations remain ambiguous even when a built-in fallback is available.
    public void MultipleCustomExecutors_RejectAmbiguity(AgentExecutorKind kind, bool customBefore)
    {
        var services = new ServiceCollection().AddLogging();
        if (customBefore) RegisterCustom();
        var agent = new Agent("local", "Local", "");
        if (kind == AgentExecutorKind.Codex) agent.UseCodex(o => o.Model = "model");
        else agent.UseClaude(claude => claude.Model = "sonnet");
        services.AddRuniqServer(o => o.AddAgent(agent));
        if (!customBefore) RegisterCustom();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var error = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>());
        Assert.Contains($"Multiple agent executors are registered for kind '{kind}'", error.Message);

        void RegisterCustom()
        {
            services.AddScoped<IAgentExecutor>(_ => new CustomExecutor(kind));
            services.AddScoped<IAgentExecutor>(_ => new OtherCustomExecutor(kind));
        }
    }

    private class CustomExecutor(AgentExecutorKind kind) : IAgentExecutor
    {
        public AgentExecutorKind Kind => kind;

        public async IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request,
            AgentRunContext run, AgentToolInvoker toolInvoker, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            cancellationToken.ThrowIfCancellationRequested();
            yield return AgentExecutionEvent.AssistantDelta($"custom-{kind}");
            yield return AgentExecutionEvent.Completed();
        }
    }

    private sealed class OtherCustomExecutor(AgentExecutorKind kind) : CustomExecutor(kind);

    [Theory]
    [InlineData(true, "CodexExecutableNotFound")]
    [InlineData(false, "ClaudeExecutableNotFound")]
    // Verifies automatic registration reports an actionable missing-CLI failure instead of a missing executor.
    public async Task MissingExecutable_ReportsCliFailure(bool codex, string expectedCode)
    {
        var services = new ServiceCollection().AddLogging();
        var agent = new Agent("local", "Local", "");
        if (codex) agent.UseCodex(options => options.Model = "model");
        else agent.UseClaude(claude => claude.Model = "sonnet");
        services.AddRuniqServer(options => options.AddAgent(agent));
        var missingExecutable = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe");
        services.Configure<CodexExecutorOptions>(options => options.ExecutablePath = missingExecutable);
        services.Configure<ClaudeExecutorOptions>(options => options.ExecutablePath = missingExecutable);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("local", "hello");
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Contains("executable", result.ErrorMessage!);
    }

    [Fact]
    // Verifies the minimal hosted registration supplies both executors with the application content root.
    public void MinimalHost_UsesContentRootForBothExecutors()
    {
        using var host = new HostBuilder().UseContentRoot(Path.GetTempPath()).ConfigureServices(services =>
            services.AddRuniqServer(options =>
            {
                options.AddAgent(new Agent("codex", "Codex", "").UseCodex(o => o.Model = "model"));
                options.AddAgent(new Agent("claude", "Claude", "").UseClaude(claude => claude.Model = "sonnet"));
            })).Build();
        var expected = host.Services.GetRequiredService<IHostEnvironment>().ContentRootPath;
        Assert.Equal(expected, host.Services.GetRequiredService<IOptions<CodexExecutorOptions>>().Value.WorkingDirectory);
        Assert.Equal(expected, host.Services.GetRequiredService<IOptions<ClaudeExecutorOptions>>().Value.WorkingDirectory);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    // Verifies discovery registers only selected CLI kinds, once each, even across repeated server registration.
    public void RegisteredAgents_SelectInfrastructure(bool codex, bool claude)
    {
        var services = new ServiceCollection().AddLogging();
        for (var index = 0; index < 2; index++)
        {
            services.AddRuniqServer(options =>
            {
                if (codex) options.AddAgent(new Agent($"codex-{index}", "Codex", "").UseCodex(o => o.Model = "future-model"));
                if (claude) options.AddAgent(new Agent($"claude-{index}", "Claude", "").UseClaude(claude => claude.Model = "sonnet"));
                options.AddAgent(new Agent($"model-{index}", "Model", "").UseModel("openai/model"));
            });
        }
        Assert.Equal(codex || claude ? 1 : 0, services.Count(d => d.ServiceType == typeof(ICliProcessFactory)));
        Assert.Equal(codex ? 1 : 0, services.Count(d => d.ServiceType == typeof(CodexSessionGate)));
        Assert.Equal(claude ? 1 : 0, services.Count(d => d.ServiceType == typeof(ClaudeSessionGate)));
        Assert.Equal(codex ? 1 : 0, services.Count(d => d.ServiceType == typeof(IPostConfigureOptions<CodexExecutorOptions>)));
        Assert.Equal(claude ? 1 : 0, services.Count(d => d.ServiceType == typeof(IPostConfigureOptions<ClaudeExecutorOptions>)));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        using var otherScope = provider.CreateScope();
        var executors = scope.ServiceProvider.GetServices<IAgentExecutor>().ToArray();
        Assert.Single(executors, e => e.Kind == AgentExecutorKind.Model);
        Assert.Equal(codex ? 1 : 0, executors.Count(e => e.Kind == AgentExecutorKind.Codex));
        Assert.Equal(claude ? 1 : 0, executors.Count(e => e.Kind == AgentExecutorKind.Claude));
        var resolver = scope.ServiceProvider.GetRequiredService<AgentExecutorResolver>();
        foreach (var executor in executors)
        {
            Assert.Same(executor, resolver.Resolve(executor.Kind));
            Assert.NotSame(executor, otherScope.ServiceProvider.GetServices<IAgentExecutor>().Single(e => e.Kind == executor.Kind));
        }
        if (codex || claude)
            Assert.Same(scope.ServiceProvider.GetRequiredService<ICliProcessFactory>(), otherScope.ServiceProvider.GetRequiredService<ICliProcessFactory>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Verifies both CLI executors inherit the host content root and explicit process options always win.
    public void WorkingDirectory_UsesHostDefaultAndPreservesOverrides(bool configureBefore)
    {
        var contentRoot = Path.GetTempPath();
        var explicitDirectory = AppContext.BaseDirectory;
        using var host = new HostBuilder().UseContentRoot(contentRoot).ConfigureServices(services =>
        {
            if (configureBefore) Configure(services);
            services.AddRuniqServer(options =>
            {
                options.AddAgent(new Agent("codex", "Codex", "").UseCodex(o => o.Model = "model-a"));
                options.AddAgent(new Agent("claude", "Claude", "").UseClaude(claude => claude.Model = "sonnet"));
            });
            if (!configureBefore) Configure(services);
        }).Build();
        Assert.Equal(explicitDirectory, host.Services.GetRequiredService<IOptions<CodexExecutorOptions>>().Value.WorkingDirectory);
        Assert.Equal(TimeSpan.FromSeconds(42), host.Services.GetRequiredService<IOptions<CodexExecutorOptions>>().Value.Timeout);
        Assert.Equal(host.Services.GetRequiredService<IHostEnvironment>().ContentRootPath,
            host.Services.GetRequiredService<IOptions<ClaudeExecutorOptions>>().Value.WorkingDirectory);

        void Configure(IServiceCollection services) => services.Configure<CodexExecutorOptions>(options =>
        {
            options.WorkingDirectory = explicitDirectory;
            options.Timeout = TimeSpan.FromSeconds(42);
        });
    }

    [Fact]
    // Verifies non-hosted DI consumers receive a usable directory default without requiring IHostEnvironment.
    public void WorkingDirectory_WithoutHostEnvironmentUsesCurrentDirectory()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddRuniqServer(options =>
        {
            options.AddAgent(new Agent("codex", "Codex", "").UseCodex(o => o.Model = "model"));
            options.AddAgent(new Agent("claude", "Claude", "").UseClaude(claude => claude.Model = "sonnet"));
        });
        using var provider = services.BuildServiceProvider();
        Assert.Equal(Directory.GetCurrentDirectory(), provider.GetRequiredService<IOptions<CodexExecutorOptions>>().Value.WorkingDirectory);
        Assert.Equal(Directory.GetCurrentDirectory(), provider.GetRequiredService<IOptions<ClaudeExecutorOptions>>().Value.WorkingDirectory);
    }
}
