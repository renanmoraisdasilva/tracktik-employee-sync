namespace EmployeeSync.Domain.Employees;

public enum EmployeeStatus
{
    Unknown = 0,
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
        switch (value?.Trim().ToLowerInvariant())
        {
            case "active":
                status = EmployeeStatus.Active;
                return true;
            case "inactive":
                status = EmployeeStatus.Inactive;
                return true;
            case "terminated":
                status = EmployeeStatus.Terminated;
                return true;
            default:
                status = EmployeeStatus.Unknown;
                return false;
        }
    }
}
