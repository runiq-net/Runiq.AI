using System.Runtime.CompilerServices;
using System.Text;
using Runiq.Agents;
using Runiq.Agents.Runtime;
using Runiq.Agents.Tools;
using Runiq.Teams.Execution.Planning;
using Runiq.Teams.Models.Execution;
using Runiq.Teams.Models.Teams;

namespace Runiq.Teams.Execution;

/// <summary>
/// Kayıtlı agent team tanımlarını sıralı multi-agent yürütme modeliyle çalıştıran runtime servisidir.
/// </summary>
public sealed class TeamExecutionRuntime
{
    private readonly IReadOnlyList<AgentTeam> teams;
    private readonly AgentExecutionRuntime agentRuntime;
    private readonly AgentToolInvoker toolInvoker;
    private readonly ITeamExecutionPlannerResolver plannerResolver;

    /// <summary>
    /// Yeni bir team execution runtime örneği oluşturur.
    /// </summary>
    /// <param name="teams">Runtime tarafından çalıştırılabilecek kayıtlı agent team koleksiyonudur.</param>
    /// <param name="agentRuntime">Takım üyelerini çalıştırmak için kullanılacak agent runtime örneğidir.</param>
    /// <param name="toolInvoker">Agent tool çağrılarını çalıştıran invoker örneğidir.</param>
    public TeamExecutionRuntime(
        IReadOnlyList<AgentTeam>? teams,
        AgentExecutionRuntime agentRuntime,
        AgentToolInvoker toolInvoker,
        ITeamExecutionPlannerResolver plannerResolver)
    {
        this.teams = teams ?? [];
        this.agentRuntime = agentRuntime;
        this.toolInvoker = toolInvoker;
        this.plannerResolver = plannerResolver;
    }

    /// <summary>
    /// Agent team cevabını team kimliğine göre event stream olarak üretir.
    /// </summary>
    /// <param name="teamId">Çalıştırılacak agent team kimliğidir.</param>
    /// <param name="input">Takıma gönderilecek kullanıcı girdisidir.</param>
    /// <param name="cancellationToken">İptal bildirimidir.</param>
    /// <returns>Team çalışması sırasında üretilen olay stream'idir.</returns>
    public async IAsyncEnumerable<TeamExecutionEvent> ExecuteStreamAsync(
        string teamId,
        string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            yield return TeamExecutionEvent.TeamFailed(
                teamId,
                "Team input cannot be empty.",
                "InputRequired");

            yield break;
        }

        var team = FindTeam(teamId);

        if (team is null)
        {
            yield return TeamExecutionEvent.TeamFailed(
                teamId,
                $"Agent team '{teamId}' was not found.",
                "TeamNotFound");

            yield break;
        }

        if (team.Members.Count == 0)
        {
            yield return TeamExecutionEvent.TeamFailed(
                team.Id,
                $"Agent team '{team.Id}' does not have any members.",
                "TeamHasNoMembers");

            yield break;
        }

        yield return TeamExecutionEvent.TeamStarted(
            team.Id,
            team.Name);

        var previousOutputs = new List<(TeamMember Member, string Output)>();
        string? finalContent = null;

        TeamExecutionPlan? plan = null;
        string? planningFailureMessage = null;

