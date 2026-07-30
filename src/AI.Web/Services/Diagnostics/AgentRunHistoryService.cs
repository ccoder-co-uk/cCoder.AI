// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Collections.Concurrent;
using AI.Web.Models;

namespace AI.Web.Services.Diagnostics;

internal class AgentRunHistoryService : IAgentRunHistoryService
{
    const int MaxEntries = 100;
    readonly ConcurrentQueue<AgentRunHistoryEntry> entries = new();

    public void Record(AgentRunHistoryEntry entry)
    {
        entries.Enqueue(item: entry);

        while (entries.Count > MaxEntries && entries.TryDequeue(result: out _))
        {
        }
    }

    public IReadOnlyList<AgentRunHistoryEntry> RetrieveRecent(int take = 25) =>
        entries
            .Reverse()
            .Take(count: Math.Max(1, take))
            .ToList();
}