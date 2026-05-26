using Runiq.Agents;

namespace Runiq.Workflows;

/// <summary>
/// Workflow adımlarında çözülen agent instance'larını çalıştıran sözleşmeyi temsil eder.
/// </summary>
public interface IWorkflowAgentExecutor
{
    /// <summary>
    /// Verilen agent'ı workflow adımı girdisiyle çalıştırır.
    /// </summary>
    Task<string> ExecuteAsync(
        Agent agent,
        string input,
        CancellationToken cancellationToken = default);
}