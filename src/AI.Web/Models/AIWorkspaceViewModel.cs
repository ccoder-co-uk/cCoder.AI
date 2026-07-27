// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace AI.Web.Models;

public class AIWorkspaceViewModel
{
    public string DefaultProvider { get; set; } = "Ollama";
    public string DefaultWorkingDirectory { get; set; } = string.Empty;
    public int DefaultMaxIterations { get; set; } = 6;
    public string UseCasePrompt { get; set; } = string.Empty;
    public IReadOnlyList<AIProviderOptionViewModel> Providers { get; set; } = Array.Empty<AIProviderOptionViewModel>();
}