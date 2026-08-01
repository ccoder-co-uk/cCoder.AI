// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Models.Configurations;

public sealed class FoundryAIProviderOptions : AIProviderRegistrationOptions
{
    public string ApiKeyHeaderName { get; set; } = "api-key";
    public string ApiKeyScheme { get; set; } = string.Empty;
}