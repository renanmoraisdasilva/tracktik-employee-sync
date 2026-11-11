using EmployeeSync.Domain.Events;
using EmployeeSync.Infrastructure.Events;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace EmployeeSync.UnitTests;

public sealed class InMemoryEventPublisherTests
{
    [Fact]
    public async Task Publish_writes_the_event_identity_to_the_log()
    {
        var logger = new ListLogger<InMemoryEventPublisher>();
        var publisher = new InMemoryEventPublisher(logger);
        var domainEvent = new EmployeeSynced(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            nameof(EmployeeSynced),
            new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero),
            "provider1",
            "P1_001",
            "provider1-P1_001",
            1);

        await publisher.PublishAsync(domainEvent, CancellationToken.None);

        logger.Messages.ShouldContain(message =>
            message.Contains("11111111-1111-1111-1111-111111111111", StringComparison.Ordinal)
            && message.Contains("provider1-P1_001", StringComparison.Ordinal));
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
