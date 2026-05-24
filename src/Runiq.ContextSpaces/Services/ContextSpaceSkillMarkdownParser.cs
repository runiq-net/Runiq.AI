using Runiq.ContextSpaces.Models;
using Runiq.ContextSpaces.Models.Skills;

namespace Runiq.ContextSpaces.Services;

/// <summary>
/// SKILL.md içeriğini bağlam alanı skill modeline dönüştürür.
/// </summary>
public sealed class ContextSpaceSkillMarkdownParser
{
    /// <summary>
    /// Verilen SKILL.md içeriğini ayrıştırarak skill modeli oluşturur.
    /// </summary>
    public ContextSpaceSkill Parse(
        string content,
        string sourceId,
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException(
                "Skill content cannot be null, empty, or whitespace.",
                nameof(content));
        }

        var normalizedContent = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var frontMatter = ParseFrontMatter(normalizedContent, out var instructions);

        var id = GetRequiredFrontMatterValue(frontMatter, "name");
        var name = id;
        var description = GetOptionalFrontMatterValue(frontMatter, "description");
        var version = GetOptionalFrontMatterValue(frontMatter, "version");
        var tags = GetFrontMatterList(frontMatter, "tags");

        return new ContextSpaceSkill(
            id: id,
            name: name,
            description: description,
            version: version,
            tags: tags,
            instructions: instructions,
            sourceId: sourceId,
            relativePath: relativePath);
    }

    private static Dictionary<string, List<string>> ParseFrontMatter(
        string content,
        out string instructions)
    {
        if (!content.StartsWith("---\n", StringComparison.Ordinal))
        {
            throw new FormatException("SKILL.md content must start with YAML front matter.");
        }

        var frontMatterEndIndex = content.IndexOf("\n---\n", 4, StringComparison.Ordinal);

        if (frontMatterEndIndex < 0)
        {
            throw new FormatException("SKILL.md front matter closing delimiter was not found.");
        }

        var frontMatterContent = content[4..frontMatterEndIndex];
        instructions = content[(frontMatterEndIndex + "\n---\n".Length)..].Trim();

        if (string.IsNullOrWhiteSpace(instructions))
        {
            throw new FormatException("SKILL.md instructions cannot be empty.");
        }

        return ParseSimpleYaml(frontMatterContent);
    }

    private static Dictionary<string, List<string>> ParseSimpleYaml(string content)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? activeListKey = null;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.TrimStart().StartsWith("- ", StringComparison.Ordinal))
            {
                if (activeListKey is null)
                {
                    throw new FormatException("YAML list item was found before a list key.");
                }

                values[activeListKey].Add(Unquote(line.TrimStart()[2..].Trim()));
                continue;
            }

            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);

            if (separatorIndex < 0)
            {
                throw new FormatException($"YAML line is not supported: '{line}'.");
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new FormatException("YAML key cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                activeListKey = key;
                values[key] = [];
                continue;
            }

            activeListKey = null;
            values[key] = [Unquote(value)];
        }

        return values;
    }

    private static string GetRequiredFrontMatterValue(
        IReadOnlyDictionary<string, List<string>> frontMatter,
        string key)
    {
        var value = GetOptionalFrontMatterValue(frontMatter, key);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException($"SKILL.md front matter must contain '{key}'.");
        }

        return value;
    }

    private static string? GetOptionalFrontMatterValue(
        IReadOnlyDictionary<string, List<string>> frontMatter,
        string key)
    {
        if (!frontMatter.TryGetValue(key, out var values) || values.Count == 0)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(values[0])
            ? null
            : values[0].Trim();
    }

    private static IReadOnlyList<string> GetFrontMatterList(
        IReadOnlyDictionary<string, List<string>> frontMatter,
        string key)
    {
        if (!frontMatter.TryGetValue(key, out var values))
        {
            return [];
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) ||
             (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            return value[1..^1];
        }

        return value;
    }
}