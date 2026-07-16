using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Services.Foundations.Completions;

public interface ICompletionProviderService
{
    ValueTask<CompletionResponse> CompleteAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CompletionResponse> CompleteChatAsync(
        string? provider,
        string? model,
        IReadOnlyList<ChatCompletionMessage> messages,
        double? temperature = null,
        bool enableShellTooling = false,
        CancellationToken cancellationToken = default);
}
