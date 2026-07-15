namespace Runiq.AI.Agents.Configuration
{
    /// <summary>
    /// Agent'in model saglayicisina baglanirken kullanacagi opsiyonel endpoint ayarlarini tasir.
    /// </summary>
    public class ProviderOptions
    {
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
