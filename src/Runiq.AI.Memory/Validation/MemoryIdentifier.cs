namespace Runiq.AI.Memory.Validation;

internal static class MemoryIdentifier
{
    internal static string Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256 ||
            value != value.Trim() || value.Any(char.IsControl))
            throw new ArgumentException("Memory identifiers must contain 1-256 characters without surrounding whitespace or control characters.", parameterName);
        return value;
    }
}
