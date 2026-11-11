namespace EmployeeSync.Domain.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    SyncFailed,
    Unexpected
}
