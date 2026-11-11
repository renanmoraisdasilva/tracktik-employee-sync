using System.Text.Json.Serialization;

namespace EmployeeSync.Application.Contracts;

public sealed record TrackTikEmployee(
    [property: JsonPropertyName("employeeId")] string EmployeeId,
    [property: JsonPropertyName("firstName")] string FirstName,
    [property: JsonPropertyName("lastName")] string LastName,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("phoneNumber")] string? PhoneNumber,
    [property: JsonPropertyName("position")] string? Position,
    [property: JsonPropertyName("department")] string? Department,
    [property: JsonPropertyName("startDate")] DateOnly? StartDate,
    [property: JsonPropertyName("status")] string Status);
