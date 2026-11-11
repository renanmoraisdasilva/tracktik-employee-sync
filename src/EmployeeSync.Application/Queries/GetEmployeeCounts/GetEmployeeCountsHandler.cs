using EmployeeSync.Application.Abstractions;
using EmployeeSync.Application.Contracts;
using EmployeeSync.Domain.Common;

namespace EmployeeSync.Application.Queries.GetEmployeeCounts;

public sealed class GetEmployeeCountsHandler
    : IQueryHandler<GetEmployeeCountsQuery, Result<IReadOnlyList<SyncStatusCount>>>
{
    private static readonly string[] KnownStatuses = ["pending", "synced", "failed"];

    private readonly IEmployeeReportRepository _repository;

    public GetEmployeeCountsHandler(IEmployeeReportRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<SyncStatusCount>>> HandleAsync(
        GetEmployeeCountsQuery query,
        CancellationToken cancellationToken)
    {
        var counts = await _repository.GetCountsBySyncStatusAsync(cancellationToken);
        var byStatus = counts.ToDictionary(count => count.SyncStatus, StringComparer.OrdinalIgnoreCase);

        var result = KnownStatuses
            .Select(status => new SyncStatusCount(
                status,
                byStatus.TryGetValue(status, out var count) ? count.Count : 0))
            .ToArray();

        return Result<IReadOnlyList<SyncStatusCount>>.Success(result);
    }
}
