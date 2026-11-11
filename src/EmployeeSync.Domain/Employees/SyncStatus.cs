namespace EmployeeSync.Domain.Employees;

public enum SyncStatus
{
    Pending,
    Synced,
    Failed
}

public static class SyncStatusExtensions
{
    public static string ToApiString(this SyncStatus status) => status switch
    {
        SyncStatus.Pending => "pending",
        SyncStatus.Synced => "synced",
        SyncStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
