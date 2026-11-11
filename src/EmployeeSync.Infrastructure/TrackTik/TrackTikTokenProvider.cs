using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EmployeeSync.Application.Abstractions;
using EmployeeSync.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmployeeSync.Infrastructure.TrackTik;

public sealed class TrackTikTokenProvider : ITrackTikTokenProvider
{
    private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TrackTikOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TrackTikTokenProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _expiresAt;

    public TrackTikTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<TrackTikOptions> options,
        TimeProvider timeProvider,
        ILogger<TrackTikTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (HasValidToken())
        {
            return _cachedToken!;
        }

        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            if (HasValidToken())
            {
                return _cachedToken!;
            }

            var client = _httpClientFactory.CreateClient("TrackTikToken");
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret
            };

            if (!string.IsNullOrWhiteSpace(_options.Scope))
            {
                form["scope"] = _options.Scope;
            }

            using var response = await client.PostAsync(
                _options.TokenUrl,
                new FormUrlEncodedContent(form),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new TrackTikException(
                    $"The TrackTik token endpoint returned {(int)response.StatusCode}. {Truncate(body)}");
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(
                cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(token?.AccessToken))
            {
                throw new TrackTikException("The TrackTik token endpoint returned an empty access token.");
            }

            _cachedToken = token.AccessToken;
            _expiresAt = _timeProvider.GetUtcNow()
                .Add(TimeSpan.FromSeconds(Math.Max(token.ExpiresIn, 60)));

            _logger.LogInformation("Obtained a TrackTik access token; expires at {ExpiresAt}.", _expiresAt);
            return _cachedToken;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new TrackTikException("The TrackTik token request failed.", exception);
        }
        catch (JsonException exception)
        {
            throw new TrackTikException("The TrackTik token response was invalid.", exception);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Invalidate() => _cachedToken = null;

    private bool HasValidToken() =>
        !string.IsNullOrWhiteSpace(_cachedToken)
        && _timeProvider.GetUtcNow() < _expiresAt - ExpirySkew;

    private static string Truncate(string value) =>
        value.Length <= 400 ? value : value[..400];

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
