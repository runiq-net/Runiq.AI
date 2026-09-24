using Runiq.AI.Agents.Tools;

namespace Runiq.AI.LocalCliAgents.Tools;

/// <summary>Calculates change totals from supplied data without reading or modifying files.</summary>
[RuniqTool("change_summary", "Calculates total files, added lines and deleted lines from a supplied list of file changes.")]
public sealed class ChangeSummaryTool : IRuniqTool<ChangeSummaryInput, ChangeSummaryOutput>
{
    /// <inheritdoc />
    public Task<ChangeSummaryOutput> ExecuteAsync(ChangeSummaryInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        if (input.Files is null || input.Files.Length > 1000)
            throw new ArgumentException("Supply at most 1000 file changes.", nameof(input));
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long added = 0, deleted = 0;
        foreach (var file in input.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (file is null || string.IsNullOrWhiteSpace(file.Path) || file.Added < 0 || file.Deleted < 0 || !names.Add(file.Path.Trim()))
                throw new ArgumentException("Each file must have a unique name and non-negative line counts.", nameof(input));
            added += file.Added;
            deleted += file.Deleted;
        }
        return Task.FromResult(new ChangeSummaryOutput(names.Count, added, deleted));
    }
}

/// <summary>Contains the file changes to aggregate.</summary>
/// <param name="Files">One entry per changed file; an empty list produces zero totals.</param>
public sealed record ChangeSummaryInput(FileChange[] Files);

/// <summary>Describes line counts supplied by the user for one file.</summary>
/// <param name="Path">A display name; the tool never accesses this path.</param>
/// <param name="Added">Number of added lines.</param>
/// <param name="Deleted">Number of deleted lines.</param>
public sealed record FileChange(string Path, int Added, int Deleted);

/// <summary>Contains deterministic change totals.</summary>
/// <param name="Files">Number of changed files.</param>
/// <param name="Added">Total added lines.</param>
/// <param name="Deleted">Total deleted lines.</param>
public sealed record ChangeSummaryOutput(int Files, long Added, long Deleted);
