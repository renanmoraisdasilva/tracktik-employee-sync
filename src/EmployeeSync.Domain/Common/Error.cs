namespace EmployeeSync.Domain.Common;

public sealed record Error(
    ErrorType Type,
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    SyncFailureInfo? SyncFailure = null)
{
    public static Error Validation(string field, string message) =>
        Validation(new Dictionary<string, string[]> { [field] = [message] });

    public static Error Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(ErrorType.Validation, "validation_failed", "One or more validation errors occurred.", errors);

    public static Error NotFound(string message) =>
        new(ErrorType.NotFound, "not_found", message);

    public static Error SyncFailed(string message, SyncFailureInfo info) =>
        new(ErrorType.SyncFailed, "tracktik_sync_failed", message, null, info);

    public static Error Unexpected(string message) =>
        new(ErrorType.Unexpected, "unexpected_error", message);
}
