namespace Yashdeep.SyncEngine.Clients;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yashdeep.Shared.Sync;
using Yashdeep.SyncEngine.Abstractions;

public class SyncRestApiClient : ISyncRestApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public SyncRestApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<SyncPushResponse> PushBatchAsync(SyncPushRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sync/push");

        if (!string.IsNullOrWhiteSpace(request.AuthToken))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.AuthToken);
        }

        httpRequest.Headers.Add("X-Tenant-Id", request.TenantId.ToString());
        httpRequest.Headers.Add("X-Branch-Id", request.BranchId.ToString());
        httpRequest.Headers.Add("X-Device-Id", request.DeviceId.ToString());

        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Sync HTTP request timed out.");
        }

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return new SyncPushResponse
            {
                Success = false,
                BatchError = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}",
                ServerTimeUtc = DateTime.UtcNow
            };
        }

        try
        {
            var pushResponse = await response.Content.ReadFromJsonAsync<SyncPushResponse>(JsonOptions, cancellationToken);
            return pushResponse ?? new SyncPushResponse
            {
                Success = false,
                BatchError = "Malformed JSON response: empty payload.",
                ServerTimeUtc = DateTime.UtcNow
            };
        }
        catch (JsonException ex)
        {
            return new SyncPushResponse
            {
                Success = false,
                BatchError = $"Malformed JSON response: {ex.Message}",
                ServerTimeUtc = DateTime.UtcNow
            };
        }
    }
}
