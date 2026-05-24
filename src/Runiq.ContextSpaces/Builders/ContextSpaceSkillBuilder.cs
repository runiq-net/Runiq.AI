using Runiq.ContextSpaces.Models;
using Runiq.ContextSpaces.Models.Skills;

namespace Runiq.ContextSpaces.Builders;

/// <summary>
/// Bir bağlam alanına skill kaynakları eklemek için kullanılan builder sınıfıdır.
/// </summary>
public sealed class ContextSpaceSkillBuilder
{
    private readonly List<ContextSpaceSkillSource> skillSources = [];

    /// <summary>
    /// Yerel dosya sisteminden okunacak bir skill kaynağı ekler.
    /// </summary>
    public ContextSpaceSkillBuilder FromFileSystem(
        string id,
        string name,
        string path)
    {
        skillSources.Add(ContextSpaceSkillSource.FileSystem(
            id: id,
            name: name,
            path: path));

        return this;
    }

    /// <summary>
    /// S3 uyumlu object storage üzerinden okunacak bir skill kaynağı ekler.
    /// </summary>
    public ContextSpaceSkillBuilder FromS3(
        string id,
        string name,
        string bucketName,
        string prefix)
    {
        skillSources.Add(ContextSpaceSkillSource.S3(
            id: id,
            name: name,
            bucketName: bucketName,
            prefix: prefix));

        return this;
    }

    /// <summary>
    /// Builder içinde toplanan skill kaynaklarını döner.
    /// </summary>
    internal IReadOnlyList<ContextSpaceSkillSource> Build()
    {
        return skillSources;
    }
}