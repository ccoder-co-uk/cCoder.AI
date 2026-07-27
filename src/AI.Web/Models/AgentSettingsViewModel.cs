// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace AI.Web.Models;

public class AgentSettingsViewModel
{
    public int MaxIterations { get; set; }
    public int ShellCommandTimeoutSeconds { get; set; }
    public int StreamingChunkCharacterCount { get; set; }
    public int StreamingChunkDelayMilliseconds { get; set; }
}