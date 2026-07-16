namespace AI.Web.Services.Diagnostics;

public interface IAgentRunHistoryService
{
    void Record(AgentRunHistoryEntry entry);
    IReadOnlyList<AgentRunHistoryEntry> RetrieveRecent(int take = 25);
}
