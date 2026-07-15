using Runiq.AI.Core.Providers;

namespace Runiq.AI.Core.Models
{
    /// <summary>
    /// Agent model bilgisini provider/model formatindan ayristirilmis sekilde temsil eder.
    /// </summary>
    public sealed class ModelReference
    {
        private ModelReference(string providerName, string modelName)
        {
            ProviderName = providerName;
            ModelName = modelName;
        }

        /// <summary>
        /// Model saglayici adidir. Örnek: openai, ollama, groq.
        /// </summary>
        public string ProviderName { get; }

        /// <summary>
        /// Saglayici üzerinde çagrilacak model adidir. Örnek: gpt-5, llama3.2.
        /// </summary>
        public string ModelName { get; }

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
