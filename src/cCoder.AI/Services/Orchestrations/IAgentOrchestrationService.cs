using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Services.Orchestrations;

public interface IAgentOrchestrationService
{
    ValueTask<AgentRunResponse> RunAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AgentStreamTokenResponse> StreamAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken = default);
}
