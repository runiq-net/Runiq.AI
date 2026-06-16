using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Runiq.Core.Mcp;

/// <summary>
/// Handles dashboard MCP tool playground requests.
/// </summary>
public sealed class RuniqMcpToolRunApiHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider services;

    /// <summary>
    /// Creates a new MCP tool run API handler.
    /// </summary>
    public RuniqMcpToolRunApiHandler(IServiceProvider services)
    {
        this.services = services;
    }

    /// <summary>
    /// Runs an exposed MCP tool with dashboard-provided input.
    /// </summary>
    public async Task<IResult> RunAsync(
        string toolName,
        RuniqMcpToolRunRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return Results.BadRequest(new RuniqMcpToolRunResponse(
                IsSuccess: false,
                OutputJson: null,
                ErrorCode: "ToolNameRequired",
                ErrorMessage: "Tool name is required."));
        }

        var tool = RuniqMcpToolCatalog.FindTool(toolName);

        if (tool is null)
        {
            return Results.NotFound(new RuniqMcpToolRunResponse(
                IsSuccess: false,
                OutputJson: null,
                ErrorCode: "ToolNotFound",
                ErrorMessage: $"MCP tool '{toolName}' could not be found."));
        }

        try
        {
            var arguments = BindArguments(tool.Method, request?.Input, cancellationToken);
            var instance = tool.Method.IsStatic ? null : CreateToolInstance(tool.ToolType);
            var invocationResult = tool.Method.Invoke(instance, arguments);
            var output = await UnwrapInvocationResultAsync(invocationResult);

            return Results.Ok(new RuniqMcpToolRunResponse(
                IsSuccess: true,
                OutputJson: JsonSerializer.Serialize(output, SerializerOptions),
                ErrorCode: null,
                ErrorMessage: null));
        }
        catch (TargetInvocationException exception)
        {
            return Results.Ok(CreateFailureResponse(exception.InnerException ?? exception));
        }
        catch (Exception exception)
        {
            return Results.Ok(CreateFailureResponse(exception));
        }
    }

    private object?[] BindArguments(
        MethodInfo method,
        JsonElement? input,
        CancellationToken cancellationToken)
    {
        var inputElement = input is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null }
            ? input.Value
            : default;

        return method
            .GetParameters()
            .Select(parameter => BindArgument(parameter, inputElement, cancellationToken))
            .ToArray();
    }

    private static object? BindArgument(
        ParameterInfo parameter,
        JsonElement input,
        CancellationToken cancellationToken)
    {
        if (parameter.ParameterType == typeof(CancellationToken))
        {
            return cancellationToken;
        }

        var parameterName = parameter.Name ?? string.Empty;
        var propertyName = ToJsonPropertyName(parameterName);

        if (input.ValueKind == JsonValueKind.Object &&
            input.TryGetProperty(propertyName, out var propertyValue))
        {
            return DeserializeValue(propertyValue, parameter.ParameterType);
        }

        if (parameter.HasDefaultValue)
        {
            return parameter.DefaultValue;
        }

        return parameter.ParameterType.IsValueType
            ? Activator.CreateInstance(parameter.ParameterType)
            : null;
    }

    private static object? DeserializeValue(JsonElement value, Type targetType)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return JsonSerializer.Deserialize(
            value.GetRawText(),
            targetType,
            SerializerOptions);
    }

    private object CreateToolInstance(Type toolType)
    {
        return services.GetService(toolType) ??
            ActivatorUtilities.CreateInstance(services, toolType);
    }

    private static async Task<object?> UnwrapInvocationResultAsync(object? result)
    {
        if (result is null)
        {
            return null;
        }

        if (result is Task task)
        {
            await task.ConfigureAwait(false);

            var resultProperty = task.GetType().GetProperty("Result");

            return resultProperty?.GetValue(task);
        }

        var resultType = result.GetType();

        if (resultType.IsGenericType &&
            resultType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var asTaskMethod = resultType.GetMethod("AsTask", Type.EmptyTypes);
            var valueTaskResult = (Task?)asTaskMethod?.Invoke(result, null);

            if (valueTaskResult is null)
            {
                return null;
            }

            await valueTaskResult.ConfigureAwait(false);

            return valueTaskResult.GetType().GetProperty("Result")?.GetValue(valueTaskResult);
        }

        if (result is ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
            return null;
        }

        return result;
    }

    private static RuniqMcpToolRunResponse CreateFailureResponse(Exception exception)
    {
        return new RuniqMcpToolRunResponse(
            IsSuccess: false,
            OutputJson: null,
            ErrorCode: exception.GetType().Name,
            ErrorMessage: exception.Message);
    }

    private static string ToJsonPropertyName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
