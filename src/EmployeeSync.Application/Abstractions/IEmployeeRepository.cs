using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Abstractions;

public interface IEmployeeRepository
{
    Task<Employee?> FindAsync(EmployeeIdentity identity, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to insert the employee. Returns false when the unique identity
    /// already exists, so the caller can reload and update it.
    /// </summary>
    Task<bool> TryAddAsync(Employee employee, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
