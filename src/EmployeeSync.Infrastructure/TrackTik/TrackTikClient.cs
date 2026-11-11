using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EmployeeSync.Application.Abstractions;
using EmployeeSync.Application.Common.Exceptions;
using EmployeeSync.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace EmployeeSync.Infrastructure.TrackTik;

public sealed class TrackTikClient : ITrackTikClient
{
    private readonly HttpClient _httpClient;
    private readonly ITrackTikTokenProvider _tokenProvider;
    private readonly ILogger<TrackTikClient> _logger;

    public TrackTikClient(
        HttpClient httpClient,
        ITrackTikTokenProvider tokenProvider,
        ILogger<TrackTikClient> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public Task CreateAsync(TrackTikEmployee employee, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, "employees", employee, allowConflictFallback: true, cancellationToken);

    public Task UpdateAsync(TrackTikEmployee employee, CancellationToken cancellationToken) =>
        SendAsync(
            HttpMethod.Put,
            $"employees/{Uri.EscapeDataString(employee.EmployeeId)}",
            employee,
            allowConflictFallback: false,
            cancellationToken);

    private async Task SendAsync(
        HttpMethod method,
        string path,
        TrackTikEmployee employee,
        bool allowConflictFallback,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await _tokenProvider.GetTokenAsync(cancellationToken);

            using var request = new HttpRequestMessage(method, path)
            {
                Content = JsonContent.Create(employee)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage response;

            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TrackTikException("The TrackTik request timed out.", exception);
            }
            catch (HttpRequestException exception)
            {
                throw new TrackTikException("The TrackTik request failed.", exception);
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                {
                    _tokenProvider.Invalidate();
                    continue;
                }

                if (allowConflictFallback && response.StatusCode == HttpStatusCode.Conflict)
                {
                    _logger.LogInformation(
                        "TrackTik reported that {EmployeeId} already exists; retrying with PUT.",
                        employee.EmployeeId);

                    await UpdateAsync(employee, cancellationToken);
                    return;
                }

                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new TrackTikException(
                    $"TrackTik returned {(int)response.StatusCode} {response.ReasonPhrase}. {Truncate(body)}");
            }
        }

        throw new TrackTikException("TrackTik authentication failed after a token refresh.");
    }

    private static string Truncate(string value) =>
        value.Length <= 400 ? value : value[..400];
}
