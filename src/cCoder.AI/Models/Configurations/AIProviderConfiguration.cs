// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Models.Configurations;

public class AIProviderConfiguration
{
    public string Name { get; set; } = string.Empty;
    public int MaxConcurrency { get; set; } = 1;
    public AICompletionProviderConfiguration CompletionProvider { get; set; } = new();
    public AIModelProviderConfiguration ModelProvider { get; set; } = new();
    public CodexCliConfiguration CodexCli { get; set; }
}