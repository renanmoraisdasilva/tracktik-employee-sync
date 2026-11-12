using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private readonly IReadOnlyDictionary<string, string> _mappedPropertyNames;

    protected ProviderAdapterBase(
        string name,
        IValidator<TRequest> validator,
        IValidator<MappedEmployee> mappedValidator,
        Func<TRequest, MappedEmployee> mapper,
        IReadOnlyDictionary<string, string>? mappedPropertyNames = null)
    {
        Name = name;
        _validator = validator;
        _mappedValidator = mappedValidator;
        _mapper = mapper;
        _mappedPropertyNames = mappedPropertyNames
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
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

    private Error ToError(IEnumerable<ValidationFailure> failures)
    {
        var errors = failures
            .GroupBy(failure => MapPropertyName(failure.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray());

        return Error.Validation(errors);
    }

    private string MapPropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return "payload";
        }

        if (_mappedPropertyNames.TryGetValue(propertyName, out var mappedName))
        {
            return mappedName;
        }

        return JsonPropertyPath(typeof(TRequest), propertyName);
    }

    private static string JsonPropertyPath(Type rootType, string propertyPath)
    {
        var segments = propertyPath.Split('.');
        var names = new string[segments.Length];
        Type? currentType = rootType;

        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            var property = currentType?.GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property is null)
            {
                names[index] = segment;
                currentType = null;
                continue;
            }

            names[index] = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? segment;
            currentType = Nullable.GetUnderlyingType(property.PropertyType)
                ?? property.PropertyType;
        }

        return string.Join('.', names);
    }
}
