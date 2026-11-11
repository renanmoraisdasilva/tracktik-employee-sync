using EmployeeSync.Application.Abstractions;
using EmployeeSync.Application.Contracts;

namespace EmployeeSync.Testcontainers;

public sealed class FakeTrackTikClient : ITrackTikClient
{
    public List<TrackTikEmployee> Created { get; } = [];

    public List<TrackTikEmployee> Updated { get; } = [];

    public Task CreateAsync(TrackTikEmployee employee, CancellationToken cancellationToken)
    {
        Created.Add(employee);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TrackTikEmployee employee, CancellationToken cancellationToken)
    {
        Updated.Add(employee);
        return Task.CompletedTask;
    }
}
