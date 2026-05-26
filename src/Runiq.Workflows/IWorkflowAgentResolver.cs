using Runiq.Agents;

namespace Runiq.Workflows;

/// <summary>
/// Workflow adımlarında tanımlı agent tiplerini çalışma zamanında agent instance'larına çözer.
/// </summary>
public interface IWorkflowAgentResolver
{
    /// <summary>
    /// Verilen agent tipine karşılık gelen agent instance'ını döner.
    /// </summary>
    Agent Resolve(Type agentType);
}