using System.Globalization;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Providers.Provider2;

internal static class Provider2Mapper
{
    public static MappedEmployee Map(Provider2EmployeeRequest request) =>
        new(
            request.EmployeeNumber!.Trim(),
            request.PersonalInfo!.GivenName!.Trim(),
            request.PersonalInfo.FamilyName!.Trim(),
            request.PersonalInfo.Email!.Trim(),
            Clean(request.PersonalInfo.Mobile),
            Clean(request.WorkInfo!.Role),
            Clean(request.WorkInfo.Division),
            ParseDate(request.WorkInfo.StartDate),
            ParseStatus(request.WorkInfo.CurrentStatus));

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly? ParseDate(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static EmployeeStatus ParseStatus(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "employed" => EmployeeStatus.Active,
            "on_leave" => EmployeeStatus.Inactive,
            "terminated" => EmployeeStatus.Terminated,
            _ => throw new InvalidOperationException("The current_status value was not validated.")
        };
}
