using Runiq.Teams.Models.Execution;

namespace Runiq.Teams.Tests.Execution;

/// <summary>
/// Agent team yürütme event modelinin factory davranışlarını doğrular.
/// </summary>
public sealed class TeamExecutionEventTests
{
    /// <summary>
    /// TeamStarted factory metodunun takım başlangıç event'ini doğru oluşturduğunu doğrular.
    /// </summary>
    [Fact]
    public void TeamStarted_ShouldCreateTeamStartedEvent()
    {
        var executionEvent = TeamExecutionEvent.TeamStarted(
            teamId: " travel-team ",
            teamName: " Travel Planning Team ");

        Assert.Equal(TeamExecutionEventType.TeamStarted, executionEvent.Type);
        Assert.Equal("travel-team", executionEvent.TeamId);
        Assert.Equal("Travel Planning Team", executionEvent.TeamName);
    }

    /// <summary>
    /// MemberStarted factory metodunun üye başlangıç event'ini doğru oluşturduğunu doğrular.
    /// </summary>
    [Fact]
    public void MemberStarted_ShouldCreateMemberStartedEvent()
    {
        var executionEvent = TeamExecutionEvent.MemberStarted(
            teamId: "travel-team",
            memberAgentId: "research-agent",
            memberRole: "Researcher");

        Assert.Equal(TeamExecutionEventType.MemberStarted, executionEvent.Type);
        Assert.Equal("travel-team", executionEvent.TeamId);
        Assert.Equal("research-agent", executionEvent.MemberAgentId);
        Assert.Equal("Researcher", executionEvent.MemberRole);
    }

    /// <summary>
    /// MemberDelta factory metodunun üye kısmi cevap event'ini doğru oluşturduğunu doğrular.
    /// </summary>
    [Fact]
    public void MemberDelta_ShouldCreateMemberDeltaEvent()
    {
        var executionEvent = TeamExecutionEvent.MemberDelta(
            teamId: "travel-team",
            memberAgentId: "research-agent",
            memberRole: "Researcher",
            content: "Found relevant source material.");

        Assert.Equal(TeamExecutionEventType.MemberDelta, executionEvent.Type);
        Assert.Equal("Found relevant source material.", executionEvent.Content);
        Assert.False(executionEvent.IsFinalMember);
    }

    /// <summary>
    /// MemberDelta factory metodunun final üye bilgisini event'e taşıdığını doğrular.
    /// </summary>
    [Fact]
    public void MemberDelta_ShouldSetIsFinalMember_WhenProvided()
    {
        var executionEvent = TeamExecutionEvent.MemberDelta(
            teamId: "travel-team",
            memberAgentId: "planner-agent",
            memberRole: "Travel Planner",
            content: "Final plan.",
            isFinalMember: true);

        Assert.Equal(TeamExecutionEventType.MemberDelta, executionEvent.Type);
        Assert.True(executionEvent.IsFinalMember);
    }

    /// <summary>
    /// MemberToolCallStarted factory metodunun tool başlangıç event'ini doğru oluşturduğunu doğrular.
    /// </summary>
    [Fact]
    public void MemberToolCallStarted_ShouldCreateToolCallStartedEvent()
    {
        var executionEvent = TeamExecutionEvent.MemberToolCallStarted(
            teamId: "travel-team",
            memberAgentId: "weather-agent",
            memberRole: "Weather Analyst",
            toolCallId: "call-1",
            toolName: "weather",
            argumentsJson: "{\"city\":\"Istanbul\"}");

        Assert.Equal(TeamExecutionEventType.MemberToolCallStarted, executionEvent.Type);
        Assert.Equal("weather-agent", executionEvent.MemberAgentId);
        Assert.Equal("Weather Analyst", executionEvent.MemberRole);
        Assert.Equal("call-1", executionEvent.ToolCallId);
        Assert.Equal("weather", executionEvent.ToolName);
        Assert.Equal("{\"city\":\"Istanbul\"}", executionEvent.ArgumentsJson);
    }

    /// <summary>
    /// MemberToolCallCompleted factory metodunun tool tamamlanma event'ini doğru oluşturduğunu doğrular.
    /// </summary>
    [Fact]
    public void MemberToolCallCompleted_ShouldCreateToolCallCompletedEvent()
    {
        var executionEvent = TeamExecutionEvent.MemberToolCallCompleted(
            teamId: "travel-team",
            memberAgentId: "weather-agent",
            memberRole: "Weather Analyst",
            toolCallId: "call-1",
            toolName: "weather",
            outputJson: "{\"condition\":\"Mild\"}");

        Assert.Equal(TeamExecutionEventType.MemberToolCallCompleted, executionEvent.Type);
        Assert.Equal("call-1", executionEvent.ToolCallId);
        Assert.Equal("weather", executionEvent.ToolName);
        Assert.Equal("{\"condition\":\"Mild\"}", executionEvent.OutputJson);
    }

