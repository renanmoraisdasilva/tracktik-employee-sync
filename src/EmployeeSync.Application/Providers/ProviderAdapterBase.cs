using System.Text.Json;
using EmployeeSync.Application.Abstractions;
using EmployeeSync.Domain.Common;
using EmployeeSync.Domain.Employees;
using FluentValidation;
using FluentValidation.Results;

namespace EmployeeSync.Application.Providers;

public abstract class ProviderAdapterBase<TRequest> : IProviderAdapter
    where TRequest : class
{
    private readonly IValidator<TRequest> _validator;
    private readonly IValidator<MappedEmployee> _mappedValidator;
    private readonly Func<TRequest, MappedEmployee> _mapper;

    protected ProviderAdapterBase(
        string name,
        IValidator<TRequest> validator,
        IValidator<MappedEmployee> mappedValidator,
        Func<TRequest, MappedEmployee> mapper)
    {
        Name = name;
        _validator = validator;
        _mappedValidator = mappedValidator;
        _mapper = mapper;
    }

    public string Name { get; }

    public async Task<Result<MappedEmployee>> MapAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        TRequest? request;

        try
        {
            request = payload.Deserialize<TRequest>(PayloadJson.Options);
        }
        catch (JsonException)
        {
            return Result<MappedEmployee>.Failure(
                Error.Validation("payload", "The JSON payload is invalid."));
        }

        if (request is null)
        {
            return Result<MappedEmployee>.Failure(
                Error.Validation("payload", "The JSON payload is required."));
        }

        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<MappedEmployee>.Failure(ToError(validation.Errors));
        }

        var mapped = _mapper(request);
        var mappedValidation = await _mappedValidator.ValidateAsync(mapped, cancellationToken);
        if (!mappedValidation.IsValid)
        {
            return Result<MappedEmployee>.Failure(ToError(mappedValidation.Errors));
        }

        return Result<MappedEmployee>.Success(mapped);
    }

    private static Error ToError(IEnumerable<ValidationFailure> failures)
    {
        var errors = failures
            .GroupBy(failure => string.IsNullOrWhiteSpace(failure.PropertyName)
                ? "payload"
                : failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray());

        return Error.Validation(errors);
    }
}
