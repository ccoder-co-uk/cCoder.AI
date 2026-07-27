// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Diagnostics;
using System.Text;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Requests;
using cCoder.AI.Models.Responses;

namespace cCoder.AI.Brokers.Completions;

public sealed class CodexCliBroker : ICodexCliBroker
{
    public async ValueTask<CompletionResponse> CompleteAsync(
        string providerName,
        AIProviderConfiguration providerConfiguration,
        ProviderCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        CodexCliConfiguration configuration = providerConfiguration.CodexCli
            ?? throw new InvalidOperationException(message: $"Codex CLI provider '{providerName}' is missing its CLI configuration.");
        string executablePath = ResolveExecutablePath(configuredPath: configuration.ExecutablePath);

        ProcessStartInfo startInfo = new()
        {
            FileName = executablePath,
            WorkingDirectory = ResolveWorkingDirectory(configuredDirectory: configuration.WorkingDirectory),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        AddArguments(startInfo: startInfo, configuration: configuration, model: request.Model);
        AddInputFiles(startInfo: startInfo, inputFilePaths: request.InputFilePaths);

        using Process process = new() { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                throw new InvalidOperationException(message: $"Codex CLI provider '{providerName}' could not start.");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
message: $"Codex CLI provider '{providerName}' could not start executable '{executablePath}'.",
innerException: exception);
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken: cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken: cancellationToken);
        await process.StandardInput.WriteAsync(value: BuildPrompt(request.Messages));
        await process.StandardInput.FlushAsync(cancellationToken: cancellationToken);
        process.StandardInput.Close();

        using CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(token: cancellationToken);
        if (providerConfiguration.CompletionProvider.TimeoutSeconds > 0)
            timeout.CancelAfter(delay: TimeSpan.FromSeconds(providerConfiguration.CompletionProvider.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(cancellationToken: timeout.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process: process);
            throw;
        }

        string stdout = (await stdoutTask).Trim();
        string stderr = (await stderrTask).Trim();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
message: $"Codex CLI provider '{providerName}' exited with code {process.ExitCode}: {Truncate(stderr, 2000)}");
        }

        if (string.IsNullOrWhiteSpace(value: stdout))
            throw new InvalidOperationException(message: $"Codex CLI provider '{providerName}' returned no final message.");

        return new CompletionResponse
        {
            Provider = providerName,
            Model = request.Model,
            Content = stdout,
            RawContent = stdout
        };
    }

    static void AddArguments(
        ProcessStartInfo startInfo,
        CodexCliConfiguration configuration,
        string model)
    {
        startInfo.ArgumentList.Add(item: "exec");
        startInfo.ArgumentList.Add(item: "--ephemeral");
        startInfo.ArgumentList.Add(item: "--skip-git-repo-check");
        startInfo.ArgumentList.Add(item: "--color");
        startInfo.ArgumentList.Add(item: "never");
        startInfo.ArgumentList.Add(item: "--sandbox");
        startInfo.ArgumentList.Add(item: string.IsNullOrWhiteSpace(configuration.SandboxMode)
            ? "read-only"
            : configuration.SandboxMode);
        if (configuration.IgnoreUserConfiguration)
            startInfo.ArgumentList.Add(item: "--ignore-user-config");
        if (configuration.IgnoreRules)
            startInfo.ArgumentList.Add(item: "--ignore-rules");
        if (!string.IsNullOrWhiteSpace(value: model))
        {
            startInfo.ArgumentList.Add(item: "--model");
            startInfo.ArgumentList.Add(item: model);
        }
        if (!string.IsNullOrWhiteSpace(value: configuration.ReasoningEffort))
        {
            startInfo.ArgumentList.Add(item: "--config");
            startInfo.ArgumentList.Add(item: $"model_reasoning_effort=\"{configuration.ReasoningEffort}\"");
        }
        if (configuration.UseOss)
        {
            startInfo.ArgumentList.Add(item: "--oss");
            if (!string.IsNullOrWhiteSpace(value: configuration.LocalProvider))
            {
                startInfo.ArgumentList.Add(item: "--local-provider");
                startInfo.ArgumentList.Add(item: configuration.LocalProvider);
            }
        }
        startInfo.ArgumentList.Add(item: "-");
    }

    static void AddInputFiles(
        ProcessStartInfo startInfo,
        IReadOnlyList<string> inputFilePaths)
    {
        foreach (string inputFilePath in inputFilePaths ?? [])
        {
            string fullPath = Path.GetFullPath(path: inputFilePath);

            if (!File.Exists(path: fullPath))
            {
                throw new FileNotFoundException(
                    message: "An AI input file could not be found.",
                    fileName: fullPath);
            }

            startInfo.ArgumentList.Insert(
                index: startInfo.ArgumentList.Count - 1,
                item: "--image");

            startInfo.ArgumentList.Insert(
                index: startInfo.ArgumentList.Count - 1,
                item: fullPath);
        }
    }

    static string BuildPrompt(IReadOnlyList<ChatCompletionMessage> messages)
    {
        StringBuilder prompt = new();
        prompt.AppendLine(value: "Act only as a text inference provider and return the assistant response requested by this conversation.");
        prompt.AppendLine(value: "Do not use your own built-in tools or inspect the host environment directly.");
        prompt.AppendLine(value: "If the conversation asks for a shell/tool directive as structured text, returning that directive is allowed: an external orchestrator will validate and execute it.");
        prompt.AppendLine();
        foreach (ChatCompletionMessage message in messages)
        {
            prompt.Append(value: message.Role.ToUpperInvariant());
            prompt.AppendLine(value: ":");
            prompt.AppendLine(value: message.Content);
            prompt.AppendLine();
        }
        prompt.AppendLine(value: "ASSISTANT:");
        return prompt.ToString();
    }

    static string ResolveWorkingDirectory(string configuredDirectory)
    {
        if (!string.IsNullOrWhiteSpace(value: configuredDirectory) && Directory.Exists(path: configuredDirectory))
            return configuredDirectory;

        string isolatedDirectory = Path.Combine(path1: Path.GetTempPath(), path2: "cCoder.AI", path3: "Codex");
        Directory.CreateDirectory(path: isolatedDirectory);
        return isolatedDirectory;
    }

    static string ResolveExecutablePath(string configuredPath)
    {
        string requestedPath = configuredPath?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(value: requestedPath)
            && !requestedPath.Equals(value: "codex", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return requestedPath;
        }

        string userProfile = Environment.GetFolderPath(folder: Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(value: userProfile))
        {
            string desktopRuntime = Path.Combine(path1: userProfile, path2: ".codex", path3: ".sandbox-bin", path4: "codex.exe");
            if (File.Exists(path: desktopRuntime))
                return desktopRuntime;

            string pluginRuntime = Path.Combine(
                userProfile,
                ".codex",
                "plugins",
                ".plugin-appserver",
                "codex.exe");
            if (File.Exists(path: pluginRuntime))
                return pluginRuntime;
        }

        return string.IsNullOrWhiteSpace(value: requestedPath) ? "codex" : requestedPath;
    }

    static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    static string Truncate(string value, int maxLength) =>
        string.IsNullOrWhiteSpace(value: value) || value.Length <= maxLength
            ? value
            : value[..maxLength];
}