namespace Runiq.AI.Rag.Configuration;

/// <summary>Identifies a typed embedding model while preserving the existing effective string reference.</summary>
public readonly record struct RagEmbeddingModelReference
{
    /// <summary>Initializes a typed embedding model reference.</summary>
    /// <param name="providerName">The provider identity.</param>
    /// <param name="modelName">The provider-visible model name.</param>
    /// <param name="displayName">The safe display name.</param>
    public RagEmbeddingModelReference(string providerName, string modelName, string displayName)
    {
        ProviderName = Require(providerName, nameof(providerName));
        ModelName = Require(modelName, nameof(modelName));
        DisplayName = Require(displayName, nameof(displayName));
    }

    /// <summary>Gets the provider identity.</summary>
    public string? ProviderName { get; }
    /// <summary>Gets the provider-visible model name.</summary>
    public string? ModelName { get; }
    /// <summary>Gets the safe display name.</summary>
    public string? DisplayName { get; }
    /// <summary>Gets the effective provider/model reference.</summary>
    public string Reference => IsDefined ? $"{ProviderName}/{ModelName}" : string.Empty;
    internal bool IsDefined => !string.IsNullOrWhiteSpace(ProviderName) && !string.IsNullOrWhiteSpace(ModelName) && !string.IsNullOrWhiteSpace(DisplayName);

    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A non-empty value is required.", parameterName) : value.Trim();
}

/// <summary>Identifies a typed vector-store selection while preserving the existing effective string reference.</summary>
public readonly record struct RagVectorStoreReference
{
    /// <summary>Initializes a typed vector-store reference.</summary>
    /// <param name="reference">The effective registration reference.</param>
    /// <param name="storeType">The provider-independent store type.</param>
    /// <param name="displayName">The safe display name.</param>
    /// <param name="namedReference">The optional named store registration.</param>
    public RagVectorStoreReference(string reference, string storeType, string displayName, string? namedReference = null)
    {
        Reference = Require(reference, nameof(reference));
        StoreType = Require(storeType, nameof(storeType));
        DisplayName = Require(displayName, nameof(displayName));
        NamedReference = namedReference is null ? null : Require(namedReference, nameof(namedReference));
    }

    /// <summary>Gets the effective registration reference.</summary>
    public string? Reference { get; }
    /// <summary>Gets the provider-independent store type.</summary>
    public string? StoreType { get; }
    /// <summary>Gets the safe display name.</summary>
    public string? DisplayName { get; }
    /// <summary>Gets the optional named store registration.</summary>
    public string? NamedReference { get; }
    internal bool IsDefined => !string.IsNullOrWhiteSpace(Reference) && !string.IsNullOrWhiteSpace(StoreType) && !string.IsNullOrWhiteSpace(DisplayName);

    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A non-empty value is required.", parameterName) : value.Trim();
}
