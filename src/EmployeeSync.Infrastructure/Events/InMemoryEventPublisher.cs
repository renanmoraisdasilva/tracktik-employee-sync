using EmployeeSync.Application.Abstractions;
using EmployeeSync.Domain.Events;
using Microsoft.Extensions.Logging;

namespace EmployeeSync.Infrastructure.Events;

public sealed class InMemoryEventPublisher : IEventPublisher
{
    private readonly ILogger<InMemoryEventPublisher> _logger;

    public InMemoryEventPublisher(ILogger<InMemoryEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(EmployeeSynced domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "EmployeeSynced {EventId}: {Provider}/{ProviderEmployeeId} -> {EmployeeId} after {Attempts} attempt(s).",
            domainEvent.EventId,
            domainEvent.Provider,
            domainEvent.ProviderEmployeeId,
            domainEvent.EmployeeId,
            domainEvent.Attempts);

        return Task.CompletedTask;
    }
}
