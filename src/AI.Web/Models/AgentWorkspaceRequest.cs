namespace AI.Web.Models;

public class AgentWorkspaceRequest
{
    public string Instructions { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? WorkingDirectory { get; set; }
    public int? MaxIterations { get; set; }
}
