namespace Runiq.AI.Agents.Tools;

/// <summary>
/// Bir agent'a eklenmis typed tool kaydini temsil eder.
/// </summary>
/// <param name="ToolType">Tool sinifinin CLR tipidir.</param>
/// <param name="InputType">Tool input modelinin CLR tipidir.</param>
/// <param name="OutputType">Tool output modelinin CLR tipidir.</param>
/// <param name="Name">Model tarafinda kullanilacak tool adidir.</param>
/// <param name="Description">Model tarafina gönderilecek tool açiklamasidir.</param>
public sealed record AgentToolRegistration(
    Type ToolType,
    Type InputType,
    Type OutputType,
    string Name,
    string Description)
{
    /// <summary>
    /// Verilen tool tipinden agent tool kaydi üretir.
    /// </summary>
    /// <param name="toolType">IRuniqTool&lt;TInput,TOutput&gt; uygulayan somut tool tipidir.</param>
    /// <returns>Agent'a eklenebilecek tool kayit bilgisini döner.</returns>
    public static AgentToolRegistration FromToolType(Type toolType)
    {
        ArgumentNullException.ThrowIfNull(toolType);

        if (toolType.IsAbstract || toolType.IsInterface)
        {
            throw new InvalidOperationException(
                $"Tool type '{toolType.FullName}' must be a concrete class.");
        }

        var toolInterfaces = toolType
            .GetInterfaces()
            .Where(type =>
                type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(IRuniqTool<,>))
            .ToArray();

        if (toolInterfaces.Length == 0)
        {
            throw new InvalidOperationException(
                $"Tool type '{toolType.FullName}' must implement IRuniqTool<TInput, TOutput>.");
        }

        if (toolInterfaces.Length > 1)
        {
            throw new InvalidOperationException(
                $"Tool type '{toolType.FullName}' must implement only one IRuniqTool<TInput, TOutput> interface.");
        }

        var attribute = Attribute.GetCustomAttribute(
            toolType,
            typeof(RuniqToolAttribute)) as RuniqToolAttribute;

        if (attribute is null)
        {
            throw new InvalidOperationException(
                $"Tool type '{toolType.FullName}' must be decorated with RuniqToolAttribute.");
        }

        var genericArguments = toolInterfaces[0].GetGenericArguments();

        return new AgentToolRegistration(
            ToolType: toolType,
            InputType: genericArguments[0],
            OutputType: genericArguments[1],
            Name: attribute.Name,
            Description: attribute.Description);
    }
}
