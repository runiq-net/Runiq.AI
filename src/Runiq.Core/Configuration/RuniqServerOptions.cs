using Runiq.Agents;
using Runiq.Agents.Tools;
using Runiq.ContextSpaces.Models.Sources;

namespace Runiq.Core.Configuration;

/// <summary>
/// Runiq server tarafı runtime kayıt seçeneklerini taşır.
/// </summary>
public sealed class RuniqServerOptions
{
    private readonly List<Agent> _agents = [];
    private readonly List<AgentToolRegistration> _tools = [];
    private readonly List<ContextSpace> _contextSpaces = [];

    /// <summary>
    /// Host uygulamada tanımlanan agent kayıtlarını döndürür.
    /// </summary>
    public IReadOnlyList<Agent> Agents => _agents;

    /// <summary>
    /// Host uygulamada agent'a bağlanmadan doğrudan register edilmiş tool kayıtlarını döndürür.
    /// </summary>
    public IReadOnlyList<AgentToolRegistration> Tools => _tools;

    /// <summary>
    /// Host uygulamada tanımlanan context space kayıtlarını döndürür.
    /// </summary>
    public IReadOnlyList<ContextSpace> ContextSpaces => _contextSpaces;

    /// <summary>
    /// Runtime'a yeni bir agent kaydı ekler.
    /// </summary>
    public RuniqServerOptions AddAgent(Agent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        _agents.Add(agent);

        return this;
    }

    /// <summary>
    /// Runtime'a agent'a bağlı olmak zorunda olmayan typed bir tool kaydı ekler.
    /// </summary>
    /// <typeparam name="TTool">IRuniqTool&lt;TInput,TOutput&gt; uygulayan tool tipidir.</typeparam>
    public RuniqServerOptions AddTool<TTool>()
        where TTool : class
    {
        _tools.Add(AgentToolRegistration.FromToolType(typeof(TTool)));

        return this;
    }

    /// <summary>
    /// Runtime'a yeni bir context space kaydı ekler.
    /// </summary>
    public RuniqServerOptions AddContextSpace(ContextSpace contextSpace)
    {
        ArgumentNullException.ThrowIfNull(contextSpace);

        if (_contextSpaces.Any(existing =>
                string.Equals(existing.Id, contextSpace.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"A context space with id '{contextSpace.Id}' is already registered.");
        }

        _contextSpaces.Add(contextSpace);

        return this;
    }
}
