namespace cCoder.AI.Models.Requests;

public class CompletionRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? SystemPrompt { get; set; }
    public double? Temperature { get; set; }
}
