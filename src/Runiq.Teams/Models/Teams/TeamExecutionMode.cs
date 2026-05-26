namespace Runiq.Teams.Models.Teams;

/// <summary>
/// Bir agent takımının üyelerini hangi yürütme stratejisiyle çalıştıracağını belirtir.
/// </summary>
public enum TeamExecutionMode
{
    /// <summary>
    /// Takım üyeleri tanımlandıkları sırayla, bir önceki üyenin çıktısı sonraki üyeye bağlam olarak verilerek çalıştırılır.
    /// </summary>
    Sequential = 0,

    /// <summary>
    /// Takım üyeleri kullanıcı isteğine göre model destekli bir planla seçilir ve sıralanır.
    /// </summary>
    Adaptive = 1
}
