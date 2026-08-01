// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Models.Configurations;

public sealed class OllamaAIProviderOptions : AIProviderRegistrationOptions
{
    public OllamaAIProviderOptions()
    {
        Endpoint = "http://localhost:11434";
    }
}