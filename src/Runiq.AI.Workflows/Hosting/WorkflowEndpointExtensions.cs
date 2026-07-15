using Runiq.AI.Workflows.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Workflows.Models;
using Runiq.AI.Workflows.Domain;
using Runiq.AI.Workflows.Infrastructure;

namespace Runiq.AI.Core.Workflows;

/// <summary>
/// Maps workflow dashboard endpoints for listing, inspecting, and executing registered flows.
/// </summary>
public static class WorkflowEndpointExtensions
{
    /// <summary>
    /// Adds workflow dashboard API endpoints under the supplied path prefix.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder used by the host application.</param>
    /// <param name="pathPrefix">The dashboard API path prefix.</param>
    /// <returns>The same endpoint route builder for fluent endpoint composition.</returns>
    public static IEndpointRouteBuilder MapRuniqWorkflowApi(
        this IEndpointRouteBuilder endpoints,
        string pathPrefix = "/runiq/api")
    {
        var group = endpoints.MapGroup(pathPrefix);

        group.MapGet("/workflows", (IServiceProvider serviceProvider) =>
        {
            var catalog = serviceProvider.GetService<FlowCatalog>();

            return Results.Ok((catalog?.Flows ?? [])
                .Select(MapWorkflow)
                .ToList());
        });

        group.MapGet("/workflows/{workflowId}", (
            string workflowId,
            IServiceProvider serviceProvider) =>
        {
            var workflow = serviceProvider
                .GetService<FlowCatalog>()
                ?.FindById(workflowId);

            return workflow is null
                ? Results.NotFound()
                : Results.Ok(MapWorkflow(workflow));
        });

        group.MapPost("/workflows/{workflowId}/run", async (
            string workflowId,
            WorkflowRunRequestDto request,
            [FromServices] IFlowRunner runtime,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Input))
            {
                return Results.BadRequest(new
                {
                    error = "Workflow input cannot be empty."
                });
            }

            var workflow = serviceProvider
                .GetService<FlowCatalog>()
                ?.FindById(workflowId);

            if (workflow is null)
            {
                return Results.NotFound();
            }

            var result = await runtime.ExecuteAsync(
                workflow,
                request.Input.Trim(),
                cancellationToken);

            return Results.Ok(new WorkflowRunResponseDto(
                WorkflowId: workflow.Id,
                Status: result.Status.ToString(),
                FinalOutput: result.FinalOutput,
                ErrorMessage: result.ErrorMessage,
                Steps: result.StepResults
                    .Select(step => new WorkflowStepRunResultDto(
                        StepId: step.StepId,
                        AgentName: step.AgentType.Name,
                        AgentType: step.AgentType.FullName ?? step.AgentType.Name,
                        Status: step.Status.ToString(),
                        Input: step.Input,
                        Output: step.Output,
                        ErrorMessage: step.ErrorMessage,
                        ToolCalls: step.ToolCalls
                            .Select(toolCall => new WorkflowToolCallRunResultDto(
                                ToolCallId: toolCall.ToolCallId,
                                ToolName: toolCall.ToolName,
                                Status: toolCall.Status.ToString(),
                                ArgumentsJson: toolCall.ArgumentsJson,
                                OutputJson: toolCall.OutputJson,
                                ErrorCode: toolCall.ErrorCode,
                                ErrorMessage: toolCall.ErrorMessage,
                                StartedAt: toolCall.StartedAt,
                                CompletedAt: toolCall.CompletedAt,
                                DurationMs: toolCall.DurationMs))
                            .ToList()))
                    .ToList()));
        });

        return endpoints;
    }

    private static WorkflowMetadataDto MapWorkflow(Flow workflow)
    {
        return new WorkflowMetadataDto(
            Id: workflow.Id,
            Name: workflow.Name,
            StartStepId: workflow.Steps.Count > 0 ? workflow.Steps[0].Id : null,
            StepCount: workflow.Steps.Count,
            Steps: workflow.Steps
                .Select(step => new WorkflowStepMetadataDto(
                    Id: step.Id,
                    AgentType: step.ExecutableType.FullName ?? step.ExecutableType.Name,
                    AgentName: step.ExecutableType.Name,
                    SuccessStepId: step.SuccessStepId,
                    FailureBehavior: step.FailureBehavior.ToString(),
                    FailureStepId: step.FailureStepId))
                .ToList());
    }
}

