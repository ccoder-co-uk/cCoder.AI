namespace cCoder.AI.Models.Responses;

public class AgentStreamTokenResponse
{
    public string Type { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string? Content { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public AgentRunResponse? Completion { get; set; }
}
