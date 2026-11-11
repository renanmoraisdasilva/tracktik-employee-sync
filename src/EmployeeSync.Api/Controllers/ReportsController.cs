using EmployeeSync.Api.Extensions;
using EmployeeSync.Application.Abstractions;
using EmployeeSync.Application.Contracts;
using EmployeeSync.Application.Queries.GetEmployeeCounts;
using EmployeeSync.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeSync.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IQueryHandler<GetEmployeeCountsQuery, Result<IReadOnlyList<SyncStatusCount>>> _handler;

    public ReportsController(
        IQueryHandler<GetEmployeeCountsQuery, Result<IReadOnlyList<SyncStatusCount>>> handler)
    {
        _handler = handler;
    }

    [HttpGet("employees-by-sync-status")]
    [ProducesResponseType(typeof(IReadOnlyList<SyncStatusCount>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> GetCounts(CancellationToken cancellationToken)
    {
        var result = await _handler.HandleAsync(new GetEmployeeCountsQuery(), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error!.ToActionResult(this);
    }
}
