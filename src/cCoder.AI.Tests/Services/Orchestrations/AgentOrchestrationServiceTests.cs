// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Exposures;
using cCoder.AI.Brokers.Shells;
using cCoder.AI.Models.Configurations;
using cCoder.AI.Services.Foundations.Completions;
using cCoder.AI.Services.Orchestrations;
using Moq;

namespace cCoder.AI.Tests.Services.Orchestrations;

public partial class AgentOrchestrationServiceTests
{
    private readonly Mock<ICompletionProviderManager> completionProviderServiceMock;
    private readonly Mock<IShellBroker> shellBrokerMock;
    private readonly AgentOrchestrationService agentOrchestrationService;

    public AgentOrchestrationServiceTests()
    {
        completionProviderServiceMock = new Mock<ICompletionProviderManager>();
        shellBrokerMock = new Mock<IShellBroker>();

        AIConfiguration aiConfiguration = new()
        {
            Agent = new AIAgentConfiguration
            {
                BasePrompt = "BASE PROMPT",
                MaxIterations = 3,
                ShellCommandTimeoutSeconds = 30,
                StreamingChunkCharacterCount = 4,
                StreamingChunkDelayMilliseconds = 0,
            },
        };

        agentOrchestrationService = new AgentOrchestrationService(
completionProviderService: completionProviderServiceMock.Object,
shellBroker: shellBrokerMock.Object,
aiConfiguration: aiConfiguration);
    }
}