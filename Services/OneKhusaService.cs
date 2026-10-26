using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Test.VerifyWebook.Models;

namespace Test.VerifyWebook.Services;

public class OneKhusaService
{
    private readonly HttpClient _httpClient;
    private readonly OneKhusaSettings _settings;
    private readonly ILogger<OneKhusaService> _logger;
    private string? _cachedAccessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public OneKhusaService(HttpClient httpClient, IOptions<OneKhusaSettings> settings, ILogger<OneKhusaService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<(GetAccessTokenResponse? response, string? error)> GetAccessTokenAsync()
    {
        var request = new GetAccessTokenRequest
        {
            ApiKey = _settings.ApiKey,
            ApiSecret = _settings.ApiSecret,
            OrganisationId = _settings.OrganisationId,
            MerchantAccountNumber = _settings.MerchantAccountNumber
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_settings.BaseUrl}/account/getAccessToken", request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GetAccessToken failed with status {StatusCode}: {Body}", response.StatusCode, body);
                return (null, $"HTTP {(int)response.StatusCode}: {body}");
            }

            var result = JsonSerializer.Deserialize<GetAccessTokenResponse>(body);
            if (result == null)
            {
                return (null, $"Failed to deserialize response: {body}");
            }

            _cachedAccessToken = result.AccessToken;
            _tokenExpiry = result.ExpiresOn;

            _logger.LogInformation("Access token obtained, expires on {ExpiresOn}", result.ExpiresOn);
            return (result, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAccessToken request failed");
            return (null, ex.Message);
        }
    }

    public async Task<string?> GetValidAccessTokenAsync()
    {
        if (string.IsNullOrEmpty(_cachedAccessToken) || DateTime.UtcNow >= _tokenExpiry.AddMinutes(-1))
        {
            var (response, error) = await GetAccessTokenAsync();
            if (error != null || response == null)
                return null;
            return response.AccessToken;
        }
        return _cachedAccessToken;
    }

    public async Task<(RequestToPayResponse? response, string? error)> RequestToPayAsync(RequestToPayRequest request)
    {
        var token = await GetValidAccessTokenAsync();
        if (token == null)
        {
            return (null, "Failed to obtain access token. Check your OneKhusa credentials in appsettings.json.");
        }

        var idempotencyKey = Guid.NewGuid().ToString("N");

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/collections/requestToPay/initiate")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            httpRequest.Headers.TryAddWithoutValidation("Accept-Language", "en");
            httpRequest.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);

            var response = await _httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("RequestToPay failed with status {StatusCode}: {Body}", response.StatusCode, responseBody);
                return (null, $"HTTP {(int)response.StatusCode}: {responseBody}");
            }

            var result = JsonSerializer.Deserialize<RequestToPayResponse>(responseBody);
            if (result == null)
            {
                return (null, $"Failed to deserialize response: {responseBody}");
            }

            _logger.LogInformation("RequestToPay initiated - TAN: {TAN}, expires on {Expiry}",
                result.TimedAccountNumber, result.ExpiryDate);

            return (result, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RequestToPay request failed");
            return (null, ex.Message);
        }
    }

    public async Task<(JsonElement? response, string? error)> PushPaymentAsync(PushPaymentRequest request)
    {
        var token = await GetValidAccessTokenAsync();
        if (token == null)
        {
            return (null, "Failed to obtain access token. Check your OneKhusa credentials in appsettings.json.");
        }

        var idempotencyKey = Guid.NewGuid().ToString("N");

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/collections/push/direct/initiate")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            httpRequest.Headers.TryAddWithoutValidation("Accept-Language", "en");
            httpRequest.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);

            var response = await _httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PushPayment failed with status {StatusCode}: {Body}", response.StatusCode, responseBody);
                return (null, $"HTTP {(int)response.StatusCode}: {responseBody}");
            }

            using var document = JsonDocument.Parse(responseBody);
            return (document.RootElement.Clone(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PushPayment request failed");
            return (null, ex.Message);
        }
    }
}
