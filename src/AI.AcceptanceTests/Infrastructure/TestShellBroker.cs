// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Collections.Concurrent;
using cCoder.AI.Brokers.Shells;
using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Responses;

namespace AI.AcceptanceTests.Infrastructure;

public sealed class TestShellBroker : IShellBroker
{
    private readonly ConcurrentQueue<ToolExecutionResponse> toolExecutionResponses = new();

    public List<(string Command, string? WorkingDirectory, IReadOnlyDictionary<string, string>? EnvironmentVariables, ShellKind ShellKind)> Executions { get; } = [];

    public void Reset()
    {
        Executions.Clear();

        while (toolExecutionResponses.TryDequeue(result: out _))
        {
        }
    }

    public void EnqueueResponse(ToolExecutionResponse toolExecutionResponse) =>
        toolExecutionResponses.Enqueue(item: toolExecutionResponse);

    public ValueTask<ToolExecutionResponse> ExecuteAsync(
        string command,
        string? workingDirectory,
        IReadOnlyDictionary<string, string>? environmentVariables,
        ShellKind shellKind,
        CancellationToken cancellationToken = default)
    {
        Executions.Add(item: (command, workingDirectory, environmentVariables, shellKind));

        if (toolExecutionResponses.TryDequeue(result: out ToolExecutionResponse? toolExecutionResponse))
        {
            return ValueTask.FromResult(result: toolExecutionResponse);
        }

        return ValueTask.FromResult(result: new ToolExecutionResponse
        {
            Command = command,
            ExitCode = 0,
            ShellKind = shellKind,
            StandardError = string.Empty,
            StandardOutput = string.Empty,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        });
    }
}