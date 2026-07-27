// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Requests;

namespace cCoder.AI.Models.Responses;

public class AgentIterationResponse
{
    public int IterationNumber { get; set; }
    public AgentResultType ResultType { get; set; }
    public IReadOnlyList<ChatCompletionMessage> RequestMessages { get; set; } = Array.Empty<ChatCompletionMessage>();
    public string CompletionContent { get; set; } = string.Empty;
    public string ParseError { get; set; } = string.Empty;
    public string? ToolName { get; set; }
    public ToolExecutionResponse? ToolExecution { get; set; }
}