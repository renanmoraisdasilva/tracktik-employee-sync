using EmployeeSync.Application.Contracts;

namespace EmployeeSync.Application.Abstractions;

public interface IEmployeeReportRepository
{
    Task<IReadOnlyList<SyncStatusCount>> GetCountsBySyncStatusAsync(CancellationToken cancellationToken);
}
