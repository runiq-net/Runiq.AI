using Runiq.AI.Core.AI.Capabilities;

namespace Runiq.AI.Core.Configuration;

/// <summary>
/// Binds the stable, model-specific settings of a named provider model registration.
/// An empty <see cref="Capabilities"/> collection explicitly declares that the model supports no operations.
/// </summary>
public sealed class ProviderModelOptions
{
    /// <summary>Gets or sets the provider-visible model name.</summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the independently declared model capabilities. Duplicate entries are harmless because effective
    /// capabilities are reduced to flags; null allows provider defaults, while an empty list means no capabilities.
    /// </summary>
    public IList<ModelCapability>? Capabilities { get; set; }

    /// <summary>Gets or sets the fixed embedding dimensions, or null when the provider selects dimensions.</summary>
    public int? EmbeddingDimensions { get; set; }
}
