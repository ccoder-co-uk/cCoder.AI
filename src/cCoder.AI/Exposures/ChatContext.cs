// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;
using cCoder.AI.Services.Orchestrations;

namespace cCoder.AI.Exposures;

public sealed class ChatContext(
    IAgentManager agentOrchestrationService)
{
    public ValueTask<AgentRunResponse> InferAsync(
        ChatRequest chatRequest,
        CancellationToken cancellationToken = default) =>
        agentOrchestrationService.RunAsync(
            request: MapToAgentRunRequest(chatRequest: chatRequest),
            cancellationToken: cancellationToken);

    public IAsyncEnumerable<AgentStreamTokenResponse> InferAsStreamAsync(
        ChatRequest chatRequest,
        CancellationToken cancellationToken = default) =>
        agentOrchestrationService.StreamAsync(
            request: MapToAgentRunRequest(chatRequest: chatRequest),
            cancellationToken: cancellationToken);

    private static AgentRunRequest MapToAgentRunRequest(
        ChatRequest chatRequest) =>
        new()
        {
            Instructions = chatRequest.Instructions,
            Provider = chatRequest.Provider,
            Model = chatRequest.Model,
            SystemPrompt = chatRequest.SystemPrompt,
            InputFilePaths = chatRequest.InputFilePaths,
            WorkingDirectory = chatRequest.WorkingDirectory,
            EnvironmentVariables = chatRequest.EnvironmentVariables,
            ShellKind = chatRequest.ShellKind,
            MaxIterations = chatRequest.MaxIterations,
        };
}