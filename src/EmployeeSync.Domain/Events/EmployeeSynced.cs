using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Domain.Events;

public sealed record EmployeeSynced(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    string Provider,
    string ProviderEmployeeId,
    string EmployeeId,
    int Attempts)
{
    public static EmployeeSynced From(Employee employee, DateTimeOffset now) =>
        new(
            Guid.NewGuid(),
            nameof(EmployeeSynced),
            now,
            employee.Provider,
            employee.ProviderEmployeeId,
            employee.TrackTikEmployeeId,
            employee.Attempts);
}
