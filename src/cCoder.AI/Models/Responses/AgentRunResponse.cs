namespace cCoder.AI.Models.Responses;

public class AgentRunResponse
{
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public int Iterations { get; set; }
    public string FinalMessage { get; set; } = string.Empty;
    public IReadOnlyList<AgentIterationResponse> IterationResponses { get; set; } = Array.Empty<AgentIterationResponse>();
}
