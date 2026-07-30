// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Exposures;

public interface IAgentManager
{
    ValueTask<AgentRunResponse> RunAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AgentStreamTokenResponse> StreamAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken = default);
}