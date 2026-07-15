using cCoder.AI.Models.Enums;

namespace cCoder.AI.Models.Responses;

public class ToolExecutionResponse
{
    public string ToolName { get; set; } = "shell";
    public string Command { get; set; } = string.Empty;
    public ShellKind ShellKind { get; set; }
    public string WorkingDirectory { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
}
