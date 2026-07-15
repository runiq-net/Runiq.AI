namespace Runiq.AI.Core.Configuration
{
    /// <summary>
    /// Agent'in model saglayicisina baglanirken kullanacagi opsiyonel endpoint ayarlarini tasir.
    /// </summary>
    public class ProviderOptions
    {
        /// <summary>
        /// Gets or sets named model registrations. A registration may supply a private model name, explicit
        /// capabilities, and fixed embedding dimensions without requiring a framework model catalog.
        /// </summary>
        public IDictionary<string, ProviderModelOptions>? Models { get; set; }
        /// <summary>
        /// Gets or sets the capabilities explicitly declared for the selected model. When null, conservative
        /// framework defaults are used; an empty list explicitly declares no capabilities.
        /// </summary>
        public IList<AI.Capabilities.ModelCapability>? Capabilities { get; set; }

        /// <summary>
        /// Gets or sets the fixed embedding vector dimension count for the selected model. Null permits the provider
        /// to select dimensions when embeddings are declared.
        /// </summary>
        public int? EmbeddingDimensions { get; set; }
        /// <summary>
        /// Varsayilan saglayici adresini ezmek için kullanilacak URL degeridir.
        /// Örnek: http://localhost:8090 veya https://api.openai.com/v1.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Istek zaman asimi süresidir. Bos birakilirsa varsayilan süre kullanilir.
        /// </summary>
        public TimeSpan? Timeout { get; set; }
    }
}
