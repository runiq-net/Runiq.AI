using Runiq.Agents;
using Runiq.Agents.Tools;

namespace Runiq.Core.Configuration;

/// <summary>
/// Runiq server tarafı runtime kayıt seçeneklerini taşır.
/// </summary>
public sealed class RuniqServerOptions
{
    private readonly List<Agent> _agents = [];
    private readonly List<AgentToolRegistration> _tools = [];

    /// <summary>
    /// Host uygulamada tanımlanan agent kayıtlarını döndürür.
    /// </summary>
    public IReadOnlyList<Agent> Agents => _agents;

    /// <summary>
    /// Host uygulamada agent'a bağlanmadan doğrudan register edilmiş tool kayıtlarını döndürür.
    /// </summary>
    public IReadOnlyList<AgentToolRegistration> Tools => _tools;

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
}