// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Models.Configurations;

public sealed class PeerLlmProviderOptions : AIProviderRegistrationOptions
{
    public PeerLlmProviderOptions()
    {
        Endpoint = "https://api.peerllm.com/v1";
        Model = "LLooMA2.0";
    }
}