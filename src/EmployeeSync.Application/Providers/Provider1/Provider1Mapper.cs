using System.Globalization;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Providers.Provider1;

internal static class Provider1Mapper
{
    public static MappedEmployee Map(Provider1EmployeeRequest request) =>
        new(
            request.EmpId!.Trim(),
            request.FirstName!.Trim(),
            request.LastName!.Trim(),
            request.EmailAddress!.Trim(),
            Clean(request.Phone),
            Clean(request.JobTitle),
            Clean(request.Department),
            ParseDate(request.HireDate),
            ParseStatus(request.EmploymentStatus));

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly? ParseDate(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static EmployeeStatus ParseStatus(string? value) =>
        EmployeeStatusExtensions.TryParse(value, out var status)
            ? status
            : throw new InvalidOperationException("The employment_status value was not validated.");
}
