// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Brokers.Completions;
using cCoder.AI.Models.Enums;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Services.Foundations.Completions;
using Moq;

namespace cCoder.AI.Tests.Services.Foundations.Completions;

public partial class CompletionProviderServiceTests
{
    private readonly Mock<IChatCompletionsBroker> chatCompletionsBrokerMock;
    private readonly Mock<ICodexCliBroker> codexCliBrokerMock;
    private readonly CompletionProviderService completionProviderService;

    public CompletionProviderServiceTests()
    {
        chatCompletionsBrokerMock = new Mock<IChatCompletionsBroker>();
        codexCliBrokerMock = new Mock<ICodexCliBroker>();

        AIConfiguration aiConfiguration = new()
        {
            DefaultProvider = "Ollama",
            Providers = new Dictionary<string, AIProviderConfiguration>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ollama"] = new()
                {
                    Name = "Ollama",
                    CompletionProvider = new AICompletionProviderConfiguration
                    {
                        Mode = AIProviderMode.OllamaApi,
                        Endpoint = "http://localhost:11434/api/chat",
                        DefaultModel = "gpt-oss:20b",
                    },
                },
                ["AzureFoundry"] = new()
                {
                    Name = "AzureFoundry",
                    CompletionProvider = new AICompletionProviderConfiguration
                    {
                        Mode = AIProviderMode.AzureFoundry,
                        Endpoint = "https://foundry.test/chat/completions",
                        DefaultModel = "gpt-4.1",
                    },
                },
                ["Codex"] = new()
                {
                    Name = "Codex",
                    CompletionProvider = new AICompletionProviderConfiguration
                    {
                        Mode = AIProviderMode.CodexCli,
                        DefaultModel = "gpt-5.6-luna",
                    },
                    CodexCli = new CodexCliConfiguration
                    {
                        ExecutablePath = "codex"
                    }
                },
            },
        };

        completionProviderService = new CompletionProviderService(
chatCompletionsBroker: chatCompletionsBrokerMock.Object,
codexCliBroker: codexCliBrokerMock.Object,
providerExecutionLimiter: new AIProviderExecutionLimiter(),
aiConfiguration: aiConfiguration);
    }
}
