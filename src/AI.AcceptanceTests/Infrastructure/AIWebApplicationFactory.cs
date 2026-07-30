// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Exposures;
using cCoder.AI.Brokers.Shells;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI.AcceptanceTests.Infrastructure;

public sealed class AIWebApplicationFactory : WebApplicationFactory<Program>
{
    public TestCompletionProviderService CompletionProviderService { get; } = new();
    public TestShellBroker ShellBroker { get; } = new();
    public TestModelManagerService ModelManagerService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment: "Acceptance");

        builder.ConfigureAppConfiguration(configureDelegate: (_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("AI:DefaultProvider", "Ollama"),
                new KeyValuePair<string, string?>("AI:Providers:Ollama:Name", "Ollama"),
                new KeyValuePair<string, string?>("AI:Providers:Ollama:CompletionProvider:Mode", "OllamaApi"),
                new KeyValuePair<string, string?>("AI:Providers:Ollama:CompletionProvider:Endpoint", "http://localhost:11434/api/chat"),
                new KeyValuePair<string, string?>("AI:Providers:Ollama:CompletionProvider:DefaultModel", "gpt-oss:20b"),
                new KeyValuePair<string, string?>("AI:Providers:Ollama:ModelProvider:Endpoint", "http://localhost:11434"),
            ]);
        });

        builder.ConfigureTestServices(servicesConfiguration: services =>
        {
            services.RemoveAll<ICompletionProviderManager>();
            services.RemoveAll<IShellBroker>();
            services.RemoveAll<IModelManager>();

            services.AddSingleton<ICompletionProviderManager>(CompletionProviderService);
            services.AddSingleton<IShellBroker>(ShellBroker);
            services.AddSingleton<IModelManager>(ModelManagerService);
        });
    }
}