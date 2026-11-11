using EmployeeSync.Application.Abstractions;
using EmployeeSync.Domain.Events;

namespace EmployeeSync.Testcontainers;

public sealed class FakeEventPublisher : IEventPublisher
{
    public List<EmployeeSynced> Events { get; } = [];

    public Task PublishAsync(EmployeeSynced domainEvent, CancellationToken cancellationToken)
    {
        Events.Add(domainEvent);
        return Task.CompletedTask;
    }
}
