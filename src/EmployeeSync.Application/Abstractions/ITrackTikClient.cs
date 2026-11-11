using EmployeeSync.Application.Contracts;

namespace EmployeeSync.Application.Abstractions;

public interface ITrackTikClient
{
    Task CreateAsync(TrackTikEmployee employee, CancellationToken cancellationToken);

    Task UpdateAsync(TrackTikEmployee employee, CancellationToken cancellationToken);
}
