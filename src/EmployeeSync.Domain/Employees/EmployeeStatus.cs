namespace EmployeeSync.Domain.Employees;

public enum EmployeeStatus
{
    Active,
    Inactive,
    Terminated
}

public static class EmployeeStatusExtensions
{
    public static string ToApiString(this EmployeeStatus status) => status switch
    {
        EmployeeStatus.Active => "active",
        EmployeeStatus.Inactive => "inactive",
        EmployeeStatus.Terminated => "terminated",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static bool TryParse(string? value, out EmployeeStatus status)
    {
        status = value?.Trim().ToLowerInvariant() switch
        {
            "active" => EmployeeStatus.Active,
            "inactive" => EmployeeStatus.Inactive,
            "terminated" => EmployeeStatus.Terminated,
            _ => EmployeeStatus.Active
        };

        return value?.Trim().ToLowerInvariant() is "active" or "inactive" or "terminated";
    }
}
