namespace Runiq.AI.Agents.Tools;

/// <summary>
/// Agent üzerine code-first tool eklemek için kullanilan extension metotlarini içerir.
/// </summary>
public static class AgentToolExtensions
{
    /// <summary>
    /// Agent'a tek bir typed Runiq tool ekler.
    /// </summary>
    /// <typeparam name="TTool">IRuniqTool&lt;TInput,TOutput&gt; uygulayan tool tipidir.</typeparam>
    /// <param name="agent">Tool eklenecek agent örnegidir.</param>
    /// <returns>Tool eklenmis agent örnegini döner.</returns>
    public static Agent AddTool<TTool>(this Agent agent)
        where TTool : class
    {
        ArgumentNullException.ThrowIfNull(agent);

        agent.AddToolRegistration(
            AgentToolRegistration.FromToolType(typeof(TTool)));

        return agent;
    }
}
