namespace Runiq.AI.Rag.Ingestion;

/// <summary>Stores successful ingestion fingerprints for the lifetime of the configured RAG service provider.</summary>
public sealed class RagIngestionState
{
    internal object Gate { get; } = new();
    internal Dictionary<string, Entry> Entries { get; } = new(StringComparer.Ordinal);
    internal sealed record Entry(string Hash, IReadOnlyList<string> ChunkIds);

    internal void Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        var stored = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, PersistedEntry>>(File.ReadAllText(path));
        if (stored is null) return;
        lock (Gate) { Entries.Clear(); foreach (var pair in stored) Entries[pair.Key] = new Entry(pair.Value.Hash, pair.Value.ChunkIds); }
    }

    internal void Save(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var directory = Path.GetDirectoryName(Path.GetFullPath(path)); if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        Dictionary<string, PersistedEntry> copy; lock (Gate) copy = Entries.ToDictionary(pair => pair.Key, pair => new PersistedEntry(pair.Value.Hash, pair.Value.ChunkIds.ToArray()), StringComparer.Ordinal);
        var temporary = path + ".tmp"; File.WriteAllText(temporary, System.Text.Json.JsonSerializer.Serialize(copy)); File.Move(temporary, path, true);
    }

    private sealed record PersistedEntry(string Hash, IReadOnlyList<string> ChunkIds);
}
