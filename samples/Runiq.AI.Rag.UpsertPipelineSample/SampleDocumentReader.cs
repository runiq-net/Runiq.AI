namespace Runiq.AI.Rag.UpsertPipelineSample;

/// <summary>
/// Reads the checked-in sample document instead of generating demo content at runtime, so the
/// upsert pipeline sample always processes an input a developer can inspect in the repository.
/// </summary>
public static class SampleDocumentReader
{
    /// <summary>
    /// Defines the repository-relative path expected by the sample and its tests.
    /// </summary>
    public static readonly string RepositoryRelativePath = Path.Combine(
        "samples",
        "Runiq.AI.Rag.UpsertPipelineSample",
        "sample-document.txt");

    /// <summary>
    /// Finds the sample document from the build output first, then from ancestor repository directories.
    /// </summary>
    /// <param name="baseDirectory">The directory to start searching from. Defaults to the application base directory.</param>
    /// <returns>The resolved sample document path.</returns>
    public static string ResolvePath(string? baseDirectory = null)
    {
        var startDirectory = Path.GetFullPath(baseDirectory ?? AppContext.BaseDirectory);
        var outputCopyPath = Path.Combine(startDirectory, "sample-document.txt");

        if (File.Exists(outputCopyPath))
        {
            return outputCopyPath;
        }

        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            var repositoryPath = Path.Combine(directory.FullName, RepositoryRelativePath);

            if (File.Exists(repositoryPath))
            {
                return repositoryPath;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find sample-document.txt in the build output or at '{RepositoryRelativePath}'.",
            outputCopyPath);
    }

    /// <summary>
    /// Reads the sample document from disk so the upsert pipeline sample uses the same input a
    /// developer can open and modify in the repository.
    /// </summary>
    /// <param name="baseDirectory">The directory to start searching from. Defaults to the application base directory.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the file read.</param>
    /// <returns>The resolved path and text content of the sample document.</returns>
    public static async Task<SampleDocumentContent> ReadAsync(
        string? baseDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(baseDirectory);
        var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        return new SampleDocumentContent(path, content);
    }
}

