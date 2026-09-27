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
builder.Services.AddRuniqMemoryPostgreSql(options =>
{
    options.ConnectionString = Environment.GetEnvironmentVariable("RUNIQ_MEMORY_TEST_CONNECTION")!;
    options.Schema = args[1];
});
using var host = builder.Build();
await host.StartAsync();
using (var scope = host.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<PersistenceProbe>().RunAsync(args);
await host.StopAsync();
