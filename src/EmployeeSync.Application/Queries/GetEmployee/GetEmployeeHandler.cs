using EmployeeSync.Application.Abstractions;
using EmployeeSync.Application.Contracts;
using EmployeeSync.Application.Common;
using EmployeeSync.Domain.Common;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Queries.GetEmployee;

public sealed class GetEmployeeHandler : IQueryHandler<GetEmployeeQuery, Result<EmployeeResponse>>
{
    private readonly IEmployeeRepository _repository;
    private readonly IProviderAdapterFactory _adapterFactory;

    public GetEmployeeHandler(IEmployeeRepository repository, IProviderAdapterFactory adapterFactory)
    {
        _repository = repository;
        _adapterFactory = adapterFactory;
    }

    public async Task<Result<EmployeeResponse>> HandleAsync(
        GetEmployeeQuery query,
        CancellationToken cancellationToken)
    {
        var provider = ProviderNames.Normalize(query.Provider);
        if (_adapterFactory.Get(provider) is null)
        {
            return Result<EmployeeResponse>.Failure(
                Error.NotFound($"Unknown provider '{query.Provider}'."));
        }

        var identity = new EmployeeIdentity(provider, query.ProviderEmployeeId);
        var employee = await _repository.FindAsync(identity, cancellationToken);

        return employee is null
            ? Result<EmployeeResponse>.Failure(
                Error.NotFound(
                    $"No employee exists for provider '{provider}' with provider employee id '{query.ProviderEmployeeId}'."))
            : Result<EmployeeResponse>.Success(EmployeeResponse.From(employee));
    }
}
