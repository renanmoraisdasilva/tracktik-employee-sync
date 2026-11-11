using EmployeeSync.Application.Abstractions;
using EmployeeSync.Domain.Employees;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EmployeeSync.Infrastructure.Persistence;

public sealed class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _dbContext;

    public EmployeeRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Employee?> FindAsync(EmployeeIdentity identity, CancellationToken cancellationToken) =>
        _dbContext.Employees.SingleOrDefaultAsync(
            employee => employee.Provider == identity.Provider
                && employee.ProviderEmployeeId == identity.ProviderEmployeeId,
            cancellationToken);

    public async Task<bool> TryAddAsync(Employee employee, CancellationToken cancellationToken)
    {
        _dbContext.Employees.Add(employee);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            _dbContext.Entry(employee).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _dbContext.SaveChangesAsync(cancellationToken);

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        if (exception.InnerException is SqlException sqlException)
        {
            return sqlException.Number is 2601 or 2627;
        }

        return exception.InnerException?.Message.Contains(
            "UNIQUE constraint failed",
            StringComparison.OrdinalIgnoreCase) == true;
    }
}
