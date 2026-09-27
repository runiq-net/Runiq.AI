using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runiq.AI.Agents;
using Runiq.AI.Core;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.DependencyInjection;
using Runiq.AI.MemoryConversation.Providers;
using Runiq.AI.MemoryConversation.Security;
using Runiq.AI.MemoryConversation.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddRuniqMemory();
builder.Services.AddRuniqMemoryInMemory();
builder.Services.AddScoped<IMemoryIdentityResolver, DemoIdentity>();
builder.Services.AddScoped<IMemoryAccessPolicy, DemoAccessPolicy>();
builder.Services.AddRuniqServer(options => options.AddAgent(
    new Agent("conversation", "Conversation", "Remember the user's name.", "openai/demo", "unused-demo-key").UseMemory()));
builder.Services.AddSingleton<IChatClientResolver, DeterministicModel>();
builder.Services.AddScoped<ConversationDemo>();
using var host = builder.Build();
using var scope = host.Services.CreateScope();
await scope.ServiceProvider.GetRequiredService<ConversationDemo>().RunAsync();
