namespace Runiq.Teams.Models.Execution;

/// <summary>
/// Agent team yürütme planındaki tek bir üye çalıştırma adımını temsil eder.
/// </summary>
public sealed record TeamExecutionPlanStep
{
    /// <summary>
    /// Yeni bir team execution plan adımı oluşturur.
    /// </summary>
    public TeamExecutionPlanStep(
        string agentId,
        string role,
        string reason,
        int order,
        bool isFinalMember)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent id cannot be empty.", nameof(agentId));
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("Role cannot be empty.", nameof(role));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason cannot be empty.", nameof(reason));
        }

        AgentId = agentId.Trim();
        Role = role.Trim();
        Reason = reason.Trim();
        Order = order;
        IsFinalMember = isFinalMember;
    }

    /// <summary>
    /// Çalıştırılacak agent kimliğidir.
    /// </summary>
    public string AgentId { get; }

    /// <summary>
    /// Agent'ın plan içindeki rol adıdır.
    /// </summary>
    public string Role { get; }

    /// <summary>
    /// Bu agent'ın neden seçildiğini açıklayan kısa gerekçedir.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Plan içindeki çalıştırma sırasıdır.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Bu adımın kullanıcıya dönecek final cevabı üretip üretmeyeceğini belirtir.
    /// </summary>
    public bool IsFinalMember { get; }
}
