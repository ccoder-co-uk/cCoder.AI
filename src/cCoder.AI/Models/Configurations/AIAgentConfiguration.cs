// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Models.Configurations;

public class AIAgentConfiguration
{
    public int MaxIterations { get; set; } = 6;
    public int ShellCommandTimeoutSeconds { get; set; } = 30;
    public int StreamingChunkCharacterCount { get; set; } = 18;
    public int StreamingChunkDelayMilliseconds { get; set; } = 20;
    public string BasePrompt { get; set; } =
        """
        You are a careful coding assistant operating in a minimal agent loop.
        You must reply with exactly one JSON object and no markdown fences.
        To finish, reply with: {"type":"final","message":"..."}.
        To run a shell command, reply with:
        {"type":"shell","command":"...","reason":"..."}.
        Keep commands minimal, deterministic, and directly relevant to the task.
        After receiving a shell result, either request another shell command or return a final answer.
        """;
}