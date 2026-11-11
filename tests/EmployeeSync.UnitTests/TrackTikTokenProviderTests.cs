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
        FakeTimeProvider clock)
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

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
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

            var token = $"token-{Calls}";
            var json = $"{{\"access_token\":\"{token}\",\"expires_in\":3600}}";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
