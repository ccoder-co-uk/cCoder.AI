// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using AI.Web.Models;

namespace AI.Web.Exposures;

public interface IAgentRunHistoryManager
{
    void Record(AgentRunHistoryEntry entry);
    IReadOnlyList<AgentRunHistoryEntry> RetrieveRecent(int take = 25);
}
