using System.Text.Json;
using EmployeeSync.Domain.Events;
using EmployeeSync.Infrastructure.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using Shouldly;
using Testcontainers.Nats;

namespace EmployeeSync.Testcontainers;

[Trait("Category", "Testcontainers")]
public sealed class NatsEventPublisherTests : IAsyncLifetime
{
    private readonly NatsContainer _container = new NatsBuilder("nats:2-alpine").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Publish_sends_the_event_to_the_configured_subject()
    {
        var subject = $"tracktik.employees.test.{Guid.NewGuid():N}";
        var options = Options.Create(new NatsOptions
        {
            Url = _container.GetConnectionString(),
            Subject = subject
        });

        await using var subscriber = new NatsConnection(new NatsOpts
        {
            Url = _container.GetConnectionString()
        });
        await subscriber.ConnectAsync();
        await using var subscription = await subscriber.SubscribeCoreAsync<byte[]>(subject);

        var publisher = new NatsEventPublisher(
            options,
            NullLogger<NatsEventPublisher>.Instance);

        var domainEvent = new EmployeeSynced(
            Guid.NewGuid(),
            nameof(EmployeeSynced),
            new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero),
            "provider1",
            "P1_001",
            "provider1-P1_001",
            1);

        await publisher.PublishAsync(domainEvent, CancellationToken.None);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var message = await subscription.Msgs.ReadAsync(cancellation.Token);
        message.Data.ShouldNotBeNull();

        var received = JsonSerializer.Deserialize<EmployeeSynced>(message.Data!);
        received.ShouldNotBeNull();
        received!.EventId.ShouldBe(domainEvent.EventId);
        received.Provider.ShouldBe("provider1");
        received.ProviderEmployeeId.ShouldBe("P1_001");
        received.EmployeeId.ShouldBe("provider1-P1_001");

        await publisher.DisposeAsync();
    }
}
