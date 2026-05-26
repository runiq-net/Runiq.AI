using Runiq.Core.Teams;
using Runiq.Teams.Models.Execution;

namespace Runiq.Core.Tests.Teams;

/// <summary>
/// Team chat stream mapper davranışlarını doğrular.
/// </summary>
public sealed class TeamChatStreamEventMapperTests
{
    /// <summary>
    /// MemberToolCallStarted event'inin dashboard stream tipine ve tool alanlarına map edildiğini doğrular.
    /// </summary>
    [Fact]
    public void FromExecutionEvent_ShouldMapMemberToolCallStarted()
    {
        var executionEvent = TeamExecutionEvent.MemberToolCallStarted(
            teamId: "travel-team",
            memberAgentId: "weather-agent",
            memberRole: "Weather Analyst",
            toolCallId: "call-1",
            toolName: "weather",
            argumentsJson: "{\"city\":\"Istanbul\"}");

        var streamEvent = TeamChatStreamEventMapper.FromExecutionEvent(executionEvent);

        Assert.Equal("member_tool_call_started", streamEvent.Type);
        Assert.Equal("travel-team", streamEvent.TeamId);
        Assert.Equal("weather-agent", streamEvent.MemberAgentId);
        Assert.Equal("Weather Analyst", streamEvent.MemberRole);
        Assert.Equal("call-1", streamEvent.ToolCallId);
        Assert.Equal("weather", streamEvent.ToolName);
        Assert.Equal("{\"city\":\"Istanbul\"}", streamEvent.ArgumentsJson);
    }

    /// <summary>
    /// MemberDelta event'indeki final üye bilgisinin dashboard stream event'ine taşındığını doğrular.
    /// </summary>
    [Fact]
    public void FromExecutionEvent_ShouldMapIsFinalMemberForMemberDelta()
    {
        var executionEvent = TeamExecutionEvent.MemberDelta(
            teamId: "travel-team",
            memberAgentId: "planner-agent",
            memberRole: "Travel Planner",
            content: "Final plan.",
            isFinalMember: true);

        var streamEvent = TeamChatStreamEventMapper.FromExecutionEvent(executionEvent);

        Assert.Equal("member_delta", streamEvent.Type);
        Assert.Equal("Final plan.", streamEvent.Content);
        Assert.True(streamEvent.IsFinalMember);
    }
}
