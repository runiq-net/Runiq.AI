using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Runiq.AI.Agents.Tools;

/// <summary>
/// Agent'a bagli typed tool kayitlarini runtime sirasinda çalistirir.
/// </summary>
public sealed class AgentToolInvoker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Tool instance'larini DI üzerinden olusturabilecek invoker örnegini baslatir.
    /// </summary>
    /// <param name="serviceProvider">Tool bagimliliklarini çözecek servis saglayici.</param>
    public AgentToolInvoker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Verilen agent üzerinde tanimli tool'u JSON argümanlariyla çalistirir.
    /// </summary>
    /// <param name="agent">Tool kaydini tasiyan agent.</param>
    /// <param name="toolName">Model tarafindan çagrilan tool adi.</param>
    /// <param name="argumentsJson">Model tarafindan üretilen tool input JSON degeri.</param>
    /// <param name="cancellationToken">Iptal belirteci.</param>
    /// <returns>Tool çalistirma sonucunu JSON output olarak döner.</returns>
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
    /// Verilen typed tool kaydini JSON argümanlariyla dogrudan çalistirir.
    /// </summary>
    /// <param name="tool">Çalistirilacak tool kaydidir.</param>
    /// <param name="argumentsJson">Tool input JSON degeridir.</param>
    /// <param name="cancellationToken">Iptal belirteci.</param>
    /// <returns>Tool çalistirma sonucunu JSON output olarak döner.</returns>
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
/// Runtime tool çalistirma sonucunu temsil eder.
/// </summary>
public sealed record AgentToolInvocationResult(
    bool IsSuccess,
    string? OutputJson,
    string? ErrorCode,
    string? ErrorMessage)
{
    /// <summary>
    /// Basarili tool çalistirma sonucu üretir.
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
    /// Basarisiz tool çalistirma sonucu üretir.
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
