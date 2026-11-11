using System.Text.Json;
using EmployeeSync.Api.Extensions;
using EmployeeSync.Application.Abstractions;
using EmployeeSync.Application.Commands.SubmitEmployee;
using EmployeeSync.Application.Contracts;
using EmployeeSync.Application.Queries.GetEmployee;
using EmployeeSync.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeSync.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class EmployeesController : ControllerBase
{
    private readonly ICommandHandler<SubmitEmployeeCommand, Result<SubmitEmployeeResult>> _submitHandler;
    private readonly IQueryHandler<GetEmployeeQuery, Result<EmployeeResponse>> _getHandler;

    public EmployeesController(
        ICommandHandler<SubmitEmployeeCommand, Result<SubmitEmployeeResult>> submitHandler,
        IQueryHandler<GetEmployeeQuery, Result<EmployeeResponse>> getHandler)
    {
        _submitHandler = submitHandler;
        _getHandler = getHandler;
    }

    [HttpPost("providers/{provider}/employees")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Submit(
        [FromRoute] string provider,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return new BadRequestObjectResult(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["payload"] = ["A JSON body is required."] }))
            {
                ContentTypes = { "application/problem+json" }
            };
        }

        var result = await _submitHandler.HandleAsync(
            new SubmitEmployeeCommand(provider, payload),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.Error!.ToActionResult(this);
        }

        var value = result.Value;

        return value.Created
            ? CreatedAtAction(
                nameof(Get),
                new
                {
                    provider = value.Employee.Provider,
                    providerEmployeeId = value.Employee.ProviderEmployeeId
                },
                value.Employee)
            : Ok(value.Employee);
    }

    [HttpGet("employees/{provider}/{providerEmployeeId}")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get(
        [FromRoute] string provider,
        [FromRoute] string providerEmployeeId,
        CancellationToken cancellationToken)
    {
        var result = await _getHandler.HandleAsync(
            new GetEmployeeQuery(provider, providerEmployeeId),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error!.ToActionResult(this);
    }
}
