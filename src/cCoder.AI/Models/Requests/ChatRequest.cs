// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Enums;

namespace cCoder.AI.Models.Requests;

public sealed class ChatRequest
{
    public string Instructions { get; set; } = string.Empty;

    public string? Provider { get; set; }

    public string? Model { get; set; }

    public string? SystemPrompt { get; set; }

    public IReadOnlyList<string> InputFilePaths { get; set; } =
        Array.Empty<string>();

    public string? WorkingDirectory { get; set; }

    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; set; }

    public ShellKind ShellKind { get; set; } = ShellKind.Auto;

    public int? MaxIterations { get; set; }
}