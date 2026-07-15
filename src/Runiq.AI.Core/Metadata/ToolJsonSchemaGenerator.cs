using System.Reflection;

namespace Runiq.AI.Core.Metadata;

/// <summary>
/// Tool input ve output CLR tiplerinden Dashboard tarafindan kullanilacak basit JSON schema üretir.
/// </summary>
public static class ToolJsonSchemaGenerator
{
    private const int MaxDepth = 4;

    /// <summary>
    /// Verilen CLR tipinden JSON schema benzeri sade metadata üretir.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> CreateSchema(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return CreateSchema(type, depth: 0, visitedTypes: []);
    }

    /// <summary>
    /// Verilen input tipinin form alani gerektirip gerektirmedigini döner.
    /// </summary>
    public static bool HasInput(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (IsEmptyToolInput(type))
        {
            return false;
        }

        var effectiveType = Nullable.GetUnderlyingType(type) ?? type;

        if (TryGetPrimitiveSchema(effectiveType, out _))
        {
            return true;
        }

        if (effectiveType.IsEnum)
        {
            return true;
        }

        if (effectiveType != typeof(string) &&
            typeof(System.Collections.IEnumerable).IsAssignableFrom(effectiveType))
        {
            return true;
        }

        return effectiveType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Any(property => property.GetMethod is not null);
    }

    private static IReadOnlyDictionary<string, object?> CreateSchema(
        Type type,
        int depth,
        HashSet<Type> visitedTypes)
    {
        var effectiveType = Nullable.GetUnderlyingType(type) ?? type;

        if (IsEmptyToolInput(effectiveType))
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["title"] = "Empty Tool Input",
                ["properties"] = new Dictionary<string, object?>()
            };
        }

        if (depth > MaxDepth)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["title"] = effectiveType.Name
            };
        }

        if (TryGetPrimitiveSchema(effectiveType, out var primitiveSchema))
        {
            return primitiveSchema;
        }

        if (effectiveType.IsEnum)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["title"] = ToDisplayName(effectiveType.Name),
                ["enum"] = Enum.GetNames(effectiveType)
            };
        }

        if (effectiveType != typeof(string) &&
            typeof(System.Collections.IEnumerable).IsAssignableFrom(effectiveType))
        {
            var itemType = GetEnumerableItemType(effectiveType) ?? typeof(object);

            return new Dictionary<string, object?>
            {
                ["type"] = "array",
                ["title"] = ToDisplayName(effectiveType.Name),
                ["items"] = CreateSchema(itemType, depth + 1, visitedTypes)
            };
        }

        if (!visitedTypes.Add(effectiveType))
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["title"] = ToDisplayName(effectiveType.Name)
            };
        }

        var properties = effectiveType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetMethod is not null)
            .OrderBy(property => property.MetadataToken)
            .ToArray();

        var propertySchemas = new Dictionary<string, object?>(StringComparer.Ordinal);
        var required = new List<string>();
        var nullabilityContext = new NullabilityInfoContext();

        foreach (var property in properties)
        {
            var propertyName = ToJsonPropertyName(property.Name);
            var propertySchema = CreateSchema(
                property.PropertyType,
                depth + 1,
                visitedTypes);

            propertySchemas[propertyName] = AddTitle(
                propertySchema,
                ToDisplayName(property.Name));

            if (IsRequired(property, nullabilityContext))
            {
                required.Add(propertyName);
            }
        }

        visitedTypes.Remove(effectiveType);

        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["title"] = ToDisplayName(effectiveType.Name),
            ["properties"] = propertySchemas
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }

    private static bool TryGetPrimitiveSchema(
        Type type,
        out IReadOnlyDictionary<string, object?> schema)
    {
        if (type == typeof(string) ||
            type == typeof(Guid) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset))
        {
            schema = new Dictionary<string, object?>
            {
                ["type"] = "string"
            };

            return true;
        }

        if (type == typeof(bool))
        {
            schema = new Dictionary<string, object?>
            {
                ["type"] = "boolean"
            };

            return true;
        }

        if (type == typeof(byte) ||
            type == typeof(short) ||
            type == typeof(int) ||
            type == typeof(long))
        {
            schema = new Dictionary<string, object?>
            {
                ["type"] = "integer"
            };

            return true;
        }

        if (type == typeof(float) ||
            type == typeof(double) ||
            type == typeof(decimal))
        {
            schema = new Dictionary<string, object?>
            {
                ["type"] = "number"
            };

            return true;
        }

        schema = new Dictionary<string, object?>
        {
            ["type"] = "object"
        };

        return false;
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
        PropertyInfo property,
        NullabilityInfoContext nullabilityContext)
    {
        if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
        {
            return false;
        }

        if (property.PropertyType.IsValueType)
        {
            return true;
        }

        var nullability = nullabilityContext.Create(property);

        return nullability.ReadState == NullabilityState.NotNull;
    }

    private static Type? GetEnumerableItemType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType &&
            type.GetGenericArguments().Length == 1)
        {
            return type.GetGenericArguments()[0];
        }

        return type
            .GetInterfaces()
            .Where(interfaceType =>
                interfaceType.IsGenericType &&
                interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(interfaceType => interfaceType.GetGenericArguments()[0])
            .FirstOrDefault();
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

        var characters = new List<char>();

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];

            if (index > 0 && char.IsUpper(current))
            {
                characters.Add(' ');
            }

            characters.Add(current);
        }

        return new string(characters.ToArray());
    }
    private static bool IsEmptyToolInput(Type type)
    {
        return string.Equals(
            type.Name,
            "EmptyToolInput",
            StringComparison.Ordinal);
    }
}