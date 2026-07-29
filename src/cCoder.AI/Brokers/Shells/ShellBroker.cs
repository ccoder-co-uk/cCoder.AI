// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Dependencies;
using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Brokers.Shells;

internal sealed class ShellBroker(
    ShellDependency dependency) :
    IShellBroker
{
    public ValueTask<ToolExecutionResponse> ExecuteAsync(
        string command,
        string? workingDirectory,
        IReadOnlyDictionary<string, string>? environmentVariables,
        ShellKind shellKind,
        CancellationToken cancellationToken = default) =>
        dependency.ExecuteAsync(
            command: command,
            workingDirectory: workingDirectory,
            environmentVariables: environmentVariables,
            shellKind: shellKind,
            cancellationToken: cancellationToken);
}