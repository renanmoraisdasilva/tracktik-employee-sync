using System.Net;
using System.Text;
using EmployeeSync.Application.Common.Exceptions;
using EmployeeSync.Infrastructure.TrackTik;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace EmployeeSync.UnitTests;

public sealed class TrackTikTokenProviderTests
{
    [Fact]
    public async Task Reuses_the_cached_token_until_the_expiry_skew()
    {
        var handler = new TokenHandler();
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(handler, clock);

        var first = await provider.GetTokenAsync(CancellationToken.None);
        var second = await provider.GetTokenAsync(CancellationToken.None);

        first.ShouldBe("token-1");
        second.ShouldBe("token-1");
        handler.Calls.ShouldBe(1);
        handler.LastBody.ShouldNotBeNull();
        handler.LastBody.ShouldContain("grant_type=client_credentials");
        handler.LastBody.ShouldContain("client_id=test-client");

        clock.Advance(TimeSpan.FromHours(2));

        var third = await provider.GetTokenAsync(CancellationToken.None);
        third.ShouldBe("token-2");
        handler.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task Invalidate_forces_a_new_token()
    {
        var handler = new TokenHandler();
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(handler, clock);

        await provider.GetTokenAsync(CancellationToken.None);
        provider.Invalidate();
        var token = await provider.GetTokenAsync(CancellationToken.None);

        token.ShouldBe("token-2");
        handler.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task Concurrent_callers_share_one_refresh()
    {
        var handler = new TokenHandler();
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(handler, clock);

        var tokens = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => provider.GetTokenAsync(CancellationToken.None)));

        tokens.ShouldAllBe(token => token == "token-1");
        handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Invalid_json_response_is_a_tracktik_exception()
    {
        var handler = new TokenHandler { BodyOverride = "not-json" };
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(handler, clock);

        var exception = await Should.ThrowAsync<TrackTikException>(
            () => provider.GetTokenAsync(CancellationToken.None));

        exception.SafeMessage.ShouldContain("invalid");
    }

    [Fact]
    public async Task Response_without_an_access_token_is_a_tracktik_exception()
    {
        var handler = new TokenHandler { BodyOverride = "{\"expires_in\":3600}" };
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(handler, clock);

        var exception = await Should.ThrowAsync<TrackTikException>(
            () => provider.GetTokenAsync(CancellationToken.None));

        exception.SafeMessage.ShouldContain("empty access token");
    }

    [Fact]
    public async Task Configured_scope_is_sent_in_the_token_request()
    {
        var handler = new TokenHandler();
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(handler, clock, "employees:read");

        await provider.GetTokenAsync(CancellationToken.None);

        handler.LastBody.ShouldNotBeNull();
        handler.LastBody.ShouldContain("scope=employees%3Aread");
    }

    [Fact]
    public async Task Unsupported_token_content_read_is_a_tracktik_exception()
    {
        var handler = new TokenHandler { UnsupportedContent = true };
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(handler, clock);

        var exception = await Should.ThrowAsync<TrackTikException>(
            () => provider.GetTokenAsync(CancellationToken.None));

        exception.SafeMessage.ShouldContain("invalid");
    }

    [Fact]
    public async Task Token_endpoint_timeout_is_a_tracktik_exception()
    {
        var handler = new TokenHandler { SimulateTimeout = true };
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(handler, clock);

        var exception = await Should.ThrowAsync<TrackTikException>(
            () => provider.GetTokenAsync(CancellationToken.None));

        exception.SafeMessage.ShouldContain("timed out");
    }

    [Fact]
    public async Task Caller_cancellation_is_not_converted_to_a_tracktik_exception()
    {
        var handler = new TokenHandler();
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(handler, clock);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(
            () => provider.GetTokenAsync(cancellation.Token));
    }

    [Fact]
    public async Task Token_endpoint_failure_is_a_tracktik_exception()
    {
        var handler = new TokenHandler { Fail = true };
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(handler, clock);

        var exception = await Should.ThrowAsync<TrackTikException>(
            () => provider.GetTokenAsync(CancellationToken.None));

        exception.SafeMessage.ShouldContain("token endpoint");
    }

    private static TrackTikTokenProvider CreateProvider(
        TokenHandler handler,
        FakeTimeProvider clock,
        string? scope = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("TrackTikToken").Returns(_ => new HttpClient(handler));

        return new TrackTikTokenProvider(
            factory,
            Options.Create(new TrackTikOptions
            {
                BaseUrl = "https://tracktik.example/v1",
                TokenUrl = "https://tracktik.example/oauth/token",
                ClientId = "test-client",
                ClientSecret = "test-secret",
                Scope = scope,
                TimeoutSeconds = 15
            }),
            clock,
            NullLogger<TrackTikTokenProvider>.Instance);
    }

    private sealed class TokenHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? LastBody { get; private set; }

        public bool Fail { get; init; }

        public string? BodyOverride { get; init; }

        public bool UnsupportedContent { get; init; }

        public bool SimulateTimeout { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;

            if (SimulateTimeout)
            {
                throw new TaskCanceledException(
                    "The request timed out.",
                    new TimeoutException());
            }
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (Fail)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("upstream error")
                };
            }

            if (UnsupportedContent)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new UnsupportedContentResponse()
                };
            }

            if (BodyOverride is not null)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BodyOverride, Encoding.UTF8, "application/json")
                };
            }

            var token = $"token-{Calls}";
            var json = $"{{\"access_token\":\"{token}\",\"expires_in\":3600}}";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        private sealed class UnsupportedContentResponse : HttpContent
        {
            public UnsupportedContentResponse()
            {
                Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            }

            protected override Task SerializeToStreamAsync(
                Stream stream,
                System.Net.TransportContext? context) =>
                throw new NotSupportedException(
                    "The token response content type is not supported.");

            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return false;
            }
        }
    }
}
