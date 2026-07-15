using Runiq.AI.Core.Providers;
using Runiq.AI.Core.AI.Capabilities;

namespace Runiq.AI.Core.Models
{
    /// <summary>
    /// Agent model bilgisini provider/model formatindan ayristirilmis sekilde temsil eder.
    /// </summary>
    public sealed class ModelReference
    {
        private ModelReference(string providerName, string modelName, ModelCapability? capabilities = null, int? embeddingDimensions = null)
        {
            ProviderName = providerName;
            ModelName = modelName;
            Capabilities = capabilities;
            EmbeddingDimensions = embeddingDimensions;
        }

        /// <summary>
        /// Model saglayici adidir. Örnek: openai, ollama, groq.
        /// </summary>
        public string ProviderName { get; }

        /// <summary>
        /// Saglayici üzerinde çagrilacak model adidir. Örnek: gpt-5, llama3.2.
        /// </summary>
        public string ModelName { get; }

        /// <summary>Gets explicitly configured capabilities, or null when conservative defaults should be used.</summary>
        public ModelCapability? Capabilities { get; }

        /// <summary>Gets configured fixed embedding dimensions, or null when the provider selects them.</summary>
        public int? EmbeddingDimensions { get; }

        /// <summary>Returns a copy carrying explicit model capabilities.</summary>
        /// <param name="capabilities">The declared capabilities. No capability is inferred from another.</param>
        /// <param name="embeddingDimensions">The fixed embedding dimensions, when known.</param>
        /// <returns>An immutable model reference with capability overrides.</returns>
        public ModelReference WithCapabilities(ModelCapability capabilities, int? embeddingDimensions = null)
        {
            if (embeddingDimensions is < 1)
                throw new ArgumentOutOfRangeException(nameof(embeddingDimensions), "Embedding dimensions must be greater than zero.");

            return new ModelReference(ProviderName, ModelName, capabilities, embeddingDimensions);
        }

        /// <summary>Returns a copy that targets a configured provider-visible model name.</summary>
        /// <param name="modelName">The non-empty provider-visible model name.</param>
        /// <returns>An immutable reference preserving explicit capabilities and dimensions.</returns>
        public ModelReference WithModelName(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                throw new ArgumentException("Model name cannot be empty.", nameof(modelName));

            return new ModelReference(ProviderName, modelName.Trim(), Capabilities, EmbeddingDimensions);
        }

        /// <summary>
        /// Model referansini provider/model formatindan ayristirir ve dogrular.
        /// </summary>
        public static ModelReference Parse(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                throw new ArgumentException("Model cannot be empty.", nameof(model));
            }

            var parts = model.Split('/', 2, StringSplitOptions.TrimEntries);

            if (parts.Length != 2 ||
                string.IsNullOrWhiteSpace(parts[0]) ||
                string.IsNullOrWhiteSpace(parts[1]))
            {
                throw new ArgumentException(
                    "Model must be in 'provider/model' format. Example: openai/gpt-5 or ollama/llama3.2.",
                    nameof(model));
            }

            var providerName = parts[0].Trim().ToLowerInvariant();
            var modelName = parts[1].Trim();

            ProviderDefaults.EnsureSupported(providerName);

            return new ModelReference(providerName, modelName);
        }
    }
}
