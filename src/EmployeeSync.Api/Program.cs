using EmployeeSync.Api.Extensions;
using EmployeeSync.Application.Abstractions;
using EmployeeSync.Application.Commands.SubmitEmployee;
using EmployeeSync.Application.Contracts;
using EmployeeSync.Application.Providers;
using EmployeeSync.Application.Providers.Provider1;
using EmployeeSync.Application.Providers.Provider2;
using EmployeeSync.Application.Queries.GetEmployee;
using EmployeeSync.Application.Queries.GetEmployeeCounts;
using EmployeeSync.Application.Validation;
using EmployeeSync.Domain.Common;
using EmployeeSync.Domain.Employees;
using EmployeeSync.Infrastructure;
using EmployeeSync.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<ICommandHandler<SubmitEmployeeCommand, Result<SubmitEmployeeResult>>, SubmitEmployeeHandler>();
builder.Services.AddScoped<IQueryHandler<GetEmployeeQuery, Result<EmployeeResponse>>, GetEmployeeHandler>();
builder.Services.AddScoped<IQueryHandler<GetEmployeeCountsQuery, Result<IReadOnlyList<SyncStatusCount>>>, GetEmployeeCountsHandler>();

builder.Services.AddScoped<IProviderAdapter, Provider1Adapter>();
builder.Services.AddScoped<IProviderAdapter, Provider2Adapter>();
builder.Services.AddScoped<IProviderAdapterFactory, ProviderAdapterFactory>();

builder.Services.AddScoped<IValidator<Provider1EmployeeRequest>, Provider1EmployeeValidator>();
builder.Services.AddScoped<IValidator<Provider2EmployeeRequest>, Provider2EmployeeValidator>();
builder.Services.AddScoped<IValidator<MappedEmployee>, MappedEmployeeValidator>();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        await Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "An unexpected error occurred.",
                instance: context.Request.Path)
            .ExecuteAsync(context);
    });
});

app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;
    if (response.HasStarted)
    {
        return;
    }

    var statusCode = response.StatusCode;
    var title = statusCode == StatusCodes.Status404NotFound ? "Not Found" : "Request failed";

    await Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: $"The request returned status {statusCode}.",
            instance: statusCodeContext.HttpContext.Request.Path)
        .ExecuteAsync(statusCodeContext.HttpContext);
});

if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    await app.Services.MigrateDatabaseAsync();
}

app.MapOpenApi();
app.MapScalarApiReference();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
