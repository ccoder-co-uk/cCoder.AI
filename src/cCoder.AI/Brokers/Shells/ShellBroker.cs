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
        ShellKind actualShellKind = ResolveShellKind(shellKind);
        string effectiveWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Environment.CurrentDirectory
            : Path.GetFullPath(workingDirectory);

        ProcessStartInfo processStartInfo = BuildProcessStartInfo(
            command,
            actualShellKind,
            effectiveWorkingDirectory,
            environmentVariables);

        using Process process = new() { StartInfo = processStartInfo };

        process.Start();

        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

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
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                processStartInfo.Environment[key] = value ?? string.Empty;
            }
        }

        switch (shellKind)
        {
            case ShellKind.PowerShell:
                processStartInfo.FileName = "powershell";
                processStartInfo.ArgumentList.Add("-NoProfile");
                processStartInfo.ArgumentList.Add("-ExecutionPolicy");
                processStartInfo.ArgumentList.Add("Bypass");
                processStartInfo.ArgumentList.Add("-Command");
                processStartInfo.ArgumentList.Add(command);
                break;

            case ShellKind.Bash:
                processStartInfo.FileName = "bash";
                processStartInfo.ArgumentList.Add("-lc");
                processStartInfo.ArgumentList.Add(command);
                break;

            default:
                throw new InvalidOperationException($"Unsupported shell kind: {shellKind}.");
        }

        return processStartInfo;
    }

    private static ShellKind ResolveShellKind(ShellKind shellKind) =>
        shellKind switch
        {
            ShellKind.Auto when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => ShellKind.PowerShell,
            ShellKind.Auto => ShellKind.Bash,
            _ => shellKind,
        };
}
