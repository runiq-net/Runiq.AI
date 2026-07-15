using System.Reflection;
using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Core.AI.Chat;

namespace Runiq.AI.Agents.Tests.Architecture;

public sealed class RuntimeMigrationArchitectureTests
{
    // Verifies that the foundational Core assembly cannot acquire a dependency on Agent orchestration.
    [Fact]
    public void Core_ShouldNotReferenceAgents()
    {
        var references = typeof(IChatClient).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => reference.Name == "Runiq.AI.Agents");
    }

    // Verifies that provider clients expose only provider-neutral chat models on their public methods.
    [Theory]
    [InlineData(typeof(OpenAICompatibleClient))]
    [InlineData(typeof(OpenAIResponsesClient))]
    public void ProviderClients_ShouldNotExposeAgentExecutionModels(Type clientType)
    {
        var exposedTypes = clientType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType));

        Assert.DoesNotContain(exposedTypes, type => type.FullName?.Contains("AgentExecution", StringComparison.Ordinal) == true || type.Name == "Agent");
    }

    // Verifies that the runtime stores the shared resolver and does not directly retain protocol clients.
    [Fact]
    public void Runtime_ShouldCommunicateThroughCoreChatResolver()
    {
        var fields = typeof(AgentExecutionRuntime).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.Contains(fields, field => field.FieldType == typeof(IChatClientResolver));
        Assert.DoesNotContain(fields, field => field.FieldType == typeof(OpenAICompatibleClient) || field.FieldType == typeof(OpenAIResponsesClient));
    }

    // Verifies that provider-neutral contracts have one public definition and no linked-source workaround.
    [Fact]
    public void ProviderNeutralContracts_ShouldHaveSingleCoreDefinition()
    {
        var assemblies = new[] { typeof(IChatClient).Assembly, typeof(AgentExecutionRuntime).Assembly };
        var contractNames = new[] { nameof(ChatUsage), nameof(ChatProviderException), nameof(ChatRequest), nameof(ChatResponse) };

        foreach (var name in contractNames)
        {
            Assert.Single(assemblies.SelectMany(assembly => assembly.GetExportedTypes()), type => type.Name == name);
        }

        var projectFile = FindRepositoryFile("src", "Runiq.AI.Agents", "Runiq.AI.Agents.csproj");
        Assert.DoesNotContain("<Compile Include=", File.ReadAllText(projectFile), StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that protocol DTO names cannot leak into AgentExecutionRuntime source.
    [Fact]
    public void Runtime_ShouldNotReferenceOpenAIProtocolDtos()
    {
        var runtimeFile = FindRepositoryFile("src", "Runiq.AI.Agents", "Runtime", "AgentExecutionRuntime.cs");
        var source = File.ReadAllText(runtimeFile);

        Assert.DoesNotContain("OpenAIChat", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ResponseRequest", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. segments]);
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file '{Path.Combine(segments)}'.");
    }
}
