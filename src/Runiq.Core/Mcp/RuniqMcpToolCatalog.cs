using System.ComponentModel;
using System.Reflection;
using System.Text;
using Runiq.Core.Metadata;

namespace Runiq.Core.Mcp;

internal static class RuniqMcpToolCatalog
{
    private const string McpToolTypeAttributeName =
        "ModelContextProtocol.Server.McpServerToolTypeAttribute";

    private const string McpToolAttributeName =
        "ModelContextProtocol.Server.McpServerToolAttribute";

    public static IReadOnlyList<RuniqMcpToolDescriptor> DiscoverTools()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(GetLoadableTypes)
            .Where(HasMcpToolTypeAttribute)
            .SelectMany(type => type
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)
                .Where(HasMcpToolAttribute)
                .Select(method => CreateDescriptor(type, method)))
            .OrderBy(tool => tool.Name)
            .ToArray();
    }

    public static RuniqMcpToolDescriptor? FindTool(string toolName)
    {
        return DiscoverTools().FirstOrDefault(tool =>
            tool.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
    }

    public static RuniqMcpToolInfo ToInfo(RuniqMcpToolDescriptor descriptor)
    {
        return new RuniqMcpToolInfo
        {
            Name = descriptor.Name,
            Description = descriptor.Description,
            Source = "MCP",
            HasInput = descriptor.HasInput,
            InputSchema = descriptor.InputSchema
        };
    }

    private static RuniqMcpToolDescriptor CreateDescriptor(Type type, MethodInfo method)
    {
        var inputSchema = CreateInputSchema(method);

        return new RuniqMcpToolDescriptor(
            ToolType: type,
            Method: method,
            Name: ToSnakeCase(method.Name),
            Description: method.GetCustomAttribute<DescriptionAttribute>()?.Description,
            InputSchema: inputSchema,
            HasInput: HasInput(inputSchema));
    }

    private static IReadOnlyDictionary<string, object?> CreateInputSchema(MethodInfo method)
    {
        var parameters = method
            .GetParameters()
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
            .ToArray();

        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        var required = new List<string>();
        var nullabilityContext = new NullabilityInfoContext();

        foreach (var parameter in parameters)
        {
            var propertyName = ToJsonPropertyName(parameter.Name ?? $"arg{parameter.Position}");
            var schema = ToolJsonSchemaGenerator.CreateSchema(parameter.ParameterType);

            properties[propertyName] = AddTitle(
                schema,
                ToDisplayName(parameter.Name ?? propertyName));

            if (IsRequired(parameter, nullabilityContext))
            {
                required.Add(propertyName);
            }
        }

        var result = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["title"] = ToDisplayName(method.Name),
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            result["required"] = required;
        }

        return result;
    }

    private static bool HasInput(IReadOnlyDictionary<string, object?> schema)
    {
        return schema.TryGetValue("properties", out var properties) &&
            properties is IReadOnlyDictionary<string, object?> propertyDictionary &&
            propertyDictionary.Count > 0;
    }

    private static IReadOnlyDictionary<string, object?> AddTitle(
        IReadOnlyDictionary<string, object?> schema,
        string title)
    {
        var copy = new Dictionary<string, object?>(schema, StringComparer.Ordinal)
        {
            ["title"] = title
        };

        return copy;
    }

    private static bool IsRequired(
        ParameterInfo parameter,
        NullabilityInfoContext nullabilityContext)
    {
        if (parameter.HasDefaultValue)
        {
            return false;
        }

        if (Nullable.GetUnderlyingType(parameter.ParameterType) is not null)
        {
            return false;
        }

        if (parameter.ParameterType.IsValueType)
        {
            return true;
        }

        var nullability = nullabilityContext.Create(parameter);

        return nullability.ReadState == NullabilityState.NotNull;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }

    private static bool HasMcpToolTypeAttribute(Type type)
    {
        return type
            .GetCustomAttributes(inherit: false)
            .Any(attribute => attribute.GetType().FullName == McpToolTypeAttributeName);
    }

    private static bool HasMcpToolAttribute(MethodInfo method)
    {
        return method
            .GetCustomAttributes(inherit: false)
            .Any(attribute => attribute.GetType().FullName == McpToolAttributeName);
    }

    private static string ToJsonPropertyName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static string ToDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..])
            .Aggregate((left, right) => $"{left} {right}");
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 8);

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];

            if (char.IsUpper(current))
            {
                if (i > 0)
                {
                    var previous = value[i - 1];

                    if (previous != '_' &&
                        (!char.IsUpper(previous) ||
                         (i + 1 < value.Length && char.IsLower(value[i + 1]))))
                    {
                        builder.Append('_');
                    }
                }

                builder.Append(char.ToLowerInvariant(current));
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}
