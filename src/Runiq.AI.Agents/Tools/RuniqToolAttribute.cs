namespace Runiq.AI.Agents.Tools;

/// <summary>
/// Bir Runiq tool sinifinin model tarafinda hangi ad ve açiklama ile görünecegini belirtir.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RuniqToolAttribute : Attribute
{
    /// <summary>
    /// Tool adini açiklama olmadan olusturur.
    /// </summary>
    /// <param name="name">Model tarafina gönderilecek benzersiz tool adidir.</param>
    public RuniqToolAttribute(string name)
        : this(name, string.Empty)
    {
    }

    /// <summary>
    /// Tool adini ve açiklamasini olusturur.
    /// </summary>
    /// <param name="name">Model tarafina gönderilecek benzersiz tool adidir.</param>
    /// <param name="description">Tool'un ne ise yaradigini açiklayan kisa metindir.</param>
    public RuniqToolAttribute(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tool name cannot be empty.", nameof(name));
        }

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Model tarafinda kullanilacak tool adidir.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Model tarafina gönderilecek tool açiklamasidir.
    /// </summary>
    public string Description { get; }
}
