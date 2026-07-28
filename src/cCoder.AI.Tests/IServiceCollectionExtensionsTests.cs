// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Brokers.Completions;
using cCoder.AI.Brokers.ModelProviders;
using cCoder.AI.Brokers.Shells;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Services.Foundations.Completions;
using cCoder.AI.Services.Foundations.Models;
using cCoder.AI.Services.Orchestrations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace cCoder.AI.Tests;

public class IServiceCollectionExtensionsTests
{
    [Fact]
    public void ShouldAddAIServices()
    {
        // Given
        IServiceCollection services = new ServiceCollection();

        // When
        services.AddAIWeb(configure: configuration =>
        {
            configuration.DefaultProvider = "local";
            configuration.AddOllama("local", ollama =>
            {
                ollama.Endpoint = "http://localhost:11434";
                ollama.Model = "gpt-oss:20b";
            });
            configuration.AddOpenAI("open ai", openAI =>
            {
                openAI.ApiKey = "secret";
                openAI.Model = "gpt-test";
                openAI.MaxConcurrency = 4;
            });
            configuration.AddFoundry("foundry", foundry =>
            {
                foundry.Endpoint = "https://foundry.test/models/chat/completions";
                foundry.ApiKey = "foundry-secret";
                foundry.Model = "foundry-model";
            });
            configuration.AddCodex("codex", codex =>
            {
                codex.ExecutablePath = "codex-test";
                codex.Model = "gpt-test";
                codex.MaxConcurrency = 2;
            });
        });

        IServiceProvider serviceProvider = services.BuildServiceProvider();

        // Then
        serviceProvider.GetService<AIConfiguration>().Should().NotBeNull();
        serviceProvider.GetService<IChatCompletionsBroker>().Should().NotBeNull();
        serviceProvider.GetService<ICodexCliBroker>().Should().NotBeNull();
        serviceProvider.GetService<IModelProviderBroker>().Should().NotBeNull();
        serviceProvider.GetService<IShellBroker>().Should().NotBeNull();
        serviceProvider.GetService<ICompletionProviderService>().Should().NotBeNull();
        serviceProvider.GetService<IModelManagerService>().Should().NotBeNull();
        serviceProvider.GetService<IAgentOrchestrationService>().Should().NotBeNull();
        serviceProvider.GetService<IAIProviderExecutionLimiter>().Should().NotBeNull();

        AIConfiguration configuration = serviceProvider.GetRequiredService<AIConfiguration>();
        configuration.Providers["local"].CompletionProvider.Endpoint
            .Should().Be(expected: "http://localhost:11434/api/chat");
        configuration.Providers["open ai"].CompletionProvider.Endpoint
            .Should().Be(expected: "https://api.openai.com/v1/chat/completions");
        configuration.Providers["open ai"].CompletionProvider.ApiKeyHeaderName
            .Should().Be(expected: "Authorization");
        configuration.Providers["open ai"].MaxConcurrency.Should().Be(expected: 4);
        configuration.Providers["foundry"].CompletionProvider.ApiKeyHeaderName
            .Should().Be(expected: "api-key");
        configuration.Providers["foundry"].CompletionProvider.Endpoint
            .Should().Be(expected: "https://foundry.test/models/chat/completions");
        configuration.Providers["codex"].CompletionProvider.Mode
            .Should().Be(expected: Models.Enums.AIProviderMode.CodexCli);
        configuration.Providers["codex"].CodexCli.ExecutablePath.Should().Be(expected: "codex-test");

        IModelManagerService models = serviceProvider.GetRequiredService<IModelManagerService>();
        models.GetProviderCapabilities(provider: "local").Should().BeEquivalentTo(expectation: new
        {
            Provider = "local",
            DefaultModel = "gpt-oss:20b",
            MaxConcurrency = 1,
            SupportsModelListing = true,
            SupportsModelImport = true
        });
        models.GetProviderCapabilities(provider: "codex").SupportsModelListing.Should().BeFalse();
    }
}