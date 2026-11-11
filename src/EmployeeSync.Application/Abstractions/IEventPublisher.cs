using EmployeeSync.Domain.Events;

namespace EmployeeSync.Application.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync(EmployeeSynced domainEvent, CancellationToken cancellationToken);
}
