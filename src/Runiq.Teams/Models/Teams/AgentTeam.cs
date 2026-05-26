namespace Runiq.Teams.Models.Teams;

/// <summary>
/// Birden fazla uzman agent'ın koordineli şekilde çalıştırıldığı agent takımını temsil eder.
/// </summary>
public sealed class AgentTeam
{
    private readonly List<TeamMember> _members = [];

    /// <summary>
    /// Agent takımını oluşturur.
    /// </summary>
    /// <param name="id">Takım kimliği.</param>
    /// <param name="name">Takım görünen adı.</param>
    /// <param name="instructions">Takımın genel çalışma yönergesi.</param>
    public AgentTeam(
        string id,
        string name,
        string instructions)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Team id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Team name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(instructions))
        {
            throw new ArgumentException("Team instructions cannot be empty.", nameof(instructions));
        }

        Id = id.Trim();
        Name = name.Trim();
        Instructions = instructions.Trim();
    }

    /// <summary>
    /// Takım kimliği.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Takım görünen adı.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Takımın genel çalışma yönergesi.
    /// </summary>
    public string Instructions { get; }

    /// <summary>
    /// Takım yürütme modu.
    /// </summary>
    public TeamExecutionMode ExecutionMode { get; private set; } = TeamExecutionMode.Sequential;

    /// <summary>
    /// Takım üyeleri.
    /// </summary>
    public IReadOnlyList<TeamMember> Members => _members;

    /// <summary>
    /// Takımı sıralı yürütme moduna ayarlar.
    /// </summary>
    /// <returns>Akıcı yapılandırma için mevcut takım örneği.</returns>
    public AgentTeam UseSequentialMode()
    {
        ExecutionMode = TeamExecutionMode.Sequential;
        return this;
    }

    /// <summary>
    /// Takımı adaptif yürütme moduna ayarlar.
    /// </summary>
    /// <returns>Akıcı yapılandırma için mevcut takım örneği.</returns>
    public AgentTeam UseAdaptiveMode()
    {
        ExecutionMode = TeamExecutionMode.Adaptive;
        return this;
    }

    /// <summary>
    /// Takıma yeni bir agent üyesi ekler.
    /// </summary>
    /// <param name="agentId">Kayıtlı agent kimliği.</param>
    /// <param name="role">Agent'ın takım içindeki rol adı.</param>
    /// <param name="instructions">Bu üyeye özel ek yürütme yönergesi.</param>
    /// <returns>Akıcı yapılandırma için mevcut takım örneği.</returns>
    public AgentTeam AddMember(
        string agentId,
        string role,
        string? instructions = null)
    {
        var member = new TeamMember(agentId, role, instructions);
        _members.Add(member);

        return this;
    }
}
