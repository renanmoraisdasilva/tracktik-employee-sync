namespace EmployeeSync.Domain.Common;

public sealed record SyncFailureInfo(
    string SyncStatus,
    int Attempts,
    string? LastError,
    DateTimeOffset? LastAttemptAt);
