using Runiq.AI.Agents.Tools;
using Runiq.AI.Memory.PostgreSql.TestHost.Providers;

namespace Runiq.AI.Memory.PostgreSql.TestHost.Tools;

[RuniqTool("lookup", "Returns a deterministic value.")]
internal sealed class LookupTool(ProbeModel model) : IRuniqTool<Dictionary<string, string>, string>
{
    /// <inheritdoc />
    public Task<string> ExecuteAsync(Dictionary<string, string> input, CancellationToken cancellationToken = default)
    {
        model.ToolCalls++;
        return Task.FromResult("record");
    }
}
