using System.Runtime.CompilerServices;
using System.Text;

namespace Runiq.AI.Agents.Runtime.Codex;

internal static class CodexOutputReader
{
    internal static async IAsyncEnumerable<string> ReadLinesAsync(TextReader reader, int lineLimit, int totalLimit,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var line = new StringBuilder();
        long total = 0;
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) != 0)
        {
            total += count;
            if (total > totalLimit) throw new CodexException("CodexOutputLimitExceeded");
            for (var index = 0; index < count; index++)
            {
                if (buffer[index] == '\n')
                {
                    var value = line.ToString().TrimEnd('\r');
                    line.Clear();
                    if (value.Length != 0) yield return value;
                }
                else
                {
                    if (line.Length >= lineLimit) throw new CodexException("CodexOutputLimitExceeded");
                    line.Append(buffer[index]);
                }
            }
        }
        if (line.Length != 0) yield return line.ToString().TrimEnd('\r');
    }

    internal static async Task<string> DrainErrorAsync(TextReader reader, CancellationToken cancellationToken)
    {
        // Continue draining after the retention limit so noisy stderr cannot deadlock the child.
        var retained = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) != 0)
            retained.Append(buffer, 0, Math.Min(count, 16_384 - retained.Length));
        return retained.ToString();
    }
}
