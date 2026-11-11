using EmployeeSync.Application.Abstractions;
using EmployeeSync.Application.Common.Exceptions;
using EmployeeSync.Application.Contracts;

namespace EmployeeSync.IntegrationTests;

public sealed class FakeTrackTikClient : ITrackTikClient
{
    public List<TrackTikEmployee> Created { get; } = [];

    public List<TrackTikEmployee> Updated { get; } = [];

    public HashSet<string> FailEmployeeIds { get; } = new(StringComparer.Ordinal);

    public Task CreateAsync(TrackTikEmployee employee, CancellationToken cancellationToken)
    {
        if (FailEmployeeIds.Contains(employee.EmployeeId))
        {
            throw new TrackTikException("TrackTik returned 500 Internal Server Error.");
        }

        Created.Add(employee);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TrackTikEmployee employee, CancellationToken cancellationToken)
    {
        if (FailEmployeeIds.Contains(employee.EmployeeId))
        {
            throw new TrackTikException("TrackTik returned 500 Internal Server Error.");
        }

        Updated.Add(employee);
        return Task.CompletedTask;
    }
}
