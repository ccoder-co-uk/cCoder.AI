// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using cCoder.AI.Exposures;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace AI.AcceptanceTests.Infrastructure;

public sealed class TestAgentManager : IAgentManager
{
    private Exception? exception;

    public void FailWith(Exception expectedException) =>
        exception = expectedException;

    public ValueTask<AgentRunResponse> RunAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken = default)
    {
        if (exception is not null)
        {
            throw exception;
        }

        return ValueTask.FromResult(result: new AgentRunResponse
        {
            FinalMessage = "Done.",
            Succeeded = true,
        });
    }

    public async IAsyncEnumerable<AgentStreamTokenResponse> StreamAsync(
        AgentRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (exception is not null)
        {
            throw exception;
        }

        yield return new AgentStreamTokenResponse
        {
            Type = "complete",
            Content = "Done.",
        };

        await Task.CompletedTask;
    }
}