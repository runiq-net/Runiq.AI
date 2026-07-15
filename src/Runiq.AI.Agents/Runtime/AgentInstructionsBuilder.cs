using System.Text;

namespace Runiq.AI.Agents.Runtime;

/// <summary>
/// Agent sistem yonergelerini runtime context bilgileriyle zenginlestirir.
/// </summary>
internal static class AgentInstructionsBuilder
{
    /// <summary>
    /// Agent'in temel yonergelerini RAG arama sonuclariyla birlestirir.
    /// </summary>
    public static string Build(
        Agent agent,
        AgentRuntimeContext runtimeContext)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(runtimeContext);

        if (!runtimeContext.HasContext)
        {
            return agent.Instructions;
        }

        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(agent.Instructions))
        {
            builder.AppendLine(agent.Instructions.Trim());
            builder.AppendLine();
        }

        if (runtimeContext.RetrievedRagContext.Count > 0)
        {
            builder.AppendLine("## Runiq RAG Context");
            builder.AppendLine();
            builder.AppendLine("The following chunks were retrieved from the configured vector index for this user request.");
            builder.AppendLine("Prefer these chunks over general knowledge when they directly answer the request.");
            builder.AppendLine();

            foreach (var result in runtimeContext.RetrievedRagContext)
            {
                builder.AppendLine($"### Chunk: {result.Chunk.Id}");
                builder.AppendLine($"Document: {result.Chunk.DocumentId}");
                builder.AppendLine($"Score: {result.Score:0.##}");
                builder.AppendLine();
                builder.AppendLine(result.Chunk.Content.Trim());
                builder.AppendLine();
            }
        }

        return builder.ToString().Trim();
    }
}