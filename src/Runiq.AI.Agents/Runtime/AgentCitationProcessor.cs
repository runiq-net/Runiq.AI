namespace Runiq.AI.Agents.Runtime;

internal static class AgentCitationProcessor
{
    internal static IReadOnlyList<AgentCitation> Validate(
        string response,
        AgentRuntimeContext context)
    {
        if (string.IsNullOrEmpty(response) || !context.HasContext)
        {
            return [];
        }

        var counts = Parse(response)
            .GroupBy(number => number)
            .ToDictionary(group => group.Key, group => group.Count());

        return context.RetrievedRagContext
            .Select((source, index) => new { Source = source, Index = index, Number = index + 1 })
            .Where(item => counts.ContainsKey(item.Number))
            .Select(item => new AgentCitation(
                item.Number,
                item.Source.Chunk.DocumentId,
                item.Source.Chunk.Id,
                context.RetrievalCorrelationId ?? string.Empty,
                item.Index,
                counts[item.Number],
                double.IsFinite(item.Source.RawScore) ? item.Source.RawScore : null,
                item.Source.Relevance is double relevance && double.IsFinite(relevance) && relevance is >= 0 and <= 1 ? relevance : null,
                string.IsNullOrWhiteSpace(item.Source.Metric) ? null : item.Source.Metric,
                string.IsNullOrWhiteSpace(item.Source.Metric) ? null : item.Source.HigherIsBetter))
            .ToArray();
    }

    internal static IReadOnlyList<int> Parse(string response)
    {
        var citations = new List<int>();

        for (var index = 0; index < response.Length; index++)
        {
            if (response[index] == '`' && !IsEscaped(response, index))
            {
                index = SkipMatchingBacktickRun(response, index);
                continue;
            }

            if (response[index] != '[' || IsEscaped(response, index))
            {
                continue;
            }

            var bracket = FindBracket(response, index);
            if (bracket.Close < 0)
            {
                continue;
            }

            if (bracket.Nested || (index > 0 && response[index - 1] == '!'))
            {
                index = SkipLinkDestination(response, bracket.Close);
                continue;
            }

            if (bracket.Close + 1 < response.Length && response[bracket.Close + 1] == '(')
            {
                index = SkipLinkDestination(response, bracket.Close);
                continue;
            }

            var span = response.AsSpan(index + 1, bracket.Close - index - 1);
            if (!ContainsOnlyAsciiDigits(span) || span[0] == '0' ||
                !int.TryParse(span, out var number) || number <= 0)
            {
                index = bracket.Close;
                continue;
            }

            citations.Add(number);
            index = bracket.Close;
        }

        return citations;
    }

    private static int SkipMatchingBacktickRun(string value, int start)
    {
        var length = 1;
        while (start + length < value.Length && value[start + length] == '`') length++;
        for (var index = start + length; index < value.Length; index++)
        {
            if (value[index] != '`') continue;
            var candidateLength = 1;
            while (index + candidateLength < value.Length && value[index + candidateLength] == '`') candidateLength++;
            if (candidateLength == length) return index + length - 1;
            index += candidateLength - 1;
        }

        return start + length - 1;
    }

    private static (int Close, bool Nested) FindBracket(string value, int start)
    {
        var depth = 1;
        var nested = false;
        for (var index = start + 1; index < value.Length; index++)
        {
            if (value[index] == '[' && !IsEscaped(value, index)) { depth++; nested = true; }
            else if (value[index] == ']' && !IsEscaped(value, index) && --depth == 0) return (index, nested);
        }

        return (-1, nested);
    }

    private static int SkipLinkDestination(string value, int bracketClose)
    {
        if (bracketClose + 1 >= value.Length || value[bracketClose + 1] != '(') return bracketClose;
        var depth = 1;
        for (var index = bracketClose + 2; index < value.Length; index++)
        {
            if (value[index] == '(' && !IsEscaped(value, index)) depth++;
            else if (value[index] == ')' && !IsEscaped(value, index) && --depth == 0) return index;
        }

        return value.Length - 1;
    }

    private static bool IsEscaped(string value, int index)
    {
        var slashCount = 0;
        while (index > 0 && value[--index] == '\\')
        {
            slashCount++;
        }

        return slashCount % 2 == 1;
    }

    private static bool ContainsOnlyAsciiDigits(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
