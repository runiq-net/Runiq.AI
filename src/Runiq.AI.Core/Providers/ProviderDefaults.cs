namespace Runiq.AI.Core.Providers
{
    /// <summary>
    /// Model saglayicilarinin Runiq tarafindan bilinen varsayilan baglanti ve protokol bilgilerini tutar.
    /// </summary>
    public static class ProviderDefaults
    {
        private static readonly IReadOnlyDictionary<string, ProviderDefault> Defaults =
            new Dictionary<string, ProviderDefault>(StringComparer.OrdinalIgnoreCase)
            {
                ["openai"] = new(
                    ProviderName: "openai",
                    Protocol: ProviderProtocol.OpenAICompatible,
                    DefaultUrl: "https://api.openai.com/v1",
                    RequiresApiKey: true),

                ["ollama"] = new(
                    ProviderName: "ollama",
                    Protocol: ProviderProtocol.Ollama,
                    DefaultUrl: "http://localhost:11434",
                    RequiresApiKey: false),

                ["groq"] = new(
                    ProviderName: "groq",
                    Protocol: ProviderProtocol.OpenAICompatible,
                    DefaultUrl: "https://api.groq.com/openai/v1",
                    RequiresApiKey: true),

                ["mistral"] = new(
                    ProviderName: "mistral",
                    Protocol: ProviderProtocol.OpenAICompatible,
                    DefaultUrl: "https://api.mistral.ai/v1",
                    RequiresApiKey: true),

                ["deepseek"] = new(
                    ProviderName: "deepseek",
                    Protocol: ProviderProtocol.OpenAICompatible,
                    DefaultUrl: "https://api.deepseek.com",
                    RequiresApiKey: true),

                ["openrouter"] = new(
                    ProviderName: "openrouter",
                    Protocol: ProviderProtocol.OpenAICompatible,
                    DefaultUrl: "https://openrouter.ai/api/v1",
                    RequiresApiKey: true),

                ["together"] = new(
                    ProviderName: "together",
                    Protocol: ProviderProtocol.OpenAICompatible,
                    DefaultUrl: "https://api.together.xyz/v1",
                    RequiresApiKey: true),

                ["fireworks"] = new(
                    ProviderName: "fireworks",
                    Protocol: ProviderProtocol.OpenAICompatible,
                    DefaultUrl: "https://api.fireworks.ai/inference/v1",
                    RequiresApiKey: true),

                ["nvidia"] = new(
                    ProviderName: "nvidia",
                    Protocol: ProviderProtocol.OpenAICompatible,
                    DefaultUrl: "https://integrate.api.nvidia.com/v1",
                    RequiresApiKey: true),

                ["azure-openai"] = new(
                    ProviderName: "azure-openai",
                    Protocol: ProviderProtocol.OpenAICompatible,
                    DefaultUrl: null,
                    RequiresApiKey: true)
            };

        /// <summary>
        /// Provider adinin Runiq tarafindan desteklenip desteklenmedigini dogrular.
        /// </summary>
        public static void EnsureSupported(string providerName)
        {
            var normalizedProviderName = NormalizeProviderName(providerName);

            if (!Defaults.ContainsKey(normalizedProviderName))
            {
                throw CreateUnsupportedProviderException(providerName);
            }
        }

        /// <summary>
        /// Provider adina karsilik gelen varsayilan saglayici bilgisini döndürür.
        /// </summary>
        public static ProviderDefault Get(string providerName)
        {
            var normalizedProviderName = NormalizeProviderName(providerName);

            if (Defaults.TryGetValue(normalizedProviderName, out var providerDefault))
            {
                return providerDefault;
            }

            throw CreateUnsupportedProviderException(providerName);
        }

        /// <summary>
        /// Provider için kullanilacak endpoint adresini çözer.
        /// Agent üzerinde özel URL verilmisse onu, aksi halde provider varsayilan URL degerini kullanir.
        /// </summary>
        public static Uri ResolveUrl(
            string providerName,
            string registrationId,
            string? configuredUrl)
        {
            var providerDefault = Get(providerName);

            if (!string.IsNullOrWhiteSpace(configuredUrl))
            {
                return CreateUri(configuredUrl, registrationId);
            }

            if (string.IsNullOrWhiteSpace(providerDefault.DefaultUrl))
            {
                throw new InvalidOperationException(
                    $"Runiq provider registration failed. Registration '{registrationId}' uses provider '{providerName}' but Provider.Url is missing.");
            }

            return CreateUri(providerDefault.DefaultUrl, registrationId);
        }

        private static string NormalizeProviderName(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be empty.", nameof(providerName));
            }

            return providerName.Trim().ToLowerInvariant();
        }

        private static Uri CreateUri(string url, string agentId)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException(
                    $"Runiq agent registration failed. Agent '{agentId}' has invalid provider url: '{url}'.");
            }

            return uri;
        }

        private static ArgumentException CreateUnsupportedProviderException(string providerName)
        {
            var supportedProviders = string.Join(", ", Defaults.Keys.OrderBy(key => key));

            return new ArgumentException(
                $"Unsupported model provider '{providerName}'. Supported providers: {supportedProviders}.",
                nameof(providerName));
        }
    }

    /// <summary>
    /// Runiq'in saglayiciyla hangi HTTP protokolü üzerinden konusacagini belirtir.
    /// </summary>
    public enum ProviderProtocol
    {
        /// <summary>
        /// OpenAI-compatible chat completions protocol.
        /// </summary>
        OpenAICompatible,

        /// <summary>
        /// Ollama local HTTP protocol.
        /// </summary>
        Ollama
    }

    /// <summary>
    /// Bir model saglayicisinin varsayilan protokol ve baglanti bilgilerini temsil eder.
    /// </summary>
    public sealed record ProviderDefault(
        string ProviderName,
        ProviderProtocol Protocol,
        string? DefaultUrl,
        bool RequiresApiKey);
}
