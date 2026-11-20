// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using AI.Web.Exposures;
using AI.Web.Models;

namespace AI.AcceptanceTests.Infrastructure;

public sealed class TestAgentRunHistoryManager : IAgentRunHistoryManager
{
    private Exception? exception;

    public List<AgentRunHistoryEntry> Entries { get; } = [];

    public void FailWith(Exception expectedException) =>
        exception = expectedException;

    public void Reset()
    {
        Entries.Clear();
        exception = null;
    }

    public void Record(AgentRunHistoryEntry entry)
    {
        if (exception is not null)
        {
            throw exception;
        }

        Entries.Add(item: entry);
    }

    public IReadOnlyList<AgentRunHistoryEntry> RetrieveRecent(int take = 25)
    {
        if (exception is not null)
        {
            throw exception;
        }

        return Entries;
    }
}