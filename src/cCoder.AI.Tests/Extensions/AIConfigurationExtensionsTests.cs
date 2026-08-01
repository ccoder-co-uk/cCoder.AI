// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Configurations;
using cCoder.AI.Models.Enums;
using FluentAssertions;

namespace cCoder.AI.Tests.Extensions;

public sealed class AIConfigurationExtensionsTests
{
    [Fact]
    public void AddPeerLlm_ShouldRegisterOpenAICompatibleEndpoints()
    {
        // Given
        AIConfiguration configuration = new();

        // When
        configuration.AddPeerLlm(
            key: "PeerLLM",
            configure: options => options.ApiKey = "test-key");

        // Then
        AIProviderConfiguration provider = configuration.Providers["PeerLLM"];
        provider.Name.Should().Be(expected: "PeerLLM");
        provider.CompletionProvider.Mode.Should().Be(expected: AIProviderMode.OpenAICompatible);
        provider.CompletionProvider.Endpoint.Should().Be(expected: "https://api.peerllm.com/v1/chat/completions");
        provider.CompletionProvider.DefaultModel.Should().Be(expected: "LLooMA2.0");
        provider.CompletionProvider.ApiKey.Should().Be(expected: "test-key");
        provider.ModelProvider.Mode.Should().Be(expected: AIModelProviderMode.OpenAICompatible);
        provider.ModelProvider.Endpoint.Should().Be(expected: "https://api.peerllm.com/v1");
        provider.ModelProvider.ApiKey.Should().Be(expected: "test-key");
    }
}