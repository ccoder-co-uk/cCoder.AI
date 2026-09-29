// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Brokers.Shells;

public interface IShellBroker
{
    ValueTask<ToolExecutionResponse> ExecuteAsync(
        string command,
        string? workingDirectory,
        IReadOnlyDictionary<string, string>? environmentVariables,
        ShellKind shellKind,
        CancellationToken cancellationToken = default);
}