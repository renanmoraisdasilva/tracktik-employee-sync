using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Contracts;

public sealed record EmployeeResponse(
    string Provider,
    string ProviderEmployeeId,
    string EmployeeId,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? Position,
    string? Department,
    DateOnly? StartDate,
    string Status,
    string SyncStatus,
    int Attempts,
    string? LastError,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static EmployeeResponse From(Employee employee) =>
        new(
            employee.Provider,
            employee.ProviderEmployeeId,
            employee.TrackTikEmployeeId,
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.PhoneNumber,
            employee.Position,
            employee.Department,
            employee.StartDate,
            employee.Status.ToApiString(),
            employee.SyncStatus.ToApiString(),
            employee.Attempts,
            employee.LastError,
            employee.LastAttemptAt,
            employee.CreatedAt,
            employee.UpdatedAt);
}
