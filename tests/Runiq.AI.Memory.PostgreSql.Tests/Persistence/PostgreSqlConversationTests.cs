using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.PostgreSql.DependencyInjection;
using Runiq.AI.Memory.PostgreSql.Tests.Infrastructure;
using Runiq.AI.Memory.Tests.Persistence;

namespace Runiq.AI.Memory.PostgreSql.Tests.Persistence;

public sealed class PostgreSqlConversationTests : ConversationStoreScenarios
{
    private readonly PostgreSqlTestDatabase fixture = new();
    protected override ServiceProvider CreateHost() => new ServiceCollection()
        .AddSingleton<IMemoryAccessPolicy>(Policy).AddRuniqMemoryPostgreSql(o =>
        { o.ConnectionString = PostgreSqlTestDatabase.ConnectionString; o.Schema = fixture.Schema; }).BuildServiceProvider();

    public override async Task InitializeAsync()
    {
        await fixture.Migrator.MigrateAsync();
        await base.InitializeAsync();
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await fixture.DisposeAsync();
    }
}
