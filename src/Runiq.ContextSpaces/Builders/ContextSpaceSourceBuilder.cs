using Runiq.ContextSpaces.Models.Sources;

namespace Runiq.ContextSpaces.Builders;

/// <summary>
/// Bir bağlam alanına bilgi kaynakları eklemek için kullanılan builder sınıfıdır.
/// </summary>
public sealed class ContextSpaceSourceBuilder
{
    private readonly List<ContextSpaceSource> sources = [];

    /// <summary>
    /// Yerel dosya sisteminden okunacak bir bilgi kaynağı ekler.
    /// </summary>
    public ContextSpaceSourceBuilder FromFileSystem(
        string id,
        string name,
        string path,
        string? description = null)
    {
        sources.Add(ContextSpaceSource.FileSystem(
            id: id,
            name: name,
            path: path,
            description: description));

        return this;
    }

    /// <summary>
    /// S3 uyumlu object storage üzerinden okunacak bir bilgi kaynağı ekler.
    /// </summary>
    public ContextSpaceSourceBuilder FromS3(
        string id,
        string name,
        string bucketName,
        string prefix,
        string? description = null)
    {
        sources.Add(ContextSpaceSource.S3(
            id: id,
            name: name,
            bucketName: bucketName,
            prefix: prefix,
            description: description));

        return this;
    }

    /// <summary>
    /// Builder içinde toplanan bilgi kaynaklarını döner.
    /// </summary>
    internal IReadOnlyList<ContextSpaceSource> Build()
    {
        return sources;
    }
}