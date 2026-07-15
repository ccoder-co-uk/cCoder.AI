namespace cCoder.AI.Models.Configurations;

public class AIConfiguration
{
    public const string SectionName = "AI";

    public string DefaultProvider { get; set; } = "Ollama";
    public AIAgentConfiguration Agent { get; set; } = new();
    public Dictionary<string, AIProviderConfiguration> Providers { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Ollama"] = new AIProviderConfiguration
            {
                Name = "Ollama",
                CompletionProvider = new AICompletionProviderConfiguration
                {
                    Mode = Models.Enums.AIProviderMode.OllamaApi,
                    Endpoint = "http://localhost:11434/api/chat",
                },
                ModelProvider = new AIModelProviderConfiguration(),
            },
            ["AzureFoundry"] = new AIProviderConfiguration
            {
                Name = "AzureFoundry",
                CompletionProvider = new AICompletionProviderConfiguration(),
                ModelProvider = new AIModelProviderConfiguration
                {
                    Mode = Models.Enums.AIModelProviderMode.AzureFoundryDeployments,
                },
            },
        };
}
