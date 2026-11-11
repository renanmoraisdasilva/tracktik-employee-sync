namespace EmployeeSync.Domain.Employees;

public sealed record MappedEmployee(
    string ProviderEmployeeId,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? Position,
    string? Department,
    DateOnly? StartDate,
    EmployeeStatus Status);
