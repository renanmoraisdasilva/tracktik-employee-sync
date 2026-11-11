using EmployeeSync.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeSync.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Error error, ControllerBase controller)
    {
        var instance = controller.HttpContext.Request.Path.Value ?? string.Empty;

        return error.Type switch
        {
            ErrorType.Validation => ValidationResult(error, instance, controller),
            ErrorType.NotFound => ProblemResult(
                StatusCodes.Status404NotFound,
                "Not Found",
                error.Message,
                instance,
                controller),
            ErrorType.SyncFailed => SyncFailureResult(error, instance, controller),
            _ => ProblemResult(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                error.Message,
                instance,
                controller)
        };
    }

    private static IActionResult ValidationResult(
        Error error,
        string instance,
        ControllerBase controller)
    {
        var errors = error.ValidationErrors?.ToDictionary(pair => pair.Key, pair => pair.Value)
            ?? new Dictionary<string, string[]> { ["payload"] = [error.Message] };

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Instance = instance
        };

        return controller.ValidationProblem(problem);
    }

    private static IActionResult SyncFailureResult(
        Error error,
        string instance,
        ControllerBase controller)
    {
        var result = controller.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "TrackTik synchronization failed",
            detail: error.Message,
            instance: instance);

        if (result.Value is ProblemDetails problem && error.SyncFailure is not null)
        {
            problem.Extensions["syncStatus"] = error.SyncFailure.SyncStatus;
            problem.Extensions["attempts"] = error.SyncFailure.Attempts;
            problem.Extensions["lastError"] = error.SyncFailure.LastError;
            problem.Extensions["lastAttemptAt"] = error.SyncFailure.LastAttemptAt;
        }

        return result;
    }

    private static IActionResult ProblemResult(
        int status,
        string title,
        string detail,
        string instance,
        ControllerBase controller) =>
        controller.Problem(
            statusCode: status,
            title: title,
            detail: detail,
            instance: instance);
}
