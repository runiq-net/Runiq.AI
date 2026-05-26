using Runiq.Teams.Models.Teams;

namespace Runiq.Teams.Tests.Models.Teams;

/// <summary>
/// Agent takım tanımının temel yapılandırma davranışlarını doğrular.
/// </summary>
public sealed class AgentTeamTests
{
    /// <summary>
    /// Geçerli bilgilerle oluşturulan takımın temel alanlarını doğru taşıdığını doğrular.
    /// </summary>
    [Fact]
    public void Constructor_ShouldCreateTeam_WhenValuesAreValid()
    {
        var team = new AgentTeam(
            id: "travel-team",
            name: "Travel Planning Team",
            instructions: "Create travel plans with specialized agents.");

        Assert.Equal("travel-team", team.Id);
        Assert.Equal("Travel Planning Team", team.Name);
        Assert.Equal("Create travel plans with specialized agents.", team.Instructions);
        Assert.Equal(TeamExecutionMode.Sequential, team.ExecutionMode);
        Assert.Empty(team.Members);
    }

    /// <summary>
    /// Takım kimliği boş verildiğinde hata fırlatıldığını doğrular.
    /// </summary>
    [Fact]
    public void Constructor_ShouldThrow_WhenIdIsEmpty()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentTeam(
                id: " ",
                name: "Travel Planning Team",
                instructions: "Create travel plans."));

        Assert.Equal("id", exception.ParamName);
    }

    /// <summary>
    /// Takım görünen adı boş verildiğinde hata fırlatıldığını doğrular.
    /// </summary>
    [Fact]
    public void Constructor_ShouldThrow_WhenNameIsEmpty()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentTeam(
                id: "travel-team",
                name: " ",
                instructions: "Create travel plans."));

        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    /// Takım yönergesi boş verildiğinde hata fırlatıldığını doğrular.
    /// </summary>
    [Fact]
    public void Constructor_ShouldThrow_WhenInstructionsAreEmpty()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentTeam(
                id: "travel-team",
                name: "Travel Planning Team",
                instructions: " "));

        Assert.Equal("instructions", exception.ParamName);
    }

    /// <summary>
    /// Takıma üye eklendiğinde üyelerin eklenme sırasının korunduğunu doğrular.
    /// </summary>
    [Fact]
    public void AddMember_ShouldAddMembersInOrder()
    {
        var team = new AgentTeam(
                id: "travel-team",
                name: "Travel Planning Team",
                instructions: "Create travel plans.")
            .AddMember("research-agent", "Researcher")
            .AddMember("planner-agent", "Planner")
            .AddMember("review-agent", "Reviewer");

        Assert.Collection(
            team.Members,
            first =>
            {
                Assert.Equal("research-agent", first.AgentId);
                Assert.Equal("Researcher", first.Role);
            },
            second =>
            {
                Assert.Equal("planner-agent", second.AgentId);
                Assert.Equal("Planner", second.Role);
            },
            third =>
            {
                Assert.Equal("review-agent", third.AgentId);
                Assert.Equal("Reviewer", third.Role);
            });
    }

    /// <summary>
    /// Sıralı yürütme modunun akıcı yapılandırma ile ayarlanabildiğini doğrular.
    /// </summary>
    [Fact]
    public void UseSequentialMode_ShouldReturnSameTeam()
    {
        var team = new AgentTeam(
            id: "travel-team",
            name: "Travel Planning Team",
            instructions: "Create travel plans.");

        var configuredTeam = team.UseSequentialMode();

        Assert.Same(team, configuredTeam);
        Assert.Equal(TeamExecutionMode.Sequential, configuredTeam.ExecutionMode);
    }

    /// <summary>
    /// Adaptif yürütme modunun akıcı yapılandırma ile ayarlanabildiğini doğrular.
    /// </summary>
    [Fact]
    public void UseAdaptiveMode_ShouldReturnSameTeam()
    {
        var team = new AgentTeam(
            id: "travel-team",
            name: "Travel Planning Team",
            instructions: "Create travel plans.");

        var configuredTeam = team.UseAdaptiveMode();

        Assert.Same(team, configuredTeam);
        Assert.Equal(TeamExecutionMode.Adaptive, configuredTeam.ExecutionMode);
    }
}