        try
        {
            plan = await plannerResolver
                .Resolve(team)
                .CreatePlanAsync(team, input, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            planningFailureMessage = exception.Message;
        }

        if (planningFailureMessage is not null || plan is null)
        {
            yield return TeamExecutionEvent.TeamFailed(
                team.Id,
                $"Agent team planning failed: {planningFailureMessage ?? "No plan was created."}",
                "TeamPlanningFailed");

            yield break;
        }

        foreach (var planStep in plan.Steps)
        {
            var member = team.Members.FirstOrDefault(candidate =>
                string.Equals(candidate.AgentId, planStep.AgentId, StringComparison.OrdinalIgnoreCase));

            if (member is null)
            {
                yield return TeamExecutionEvent.TeamFailed(
                    team.Id,
                    $"Agent team plan selected unknown member '{planStep.AgentId}'.",
                    "TeamPlanMemberNotFound");

                yield break;
            }

            var isFinalMember = planStep.IsFinalMember;

            cancellationToken.ThrowIfCancellationRequested();

            var currentInput = BuildMemberInput(
                team,
                originalInput: input,
                currentMember: member,
                isFinalMember: isFinalMember,
                planStepReason: planStep.Reason,
                previousOutputs: previousOutputs);

            yield return TeamExecutionEvent.MemberStarted(
                team.Id,
                member.AgentId,
                member.Role);

            var memberContentBuilder = new StringBuilder();

            var agentEvents = agentRuntime.ExecuteStreamAsync(
                member.AgentId,
                currentInput,
                toolInvoker,
                cancellationToken);

            await using var agentEventEnumerator = agentEvents.GetAsyncEnumerator(cancellationToken);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                AgentExecutionEvent agentEvent;
                string? exceptionMessage = null;

                try
                {
                    if (!await agentEventEnumerator.MoveNextAsync())
                    {
                        break;
                    }

                    agentEvent = agentEventEnumerator.Current;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    exceptionMessage = exception.Message;
                    agentEvent = null!;
                }

                if (exceptionMessage is not null)
                {
                    yield return TeamExecutionEvent.MemberFailed(
                        team.Id,
                        member.AgentId,
                        member.Role,
                        exceptionMessage,
                        "MemberExecutionException");

                    yield return TeamExecutionEvent.TeamFailed(
                        team.Id,
                        $"Agent team member '{member.AgentId}' failed: {exceptionMessage}",
                        "TeamMemberFailed");

                    yield break;
                }

                if (agentEvent.Kind == AgentExecutionEventKind.AssistantDelta)
                {
                    var delta = agentEvent.Content;

                    if (delta is null || delta.Length == 0)
                    {
                        continue;
                    }

                    memberContentBuilder.Append(delta);

                    yield return TeamExecutionEvent.MemberDelta(
                        team.Id,
                        member.AgentId,
                        member.Role,
                        delta,
                        isFinalMember);
                    continue;
                }

                if (agentEvent.Kind == AgentExecutionEventKind.ToolCallStarted)
                {
                    yield return TeamExecutionEvent.MemberToolCallStarted(
                        team.Id,
                        member.AgentId,
                        member.Role,
                        agentEvent.ToolCallId ?? $"{member.AgentId}:tool-call",
                        agentEvent.ToolName ?? "unknown",
                        agentEvent.ArgumentsJson);
                    continue;
                }

                if (agentEvent.Kind == AgentExecutionEventKind.ToolCallCompleted)
                {
                    yield return TeamExecutionEvent.MemberToolCallCompleted(
                        team.Id,
                        member.AgentId,
                        member.Role,
                        agentEvent.ToolCallId ?? $"{member.AgentId}:tool-call",
                        agentEvent.ToolName ?? "unknown",
                        agentEvent.OutputJson);

                    continue;
                }

                if (agentEvent.Kind == AgentExecutionEventKind.ToolCallFailed)
                {
                    yield return TeamExecutionEvent.MemberToolCallFailed(
                        team.Id,
                        member.AgentId,
                        member.Role,
                        agentEvent.ToolCallId ?? $"{member.AgentId}:tool-call",
                        agentEvent.ToolName ?? "unknown",
                        agentEvent.ErrorMessage ?? "Agent team member tool call failed.",
                        agentEvent.ErrorCode);

                    continue;
                }

                if (agentEvent.Kind == AgentExecutionEventKind.Failed)
                {
                    var memberErrorMessage =
                        agentEvent.ErrorMessage ?? "Agent team member execution failed.";
                    var memberErrorCode =
                        agentEvent.ErrorCode ?? "MemberExecutionFailed";

                    yield return TeamExecutionEvent.MemberFailed(
                        team.Id,
                        member.AgentId,
                        member.Role,
                        memberErrorMessage,
                        memberErrorCode);

                    yield return TeamExecutionEvent.TeamFailed(
                        team.Id,
                        $"Agent team member '{member.AgentId}' failed: {memberErrorMessage}",
                        "TeamMemberFailed");

                    yield break;
                }
            }

            var memberContent = memberContentBuilder.ToString();

            if (string.IsNullOrWhiteSpace(memberContent))
            {
                yield return TeamExecutionEvent.MemberFailed(
                    team.Id,
                    member.AgentId,
                    member.Role,
                    $"Agent team member '{member.AgentId}' completed without producing output.",
                    "MemberEmptyOutput");

                yield return TeamExecutionEvent.TeamFailed(
                    team.Id,
                    $"Agent team member '{member.AgentId}' completed without producing output.",
                    "TeamMemberEmptyOutput");

                yield break;
            }

            finalContent = memberContent.Trim();

            yield return TeamExecutionEvent.MemberCompleted(
                team.Id,
                member.AgentId,
                member.Role,
                finalContent);

            previousOutputs.Add((member, finalContent));
        }

        yield return TeamExecutionEvent.TeamCompleted(
            team.Id,
            finalContent);
    }

    private AgentTeam? FindTeam(string teamId)
    {
        return teams.FirstOrDefault(team =>
            string.Equals(team.Id, teamId, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildMemberInput(
        AgentTeam team,
        string originalInput,
        TeamMember currentMember,
        bool isFinalMember,
        string planStepReason,
        IReadOnlyList<(TeamMember Member, string Output)> previousOutputs)
    {
        var previousOutputText = BuildPreviousOutputText(previousOutputs);
        var memberInstructions = string.IsNullOrWhiteSpace(currentMember.Instructions)
            ? "No member-specific instructions."
            : currentMember.Instructions;
        var finalMemberInstruction = isFinalMember
            ? "You are the final team member. Synthesize previous outputs into the final user-facing response."
            : "You are not the final team member. Do not summarize the whole plan; only provide your specialist findings for the next member.";

        return $"""
        You are working as part of the agent team '{team.Name}'.

        Current member:
        Role: {currentMember.Role}
        Agent id: {currentMember.AgentId}
        Is final team member: {isFinalMember}

        Team instructions:
        {team.Instructions}

        Member-specific instructions:
        {memberInstructions}

        Planning reason for this member:
        {planStepReason}

        Original user request:
        {originalInput}

        Previous member outputs:
        {previousOutputText}

        Role discipline:
        - Produce only the contribution for your assigned role.
        - Do not produce the final itinerary unless your role is Travel Planner or you are the final team member.
        - Keep the output focused and concise.
        - After using any tool, return a natural-language assistant response for your assigned role.
        - Do not print raw tool JSON or labels such as "Tool:" or "Output:".
        - If you are not the final member, do not summarize the whole plan; only provide your specialist findings for the next member.
        - If you are the final member, synthesize previous outputs into the final user-facing response.

        Current execution instruction:
        {finalMemberInstruction}
        """;
    }

    private static string BuildPreviousOutputText(
        IReadOnlyList<(TeamMember Member, string Output)> previousOutputs)
    {
        if (previousOutputs.Count == 0)
        {
            return "No previous member output yet.";
        }

        var builder = new StringBuilder();

        foreach (var previousOutput in previousOutputs)
        {
            builder.AppendLine($"Role: {previousOutput.Member.Role}");
            builder.AppendLine($"Agent id: {previousOutput.Member.AgentId}");
            builder.AppendLine("Output:");
            builder.AppendLine(previousOutput.Output);
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }
}
