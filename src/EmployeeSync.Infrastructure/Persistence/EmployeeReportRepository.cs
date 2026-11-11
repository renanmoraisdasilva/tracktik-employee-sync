using EmployeeSync.Application.Abstractions;
using EmployeeSync.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EmployeeSync.Infrastructure.Persistence;

public sealed class EmployeeReportRepository : IEmployeeReportRepository
{
    private readonly AppDbContext _dbContext;

    public EmployeeReportRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SyncStatusCount>> GetCountsBySyncStatusAsync(
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Database
            .SqlQuery<SyncStatusCountRow>(
                $"SELECT [SyncStatus], COUNT(*) AS [Count] FROM [Employees] GROUP BY [SyncStatus]")
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new SyncStatusCount(row.SyncStatus, row.Count))
            .ToArray();
    }

    public sealed class SyncStatusCountRow
    {
        public string SyncStatus { get; set; } = string.Empty;

        public int Count { get; set; }
    }
}
