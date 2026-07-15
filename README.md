# cCoder.AI

`cCoder.AI` is a named-provider inference broker. Applications register every AI endpoint once, then select a provider by its key on each completion or agent request.

```csharp
builder.Services.AddAI(ai =>
{
    ai.DefaultProvider = "local";

    ai.AddOllama("local", provider =>
    {
        provider.Endpoint = "http://localhost:11434";
        provider.Model = "qwen3.5:4b";
        provider.MaxConcurrency = 1;
    });

    ai.AddOllama("desktop", provider =>
    {
        provider.Endpoint = "http://desktop:11434";
        provider.Model = "gpt-oss:20b";
        provider.MaxConcurrency = 1;
    });

    ai.AddOpenAI("open-ai", provider =>
    {
        provider.ApiKey = configuration["OPENAI_API_KEY"];
        provider.Model = "gpt-5.6-luna";
        provider.MaxConcurrency = 8;
    });

    ai.AddFoundry("foundry", provider =>
    {
        provider.Endpoint = configuration["FOUNDRY_COMPLETION_ENDPOINT"];
        provider.ModelEndpoint = configuration["FOUNDRY_MODEL_ENDPOINT"];
        provider.ApiKey = configuration["FOUNDRY_API_KEY"];
        provider.Model = configuration["FOUNDRY_MODEL"];
        provider.MaxConcurrency = 8;
    });

    ai.AddCodex("codex", provider =>
    {
        // Uses the Codex CLI's existing ChatGPT or API-key login.
        provider.Model = "gpt-5.6-luna";
        provider.ReasoningEffort = "low";
        provider.MaxConcurrency = 1;
    });
});
```

The registration key (`local`, `desktop`, `open-ai`, or `foundry` above) is the provider value supplied to `ICompletionProviderService` and `IAgentOrchestrationService` requests.

Provider concurrency is enforced inside `cCoder.AI`, independently for each key. Transient HTTP 408, 429, and 5xx responses are retried using provider retry settings and `Retry-After` headers when supplied. Application-level schedulers may impose lower workload concurrency, but cannot exceed the provider cap during inference.

The Codex provider runs non-interactive `codex exec` requests in ephemeral, read-only mode. Set `ExecutablePath` in the Codex provider configuration when the CLI is not on `PATH`; Codex desktop installations are discovered automatically. To route Codex through a local model instead, set `UseOss = true` and `LocalProvider = "ollama"`.

Applications can query `IModelManagerService.GetProviderCapabilities(key)` for the provider's declared concurrency limit and model-listing support, then call `RetrieveAvailableModelsAsync(key)` when listing is supported. This keeps provider/model selection and worker controls capability-driven in consuming applications.
