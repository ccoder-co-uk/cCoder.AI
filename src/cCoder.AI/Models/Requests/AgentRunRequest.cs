using cCoder.AI.Models.Enums;

namespace cCoder.AI.Models.Requests;

public class AgentRunRequest
{
    public string Instructions { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? SystemPrompt { get; set; }
    public string? WorkingDirectory { get; set; }
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; set; }
    public ShellKind ShellKind { get; set; } = ShellKind.Auto;
    public int? MaxIterations { get; set; }
}
