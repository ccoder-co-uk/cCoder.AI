// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Models.Configurations;

public sealed class OpenAIProviderOptions : AIProviderRegistrationOptions
{
    public OpenAIProviderOptions()
    {
        Endpoint = "https://api.openai.com/v1";
    }
}