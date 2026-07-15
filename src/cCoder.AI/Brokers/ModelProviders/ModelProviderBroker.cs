using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using cCoder.AI.Models.Configurations;

namespace cCoder.AI.Brokers.ModelProviders;

public class ModelProviderBroker(HttpClient httpClient) : IModelProviderBroker
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<string> GetStringAsync(
        AIModelProviderConfiguration configuration,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource requestTimeout = CreateRequestTimeout(
            configuration.TimeoutSeconds,
            cancellationToken);

        using HttpRequestMessage httpRequestMessage =
            new(HttpMethod.Get, BuildUri(configuration.Endpoint, relativePath));

        ApplyAuthentication(configuration, httpRequestMessage);

        using HttpResponseMessage response =
            await httpClient.SendAsync(httpRequestMessage, requestTimeout.Token);

        string content = await response.Content.ReadAsStringAsync(requestTimeout.Token);
        response.EnsureSuccessStatusCode();

        return content;
    }

    public async ValueTask<string> PostAsync(
        AIModelProviderConfiguration configuration,
        string relativePath,
        object payload,
        CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource requestTimeout = CreateRequestTimeout(
            configuration.TimeoutSeconds,
            cancellationToken);

        using HttpRequestMessage httpRequestMessage =
            new(HttpMethod.Post, BuildUri(configuration.Endpoint, relativePath));

        ApplyAuthentication(configuration, httpRequestMessage);

        string serializedPayload = JsonSerializer.Serialize(payload, JsonSerializerOptions);
        httpRequestMessage.Content = new StringContent(serializedPayload, Encoding.UTF8, "application/json");

        using HttpResponseMessage response =
            await httpClient.SendAsync(httpRequestMessage, requestTimeout.Token);

        string content = await response.Content.ReadAsStringAsync(requestTimeout.Token);
        response.EnsureSuccessStatusCode();

        return content;
    }

    private static CancellationTokenSource CreateRequestTimeout(
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (timeoutSeconds > 0)
            requestTimeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        return requestTimeout;
    }

    private static string BuildUri(string endpoint, string relativePath)
    {
        string trimmedEndpoint = endpoint.TrimEnd('/');
        string trimmedRelativePath = relativePath.TrimStart('/');

        return $"{trimmedEndpoint}/{trimmedRelativePath}";
    }

    private static void ApplyAuthentication(
        AIModelProviderConfiguration configuration,
        HttpRequestMessage httpRequestMessage)
    {
        if (string.IsNullOrWhiteSpace(configuration.ApiKey))
        {
            return;
        }

        if (configuration.ApiKeyHeaderName.Equals(
            "Authorization",
            StringComparison.OrdinalIgnoreCase))
        {
            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue(
                configuration.ApiKeyScheme,
                configuration.ApiKey);

            return;
        }

        string headerValue = string.IsNullOrWhiteSpace(configuration.ApiKeyScheme)
            ? configuration.ApiKey
            : $"{configuration.ApiKeyScheme} {configuration.ApiKey}";

        httpRequestMessage.Headers.Add(configuration.ApiKeyHeaderName, headerValue);
    }
}
