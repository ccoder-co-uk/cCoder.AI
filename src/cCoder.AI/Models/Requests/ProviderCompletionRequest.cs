// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Models.Requests;

public class ProviderCompletionRequest
{
    public string Model { get; set; } = string.Empty;
    public IReadOnlyList<ChatCompletionMessage> Messages { get; set; } = Array.Empty<ChatCompletionMessage>();
    public IReadOnlyList<string> InputFilePaths { get; set; } =
        Array.Empty<string>();
    public double Temperature { get; set; }
    public bool EnableShellTooling { get; set; }
}