namespace Runiq.Cli;

public static class ProjectNameValidator
{
    public static bool TryValidate(string? projectName, out string error)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            error = "Project name cannot be empty.";
            return false;
        }

        if (projectName is "." or "..")
        {
            error = "Project name must be a folder name, not a relative path.";
            return false;
        }

        if (projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || projectName.Contains(Path.DirectorySeparatorChar)
            || projectName.Contains(Path.AltDirectorySeparatorChar))
        {
            error = "Project name must be a valid folder name.";
            return false;
        }

        if (!IsValidCSharpIdentifier(projectName))
        {
            error = "Project name must start with a letter or underscore and contain only letters, numbers, or underscores.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsValidCSharpIdentifier(string value)
    {
        if (!IsIdentifierStart(value[0]))
        {
            return false;
        }

        return value
            .Skip(1)
            .All(IsIdentifierPart);
    }

    private static bool IsIdentifierStart(char character)
    {
        return character == '_' || char.IsLetter(character);
    }

    private static bool IsIdentifierPart(char character)
    {
        return character == '_' || char.IsLetterOrDigit(character);
    }
}
