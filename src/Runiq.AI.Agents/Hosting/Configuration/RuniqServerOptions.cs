using Runiq.AI.Agents;
using Runiq.AI.Agents.Tools;

namespace Runiq.AI.Core.Configuration;

/// <summary>
/// Runiq server tarafi runtime kayit seçeneklerini tasir.
/// </summary>
public sealed class RuniqServerOptions
{
    private readonly List<Agent> _agents = [];
    private readonly List<AgentToolRegistration> _tools = [];

    /// <summary>
    /// Host uygulamada tanimlanan agent kayitlarini döndürür.
    /// </summary>
    public IReadOnlyList<Agent> Agents => _agents;

    /// <summary>
    /// Host uygulamada agent'a baglanmadan dogrudan register edilmis tool kayitlarini döndürür.
    /// </summary>
    public IReadOnlyList<AgentToolRegistration> Tools => _tools;

    /// <summary>
    /// Runtime'a yeni bir agent kaydi ekler.
    /// </summary>
    public RuniqServerOptions AddAgent(Agent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        _agents.Add(agent);

        return this;
    }

    /// <summary>
    /// Runtime'a agent'a bagli olmak zorunda olmayan typed bir tool kaydi ekler.
    /// </summary>
    /// <typeparam name="TTool">IRuniqTool&lt;TInput,TOutput&gt; uygulayan tool tipidir.</typeparam>
    public RuniqServerOptions AddTool<TTool>()
        where TTool : class
    {
        _tools.Add(AgentToolRegistration.FromToolType(typeof(TTool)));

        return this;
    }
}

