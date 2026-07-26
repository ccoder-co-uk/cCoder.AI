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
            ?? throw new InvalidOperationException($"Codex CLI provider '{providerName}' is missing its CLI configuration.");
        string executablePath = ResolveExecutablePath(configuration.ExecutablePath);

        ProcessStartInfo startInfo = new()
        {
            FileName = executablePath,
            WorkingDirectory = ResolveWorkingDirectory(configuration.WorkingDirectory),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        AddArguments(startInfo, configuration, request.Model);
        AddInputFiles(startInfo: startInfo, inputFilePaths: request.InputFilePaths);

        using Process process = new() { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Codex CLI provider '{providerName}' could not start.");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Codex CLI provider '{providerName}' could not start executable '{executablePath}'.",
                exception);
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.StandardInput.WriteAsync(BuildPrompt(request.Messages));
        await process.StandardInput.FlushAsync(cancellationToken);
        process.StandardInput.Close();

        using CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (providerConfiguration.CompletionProvider.TimeoutSeconds > 0)
            timeout.CancelAfter(TimeSpan.FromSeconds(providerConfiguration.CompletionProvider.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        string stdout = (await stdoutTask).Trim();
        string stderr = (await stderrTask).Trim();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Codex CLI provider '{providerName}' exited with code {process.ExitCode}: {Truncate(stderr, 2000)}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
            throw new InvalidOperationException($"Codex CLI provider '{providerName}' returned no final message.");

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
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--ephemeral");
        startInfo.ArgumentList.Add("--skip-git-repo-check");
        startInfo.ArgumentList.Add("--color");
        startInfo.ArgumentList.Add("never");
        startInfo.ArgumentList.Add("--sandbox");
        startInfo.ArgumentList.Add(string.IsNullOrWhiteSpace(configuration.SandboxMode)
            ? "read-only"
            : configuration.SandboxMode);
        if (configuration.IgnoreUserConfiguration)
            startInfo.ArgumentList.Add("--ignore-user-config");
        if (configuration.IgnoreRules)
            startInfo.ArgumentList.Add("--ignore-rules");
        if (!string.IsNullOrWhiteSpace(model))
        {
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(model);
        }
        if (!string.IsNullOrWhiteSpace(configuration.ReasoningEffort))
        {
            startInfo.ArgumentList.Add("--config");
            startInfo.ArgumentList.Add($"model_reasoning_effort=\"{configuration.ReasoningEffort}\"");
        }
        if (configuration.UseOss)
        {
            startInfo.ArgumentList.Add("--oss");
            if (!string.IsNullOrWhiteSpace(configuration.LocalProvider))
            {
                startInfo.ArgumentList.Add("--local-provider");
                startInfo.ArgumentList.Add(configuration.LocalProvider);
            }
        }
        startInfo.ArgumentList.Add("-");
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
        prompt.AppendLine("Act only as a text inference provider and return the assistant response requested by this conversation.");
        prompt.AppendLine("Do not use your own built-in tools or inspect the host environment directly.");
        prompt.AppendLine("If the conversation asks for a shell/tool directive as structured text, returning that directive is allowed: an external orchestrator will validate and execute it.");
        prompt.AppendLine();
        foreach (ChatCompletionMessage message in messages)
        {
            prompt.Append(message.Role.ToUpperInvariant());
            prompt.AppendLine(":");
            prompt.AppendLine(message.Content);
            prompt.AppendLine();
        }
        prompt.AppendLine("ASSISTANT:");
        return prompt.ToString();
    }

    static string ResolveWorkingDirectory(string configuredDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory) && Directory.Exists(configuredDirectory))
            return configuredDirectory;

        string isolatedDirectory = Path.Combine(Path.GetTempPath(), "cCoder.AI", "Codex");
        Directory.CreateDirectory(isolatedDirectory);
        return isolatedDirectory;
    }

    static string ResolveExecutablePath(string configuredPath)
    {
        string requestedPath = configuredPath?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(requestedPath)
            && !requestedPath.Equals("codex", StringComparison.OrdinalIgnoreCase))
        {
            return requestedPath;
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            string desktopRuntime = Path.Combine(userProfile, ".codex", ".sandbox-bin", "codex.exe");
            if (File.Exists(desktopRuntime))
                return desktopRuntime;

            string pluginRuntime = Path.Combine(
                userProfile,
                ".codex",
                "plugins",
                ".plugin-appserver",
                "codex.exe");
            if (File.Exists(pluginRuntime))
                return pluginRuntime;
        }

        return string.IsNullOrWhiteSpace(requestedPath) ? "codex" : requestedPath;
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
        string.IsNullOrWhiteSpace(value) || value.Length <= maxLength
            ? value
            : value[..maxLength];
}
