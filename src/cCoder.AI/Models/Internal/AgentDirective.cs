namespace cCoder.AI.Models.Internal;

internal class AgentDirective
{
    public string Type { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Tool { get; set; }
    public string? Command { get; set; }
    public string? Reason { get; set; }
}
