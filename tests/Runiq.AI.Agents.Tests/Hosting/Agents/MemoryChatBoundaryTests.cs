using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Hosting.Memory;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Core;
using Runiq.AI.Core.Agents;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.DependencyInjection;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Agents.Tests.Hosting.Agents;

public sealed class MemoryChatBoundaryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    // HTTP and SSE preserve safe failures; fabricated body/header identity cannot authorize a conversation.
    public async Task HostedChat_DoesNotTrustClientIdentity(bool authenticated, bool streaming)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRuniqServer(x => x.AddAgent(new Agent("agent", "Agent", "instructions", "openai/model", "key").UseMemory()));
        services.AddRuniqMemory();
        services.AddRuniqHttpMemoryIdentity(new HttpMemoryIdentityOptions("application"));
        services.AddScoped<IMemoryOwnershipLookup, ForbiddenMetadata>();
        services.AddScoped<IMemoryAccessPolicy, ForbiddenPolicy>();
        services.AddScoped<IChatClientResolver, ForbiddenModel>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = new MemoryStream();
        context.Request.Headers["X-User-Id"] = "spoofed";
        if (authenticated)
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "verified")], "test"));
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        var request = JsonSerializer.Deserialize<AgentChatRequest>(
            """{"message":"question","callerId":"spoofed","tenantId":"spoofed","memory":{"resourceId":"other","threadId":"secret"}}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var result = await scope.ServiceProvider.GetRequiredService<AgentChatApiHandler>().ChatAsync("agent",
            request with { ResponseMode = streaming ? AgentChatResponseMode.Stream : AgentChatResponseMode.Result }, context, default);
        var expected = authenticated ? "MemoryReferenceRequired" : "MemoryIdentityRequired";
        if (streaming)
        {
            var payload = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
            Assert.Contains(expected, payload);
            Assert.EndsWith("data: [DONE]\n\n", payload);
            Assert.DoesNotContain("spoofed", payload);
            Assert.DoesNotContain("secret", payload);
            Assert.Single(payload.Split("\n\n", StringSplitOptions.RemoveEmptyEntries), x => x.Contains("\"type\":\"failed\""));
        }
        else
        {
            var response = Assert.IsType<AgentChatResponse>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
            Assert.Equal(expected, response.ErrorCode);
            Assert.Equal(AgentRunStatus.Failed, response.Status);
            Assert.NotNull(response.RunId);
        }
    }

    private sealed class ForbiddenMetadata : IMemoryOwnershipLookup
    {
        public ValueTask<MemoryThreadOwnership?> FindAsync(string boundaryId, string threadId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Metadata must not be read without a valid reference.");
    }
    private sealed class ForbiddenPolicy : IMemoryAccessPolicy
    {
        public ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Client DTOs must not create a resource request.");
    }
    private sealed class ForbiddenModel : IChatClientResolver
    {
        public IChatClient Resolve(ChatRequest request) => throw new InvalidOperationException("Model work must not start.");
    }
}
