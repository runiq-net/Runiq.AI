using Runiq.AI.Memory.PostgreSql.TestHost.Providers;
using Runiq.AI.Memory.PostgreSql.TestHost.Runtime;
using Runiq.AI.Memory.PostgreSql.TestHost.Security;
using Runiq.AI.Memory.PostgreSql.TestHost.Tools;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Agents;
using Runiq.AI.Core;
using Runiq.AI.Core.AI.Chat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.PostgreSql.DependencyInjection;
using Runiq.AI.Memory.PostgreSql.TestHost;

var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
builder.Services.AddScoped<IMemoryAccessPolicy, TestAccessPolicy>();
builder.Services.AddScoped<PersistenceProbe>();
builder.Services.AddScoped<ConversationProbe>();
builder.Services.AddScoped<IMemoryIdentityResolver, TestIdentity>();
builder.Services.AddRuniqServer(o => o.AddAgent(new Agent("agent", "Agent", "instructions", "openai/test", "test-key").UseMemory().AddTool<LookupTool>()));
builder.Services.AddSingleton<ProbeModel>();
builder.Services.AddSingleton<IChatClientResolver>(p => p.GetRequiredService<ProbeModel>());
builder.Services.AddRuniqMemoryPostgreSql(options =>
{
    options.ConnectionString = Environment.GetEnvironmentVariable("RUNIQ_MEMORY_TEST_CONNECTION")!;
    options.Schema = args[1];
});
using var host = builder.Build();
await host.StartAsync();
using (var scope = host.Services.CreateScope())
    if (args[0].StartsWith("model-", StringComparison.Ordinal))
        await scope.ServiceProvider.GetRequiredService<ConversationProbe>().RunAsync(args);
    else
        await scope.ServiceProvider.GetRequiredService<PersistenceProbe>().RunAsync(args);
await host.StopAsync();
