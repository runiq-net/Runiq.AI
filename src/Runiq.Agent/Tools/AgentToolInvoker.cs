using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Runiq.Agents.Tools;

/// <summary>
/// Agent'a bağlı typed tool kayıtlarını runtime sırasında çalıştırır.
/// </summary>
public sealed class AgentToolInvoker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Tool instance'larını DI üzerinden oluşturabilecek invoker örneğini başlatır.
    /// </summary>
    /// <param name="serviceProvider">Tool bağımlılıklarını çözecek servis sağlayıcı.</param>
    public AgentToolInvoker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Verilen agent üzerinde tanımlı tool'u JSON argümanlarıyla çalıştırır.
    /// </summary>
    /// <param name="agent">Tool kaydını taşıyan agent.</param>
    /// <param name="toolName">Model tarafından çağrılan tool adı.</param>
    /// <param name="argumentsJson">Model tarafından üretilen tool input JSON değeri.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Tool çalıştırma sonucunu JSON output olarak döner.</returns>
    public async Task<AgentToolInvocationResult> InvokeAsync(
        Agent agent,
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (string.IsNullOrWhiteSpace(toolName))
        {
            return AgentToolInvocationResult.Failure(
                "ToolNameRequired",
                "Tool name cannot be empty.");
        }

        var tool = agent.Tools.FirstOrDefault(candidate =>
            candidate.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));

        if (tool is null)
        {
            return AgentToolInvocationResult.Failure(
                "ToolNotFound",
                $"Agent '{agent.Id}' does not have a tool named '{toolName}'.");
        }

        return await InvokeAsync(
            tool,
            argumentsJson,
            cancellationToken);
    }

    /// <summary>
    /// Verilen typed tool kaydını JSON argümanlarıyla doğrudan çalıştırır.
    /// </summary>
    /// <param name="tool">Çalıştırılacak tool kaydıdır.</param>
    /// <param name="argumentsJson">Tool input JSON değeridir.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Tool çalıştırma sonucunu JSON output olarak döner.</returns>
    public async Task<AgentToolInvocationResult> InvokeAsync(
        AgentToolRegistration tool,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);

        try
        {
            var outputJson = await InvokeCoreAsync(
                tool,
                string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson,
                cancellationToken);

            return AgentToolInvocationResult.Success(outputJson);
        }
        catch (JsonException exception)
        {
            return AgentToolInvocationResult.Failure(
                "ToolInputInvalid",
                $"Tool '{tool.Name}' input could not be deserialized. {exception.Message}");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            return AgentToolInvocationResult.Failure(
                "ToolExecutionFailed",
                exception.InnerException.Message);
        }
        catch (Exception exception)
        {
            return AgentToolInvocationResult.Failure(
                "ToolExecutionFailed",
                exception.Message);
        }
    }

    private async Task<string> InvokeCoreAsync(
        AgentToolRegistration tool,
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        var input = JsonSerializer.Deserialize(
            argumentsJson,
            tool.InputType,
            JsonOptions);

        if (input is null)
        {
            throw new JsonException(
                $"Tool '{tool.Name}' input JSON produced a null value.");
        }

        var toolInstance = ActivatorUtilities.CreateInstance(
            _serviceProvider,
            tool.ToolType);

        var executeMethod = tool.ToolType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method =>
                method.Name == nameof(IRuniqTool<object, object>.ExecuteAsync) &&
                method.GetParameters().Length == 2);

        var taskObject = executeMethod.Invoke(
            toolInstance,
            [input, cancellationToken]);

        if (taskObject is not Task task)
        {
            throw new InvalidOperationException(
                $"Tool '{tool.Name}' ExecuteAsync method did not return a Task.");
        }

        await task.ConfigureAwait(false);

        var resultProperty = task.GetType().GetProperty("Result");

        if (resultProperty is null)
        {
            return "{}";
        }

        var output = resultProperty.GetValue(task);

        return JsonSerializer.Serialize(
            output,
            tool.OutputType,
            JsonOptions);
    }
}

/// <summary>
/// Runtime tool çalıştırma sonucunu temsil eder.
/// </summary>
public sealed record AgentToolInvocationResult(
    bool IsSuccess,
    string? OutputJson,
    string? ErrorCode,
    string? ErrorMessage)
{
    /// <summary>
    /// Başarılı tool çalıştırma sonucu üretir.
    /// </summary>
    public static AgentToolInvocationResult Success(string outputJson)
    {
        return new AgentToolInvocationResult(
            IsSuccess: true,
            OutputJson: outputJson,
            ErrorCode: null,
            ErrorMessage: null);
    }

    /// <summary>
    /// Başarısız tool çalıştırma sonucu üretir.
    /// </summary>
    public static AgentToolInvocationResult Failure(
        string errorCode,
        string errorMessage)
    {
        return new AgentToolInvocationResult(
            IsSuccess: false,
            OutputJson: null,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }
}