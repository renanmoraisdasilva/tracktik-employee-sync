using EmployeeSync.Application.Abstractions;
using EmployeeSync.Domain.Employees;
using EmployeeSync.Infrastructure.Persistence;

namespace EmployeeSync.IntegrationTests;

public sealed class SaveFailureSwitch
{
    public bool Fail { get; set; }
}

public sealed class FailingEmployeeRepository : IEmployeeRepository
{
    private readonly EmployeeRepository _inner;
    private readonly SaveFailureSwitch _switch;

    public FailingEmployeeRepository(AppDbContext dbContext, SaveFailureSwitch failureSwitch)
    {
        _inner = new EmployeeRepository(dbContext);
        _switch = failureSwitch;
    }

    public Task<Employee?> FindAsync(EmployeeIdentity identity, CancellationToken cancellationToken) =>
        _inner.FindAsync(identity, cancellationToken);

    public Task<bool> TryAddAsync(Employee employee, CancellationToken cancellationToken) =>
        _inner.TryAddAsync(employee, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _switch.Fail
            ? Task.FromException(new InvalidOperationException("database unavailable"))
            : _inner.SaveChangesAsync(cancellationToken);
}
