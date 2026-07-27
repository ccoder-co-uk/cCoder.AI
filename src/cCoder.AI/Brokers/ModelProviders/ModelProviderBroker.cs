// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

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
timeoutSeconds: configuration.TimeoutSeconds,
cancellationToken: cancellationToken);

        using HttpRequestMessage httpRequestMessage =
            new(HttpMethod.Get, BuildUri(endpoint: configuration.Endpoint, relativePath: relativePath));

        ApplyAuthentication(configuration: configuration, httpRequestMessage: httpRequestMessage);

        using HttpResponseMessage response =
            await httpClient.SendAsync(request: httpRequestMessage, cancellationToken: requestTimeout.Token);

        string content = await response.Content.ReadAsStringAsync(cancellationToken: requestTimeout.Token);
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
timeoutSeconds: configuration.TimeoutSeconds,
cancellationToken: cancellationToken);

        using HttpRequestMessage httpRequestMessage =
            new(HttpMethod.Post, BuildUri(endpoint: configuration.Endpoint, relativePath: relativePath));

        ApplyAuthentication(configuration: configuration, httpRequestMessage: httpRequestMessage);

        string serializedPayload = JsonSerializer.Serialize(value: payload, options: JsonSerializerOptions);
        httpRequestMessage.Content = new StringContent(content: serializedPayload, encoding: Encoding.UTF8, mediaType: "application/json");

        using HttpResponseMessage response =
            await httpClient.SendAsync(request: httpRequestMessage, cancellationToken: requestTimeout.Token);

        string content = await response.Content.ReadAsStringAsync(cancellationToken: requestTimeout.Token);
        response.EnsureSuccessStatusCode();

        return content;
    }

    private static CancellationTokenSource CreateRequestTimeout(
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(token: cancellationToken);

        if (timeoutSeconds > 0)
            requestTimeout.CancelAfter(delay: TimeSpan.FromSeconds(timeoutSeconds));

        return requestTimeout;
    }

    private static string BuildUri(string endpoint, string relativePath)
    {
        string trimmedEndpoint = endpoint.TrimEnd(trimChar: '/');
        string trimmedRelativePath = relativePath.TrimStart(trimChar: '/');

        return $"{trimmedEndpoint}/{trimmedRelativePath}";
    }

    private static void ApplyAuthentication(
        AIModelProviderConfiguration configuration,
        HttpRequestMessage httpRequestMessage)
    {
        if (string.IsNullOrWhiteSpace(value: configuration.ApiKey))
        {
            return;
        }

        if (configuration.ApiKeyHeaderName.Equals(
value: "Authorization",
comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue(
scheme: configuration.ApiKeyScheme,
parameter: configuration.ApiKey);

            return;
        }

        string headerValue = string.IsNullOrWhiteSpace(value: configuration.ApiKeyScheme)
            ? configuration.ApiKey
            : $"{configuration.ApiKeyScheme} {configuration.ApiKey}";

        httpRequestMessage.Headers.Add(name: configuration.ApiKeyHeaderName, value: headerValue);
    }
}