using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Web.Handlers;

public class ApiAuthorizationHandler : DelegatingHandler
{
    private readonly ITokenService _tokenService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiAuthorizationHandler> _logger;
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);

    public ApiAuthorizationHandler(
        ITokenService _tokenService,
        IHttpClientFactory httpClientFactory,
        ILogger<ApiAuthorizationHandler> logger)
    {
        this._tokenService = _tokenService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = _tokenService.GetAccessToken();
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Detect 401 Unauthorized and attempt token refresh
        if (response.StatusCode == HttpStatusCode.Unauthorized && !request.Headers.Contains("X-Retry-Attempted"))
        {
            var refreshToken = _tokenService.GetRefreshToken();
            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("Api returned 401 but no refresh token available in session.");
                return response;
            }

            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                // Double check if token was refreshed by another thread
                var currentAccessToken = _tokenService.GetAccessToken();
                if (!string.IsNullOrEmpty(currentAccessToken) && currentAccessToken != accessToken)
                {
                    // Token was already refreshed, retry with new token
                    return await RetryRequestWithNewTokenAsync(request, currentAccessToken, cancellationToken);
                }

                _logger.LogInformation("Access token expired (401). Attempting refresh via RawApiClient...");

                // Use RawApiClient (WITHOUT ApiAuthorizationHandler) to prevent recursive loop
                var rawClient = _httpClientFactory.CreateClient("RawApiClient");
                var refreshPayload = new RefreshTokenRequest { RefreshToken = refreshToken };
                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(refreshPayload),
                    Encoding.UTF8,
                    "application/json");

                var refreshResponse = await rawClient.PostAsync("/api/auth/refresh", jsonContent, cancellationToken);
                if (refreshResponse.IsSuccessStatusCode)
                {
                    var responseBody = await refreshResponse.Content.ReadAsStringAsync(cancellationToken);
                    var apiResult = JsonSerializer.Deserialize<ApiResponse<RefreshTokenResponse>>(responseBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (apiResult != null && apiResult.Success && apiResult.Data != null)
                    {
                        _tokenService.SaveTokens(
                            apiResult.Data.AccessToken,
                            apiResult.Data.RefreshToken,
                            apiResult.Data.ExpiresAt);

                        _logger.LogInformation("Token refreshed successfully. Retrying original request (max 1 retry)...");
                        return await RetryRequestWithNewTokenAsync(request, apiResult.Data.AccessToken, cancellationToken);
                    }
                }

                _logger.LogWarning("Refresh token failed with status code {StatusCode}. Clearing tokens.", refreshResponse.StatusCode);
                _tokenService.ClearTokens();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while attempting to refresh token in ApiAuthorizationHandler.");
                _tokenService.ClearTokens();
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        return response;
    }

    private async Task<HttpResponseMessage> RetryRequestWithNewTokenAsync(
        HttpRequestMessage originalRequest,
        string newAccessToken,
        CancellationToken cancellationToken)
    {
        var newRequest = await CloneHttpRequestMessageAsync(originalRequest);
        newRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);
        newRequest.Headers.Add("X-Retry-Attempted", "true");

        return await base.SendAsync(newRequest, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri)
        {
            Version = req.Version
        };

        foreach (var header in req.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (req.Content != null)
        {
            var ms = new MemoryStream();
            await req.Content.CopyToAsync(ms);
            ms.Position = 0;
            clone.Content = new StreamContent(ms);

            foreach (var header in req.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
