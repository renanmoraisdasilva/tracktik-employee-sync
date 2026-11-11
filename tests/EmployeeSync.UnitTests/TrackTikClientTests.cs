using System.Net;
using System.Text;
using System.Text.Json;
using EmployeeSync.Application.Abstractions;
using EmployeeSync.Application.Common.Exceptions;
using EmployeeSync.Application.Contracts;
using EmployeeSync.Infrastructure.TrackTik;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace EmployeeSync.UnitTests;

public sealed class TrackTikClientTests
{
    private static readonly TrackTikEmployee Employee = new(
        "provider1-P1_001",
        "Alice",
        "Johnson",
        "alice.johnson@provider1.com",
        "+1-555-0101",
        "Security Officer",
        "Security Operations",
        new DateOnly(2024, 1, 15),
        "active");

    [Fact]
    public async Task Create_sends_post_with_bearer_token_and_body()
    {
        var handler = new RecordingHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Created));
        var client = CreateClient(handler, new FakeTokenProvider("token-1"));

        await client.CreateAsync(Employee, CancellationToken.None);

        handler.Requests.Count.ShouldBe(1);
        var request = handler.Requests[0];
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.AbsoluteUri.ShouldBe("https://tracktik.test/v1/employees");
        request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe("token-1");
        handler.LastBody.ShouldNotBeNull();

        using var body = JsonDocument.Parse(handler.LastBody!);
        body.RootElement.GetProperty("employeeId").GetString().ShouldBe("provider1-P1_001");
        body.RootElement.GetProperty("startDate").GetString().ShouldBe("2024-01-15");
        body.RootElement.GetProperty("status").GetString().ShouldBe("active");
    }

    [Fact]
    public async Task Update_sends_put_to_the_encoded_employee_path()
    {
        var handler = new RecordingHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler, new FakeTokenProvider("token-1"));

        await client.UpdateAsync(Employee, CancellationToken.None);

        handler.Requests.Count.ShouldBe(1);
        var request = handler.Requests[0];
        request.Method.ShouldBe(HttpMethod.Put);
        request.RequestUri!.AbsoluteUri.ShouldBe(
            "https://tracktik.test/v1/employees/provider1-P1_001");
    }

    [Fact]
    public async Task Unauthorized_response_invalidates_the_token_and_retries_once()
    {
        var handler = new RecordingHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Created));
        var tokenProvider = new FakeTokenProvider("token-1", "token-2");
        var client = CreateClient(handler, tokenProvider);

        await client.CreateAsync(Employee, CancellationToken.None);

        tokenProvider.InvalidateCalls.ShouldBe(1);
        tokenProvider.GetCalls.ShouldBe(2);
        handler.Requests.Count.ShouldBe(2);
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("token-1");
        handler.Requests[1].Headers.Authorization!.Parameter.ShouldBe("token-2");
    }

    [Fact]
    public async Task Create_conflict_falls_back_to_put()
    {
        var handler = new RecordingHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Conflict));
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler, new FakeTokenProvider("token-1"));

        await client.CreateAsync(Employee, CancellationToken.None);

        handler.Requests.Count.ShouldBe(2);
        handler.Requests[0].Method.ShouldBe(HttpMethod.Post);
        handler.Requests[1].Method.ShouldBe(HttpMethod.Put);
    }

    [Fact]
    public async Task Server_error_becomes_a_tracktik_exception()
    {
        var handler = new RecordingHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("upstream failed", Encoding.UTF8, "text/plain")
        });
        var client = CreateClient(handler, new FakeTokenProvider("token-1"));

        var exception = await Should.ThrowAsync<TrackTikException>(
            () => client.CreateAsync(Employee, CancellationToken.None));

        exception.SafeMessage.ShouldContain("500");
    }

    [Fact]
    public async Task Request_timeout_becomes_a_tracktik_exception()
    {
        var client = new TrackTikClient(
            new HttpClient(new TimeoutHandler())
            {
                BaseAddress = new Uri("https://tracktik.test/v1/")
            },
            new FakeTokenProvider("token-1"),
            NullLogger<TrackTikClient>.Instance);

        var exception = await Should.ThrowAsync<TrackTikException>(
            () => client.CreateAsync(Employee, CancellationToken.None));

        exception.SafeMessage.ShouldContain("timed out");
    }

    private static TrackTikClient CreateClient(
        RecordingHandler handler,
        FakeTokenProvider tokenProvider) =>
        new(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://tracktik.test/v1/")
            },
            tokenProvider,
            NullLogger<TrackTikClient>.Instance);

    private sealed class FakeTokenProvider : ITrackTikTokenProvider
    {
        private readonly Queue<string> _tokens;

        public FakeTokenProvider(params string[] tokens)
        {
            _tokens = new Queue<string>(tokens);
        }

        public int GetCalls { get; private set; }

        public int InvalidateCalls { get; private set; }

        public Task<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            GetCalls++;
            return Task.FromResult(_tokens.Peek());
        }

        public void Invalidate()
        {
            InvalidateCalls++;
            if (_tokens.Count > 1)
            {
                _tokens.Dequeue();
            }
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public Queue<HttpResponseMessage> Responses { get; } = new();

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (Responses.Count == 0)
            {
                throw new InvalidOperationException("No response was queued for the request.");
            }

            return Responses.Dequeue();
        }
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new TaskCanceledException("The request timed out.");
    }
}
