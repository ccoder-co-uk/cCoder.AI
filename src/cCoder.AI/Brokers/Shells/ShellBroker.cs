// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.InteropServices;
using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Brokers.Shells;

public class ShellBroker : IShellBroker
{
    public async ValueTask<ToolExecutionResponse> ExecuteAsync(
        string command,
        string? workingDirectory,
        IReadOnlyDictionary<string, string>? environmentVariables,
        ShellKind shellKind,
        CancellationToken cancellationToken = default)
    {
        ShellKind actualShellKind = ResolveShellKind(shellKind: shellKind);
        string effectiveWorkingDirectory = string.IsNullOrWhiteSpace(value: workingDirectory)
            ? Environment.CurrentDirectory
            : Path.GetFullPath(path: workingDirectory);

        ProcessStartInfo processStartInfo = BuildProcessStartInfo(
command: command,
shellKind: actualShellKind,
workingDirectory: effectiveWorkingDirectory,
environmentVariables: environmentVariables);

        using Process process = new() { StartInfo = processStartInfo };

        process.Start();

        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken: cancellationToken);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken: cancellationToken);

        await process.WaitForExitAsync(cancellationToken: cancellationToken);

        string standardOutput = await standardOutputTask;
        string standardError = await standardErrorTask;

        return new ToolExecutionResponse
        {
            Command = command,
            ExitCode = process.ExitCode,
            ShellKind = actualShellKind,
            StandardError = standardError,
            StandardOutput = standardOutput,
            WorkingDirectory = effectiveWorkingDirectory,
        };
    }

    private static ProcessStartInfo BuildProcessStartInfo(
        string command,
        ShellKind shellKind,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environmentVariables)
    {
        ProcessStartInfo processStartInfo = new()
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };

        if (environmentVariables is not null)
        {
            foreach ((string key, string value) in environmentVariables)
            {
                if (string.IsNullOrWhiteSpace(value: key))
                    continue;

                processStartInfo.Environment[key] = value ?? string.Empty;
            }
        }

        switch (shellKind)
        {
            case ShellKind.PowerShell:
                processStartInfo.FileName = "powershell";
                processStartInfo.ArgumentList.Add(item: "-NoProfile");
                processStartInfo.ArgumentList.Add(item: "-ExecutionPolicy");
                processStartInfo.ArgumentList.Add(item: "Bypass");
                processStartInfo.ArgumentList.Add(item: "-Command");
                processStartInfo.ArgumentList.Add(item: command);
                break;

            case ShellKind.Bash:
                processStartInfo.FileName = "bash";
                processStartInfo.ArgumentList.Add(item: "-lc");
                processStartInfo.ArgumentList.Add(item: command);
                break;

            default:
                throw new InvalidOperationException(message: $"Unsupported shell kind: {shellKind}.");
        }

        return processStartInfo;
    }

    private static ShellKind ResolveShellKind(ShellKind shellKind) =>
        shellKind switch
        {
            ShellKind.Auto when RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows) => ShellKind.PowerShell,
            ShellKind.Auto => ShellKind.Bash,
            _ => shellKind,
        };
}