    /// <summary>
    /// MemberToolCallFailed factory metodunun tool hata event'ini doğru oluşturduğunu doğrular.
    /// </summary>
    [Fact]
    public void MemberToolCallFailed_ShouldCreateToolCallFailedEvent()
    {
        var executionEvent = TeamExecutionEvent.MemberToolCallFailed(
            teamId: "travel-team",
            memberAgentId: "weather-agent",
            memberRole: "Weather Analyst",
            toolCallId: "call-1",
            toolName: "weather",
            errorMessage: "Tool failed.",
            errorCode: "ToolError");

        Assert.Equal(TeamExecutionEventType.MemberToolCallFailed, executionEvent.Type);
        Assert.Equal("call-1", executionEvent.ToolCallId);
        Assert.Equal("weather", executionEvent.ToolName);
        Assert.Equal("ToolError", executionEvent.ErrorCode);
        Assert.Equal("Tool failed.", executionEvent.ErrorMessage);
    }

    /// <summary>
    /// MemberDelta factory metodunun yalnızca boşluk içeren streaming parçasını koruduğunu doğrular.
    /// </summary>
    [Fact]
    public void MemberDelta_ShouldAllowWhitespaceContent()
    {
        var executionEvent = TeamExecutionEvent.MemberDelta(
            teamId: "travel-team",
            memberAgentId: "planner-agent",
            memberRole: "Travel Planner",
            content: " ");

        Assert.Equal(TeamExecutionEventType.MemberDelta, executionEvent.Type);
        Assert.Equal(" ", executionEvent.Content);
    }

    /// <summary>
    /// MemberDelta factory metodunun tamamen boş content değerini reddettiğini doğrular.
    /// </summary>
    [Fact]
    public void MemberDelta_ShouldThrow_WhenContentIsEmpty()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            TeamExecutionEvent.MemberDelta(
                teamId: "travel-team",
                memberAgentId: "planner-agent",
                memberRole: "Travel Planner",
                content: string.Empty));

        Assert.Equal("content", exception.ParamName);
    }

    /// <summary>
    /// MemberCompleted factory metodunun üye tamamlandı event'ini doğru oluşturduğunu doğrular.
    /// </summary>
    [Fact]
    public void MemberCompleted_ShouldCreateMemberCompletedEvent()
    {
        var executionEvent = TeamExecutionEvent.MemberCompleted(
            teamId: "travel-team",
            memberAgentId: "planner-agent",
            memberRole: "Planner",
            content: "Plan created.");

        Assert.Equal(TeamExecutionEventType.MemberCompleted, executionEvent.Type);
        Assert.Equal("planner-agent", executionEvent.MemberAgentId);
        Assert.Equal("Planner", executionEvent.MemberRole);
        Assert.Equal("Plan created.", executionEvent.Content);
    }

    /// <summary>
    /// MemberFailed factory metodunun üye hata event'ini doğru oluşturduğunu doğrular.
    /// </summary>
    [Fact]
    public void MemberFailed_ShouldCreateMemberFailedEvent()
    {
        var executionEvent = TeamExecutionEvent.MemberFailed(
            teamId: "travel-team",
            memberAgentId: "review-agent",
            memberRole: "Reviewer",
            errorMessage: "Review failed.",
            errorCode: "review_error");

        Assert.Equal(TeamExecutionEventType.MemberFailed, executionEvent.Type);
        Assert.Equal("review_error", executionEvent.ErrorCode);
        Assert.Equal("Review failed.", executionEvent.ErrorMessage);
    }

    /// <summary>
    /// TeamCompleted factory metodunun takım tamamlandı event'ini doğru oluşturduğunu doğrular.
    /// </summary>
    [Fact]
    public void TeamCompleted_ShouldCreateTeamCompletedEvent()
    {
        var executionEvent = TeamExecutionEvent.TeamCompleted(
            teamId: "travel-team",
            content: "Final answer.");

        Assert.Equal(TeamExecutionEventType.TeamCompleted, executionEvent.Type);
        Assert.Equal("travel-team", executionEvent.TeamId);
        Assert.Equal("Final answer.", executionEvent.Content);
    }

    /// <summary>
    /// TeamFailed factory metodunun takım hata event'ini doğru oluşturduğunu doğrular.
    /// </summary>
    [Fact]
    public void TeamFailed_ShouldCreateTeamFailedEvent()
    {
        var executionEvent = TeamExecutionEvent.TeamFailed(
            teamId: "travel-team",
            errorMessage: "Team execution failed.",
            errorCode: "team_error");

        Assert.Equal(TeamExecutionEventType.TeamFailed, executionEvent.Type);
        Assert.Equal("team_error", executionEvent.ErrorCode);
        Assert.Equal("Team execution failed.", executionEvent.ErrorMessage);
    }

    /// <summary>
    /// Zorunlu alanlar boş verildiğinde hata fırlatıldığını doğrular.
    /// </summary>
    [Fact]
    public void FactoryMethods_ShouldThrow_WhenRequiredValueIsEmpty()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            TeamExecutionEvent.TeamStarted(
                teamId: " ",
                teamName: "Travel Planning Team"));

        Assert.Equal("teamId", exception.ParamName);
    }
}
