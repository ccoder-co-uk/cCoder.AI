namespace cCoder.AI.Models.Requests;

public class ProviderCompletionRequest
{
    public string Model { get; set; } = string.Empty;
    public IReadOnlyList<ChatCompletionMessage> Messages { get; set; } = Array.Empty<ChatCompletionMessage>();
    public double Temperature { get; set; }
    public bool EnableShellTooling { get; set; }
}
