// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Models.Configurations;

public sealed class CodexCliConfiguration
{
    public string ExecutablePath { get; set; } = "codex";
    public string WorkingDirectory { get; set; } = string.Empty;
    public string SandboxMode { get; set; } = "read-only";
    public string ReasoningEffort { get; set; } = "low";
    public bool IgnoreUserConfiguration { get; set; } = true;
    public bool IgnoreRules { get; set; } = true;
    public bool UseOss { get; set; }
    public string LocalProvider { get; set; } = string.Empty;
}