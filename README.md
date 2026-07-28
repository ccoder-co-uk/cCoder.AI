# cCoder.AI

`cCoder.AI` is a named-provider inference broker. Applications register every AI endpoint once, then select a provider by its key on each completion or agent request.

```csharp
builder.Services.AddAIWeb(ai =>
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

## Consumer exposure

Consumers should use `ChatContext` as the high-level inference boundary. It
accepts a `ChatRequest`, supports attached image paths and agent tools, and can
either return a complete response or stream inference tokens:

```csharp
ChatContext chat = serviceProvider.GetRequiredService<ChatContext>();

AgentRunResponse response = await chat.InferAsync(new ChatRequest
{
    Instructions = "Describe the attached image.",
    Provider = "codex",
    InputFilePaths = [imagePath]
});
```

Use `InferAsStreamAsync` for an `IAsyncEnumerable<AgentStreamTokenResponse>`.
`AddAIWeb` registers the exposure and the library-owned MVC application part.
Applications that map controllers consequently receive:

- `POST /Api/AI/Completions`
- `POST /Api/AI/Agents`
- `POST /Api/AI/Agents/Stream`
- `POST /Api/AI/Chat`
- `POST /Api/AI/Chat/Stream`
- `GET /Api/Model/Providers/{provider}/Available`
- `POST /Api/Model/Providers/{provider}/Import`

The included web application contains only UI-facing controller actions. Its
API controllers are supplied by `cCoder.AI`, so another host receives the same
API surface by referencing and configuring the package.

Provider concurrency is enforced inside `cCoder.AI`, independently for each key. Transient HTTP 408, 429, and 5xx responses are retried using provider retry settings and `Retry-After` headers when supplied. Application-level schedulers may impose lower workload concurrency, but cannot exceed the provider cap during inference.

The Codex provider runs non-interactive `codex exec` requests in ephemeral, read-only mode. Set `ExecutablePath` in the Codex provider configuration when the CLI is not on `PATH`; Codex desktop installations are discovered automatically. To route Codex through a local model instead, set `UseOss = true` and `LocalProvider = "ollama"`.

Applications can query `IModelManagerService.GetProviderCapabilities(key)` for the provider's declared concurrency limit and model-listing support, then call `RetrieveAvailableModelsAsync(key)` when listing is supported. This keeps provider/model selection and worker controls capability-driven in consuming applications.

The sample web application exposes every completion provider currently
implemented by the library: Ollama, OpenAI-compatible, Azure AI Foundry, and
Codex CLI. Keys and endpoints remain configuration-driven; secrets should be
provided through environment variables or another configuration provider.

## Local configuration

The sample application binds an `AIWebConfiguration` root containing the `AI`
domain section. Keep provider secrets empty in `appsettings.json` and provide
them through structured user- or machine-level environment variables:

- `AI__Providers__Ollama__CompletionProvider__ApiKey`
- `AI__Providers__Ollama__ModelProvider__ApiKey`
- `AI__Providers__AzureFoundry__CompletionProvider__ApiKey`
- `AI__Providers__AzureFoundry__ModelProvider__ApiKey`
- `AI__Providers__OpenAI__CompletionProvider__ApiKey`
- `AI__Providers__OpenAI__ModelProvider__ApiKey`
- `AI__Providers__Codex__CompletionProvider__ApiKey`
- `AI__Providers__Codex__ModelProvider__ApiKey`

Only define variables for configured providers that require credentials.
Restart Visual Studio after changing user- or machine-level variables, then run
the application with F5. No local secrets file or configuration conversion step
is required.
