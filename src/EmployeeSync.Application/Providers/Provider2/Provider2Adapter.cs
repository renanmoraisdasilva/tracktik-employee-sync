using EmployeeSync.Application.Common;
using EmployeeSync.Domain.Employees;
using FluentValidation;

namespace EmployeeSync.Application.Providers.Provider2;

public sealed class Provider2Adapter : ProviderAdapterBase<Provider2EmployeeRequest>
{
    public Provider2Adapter(
        IValidator<Provider2EmployeeRequest> validator,
        IValidator<MappedEmployee> mappedValidator)
        : base(ProviderNames.Provider2, validator, mappedValidator, Provider2Mapper.Map)
    {
    }
}
