using EmployeeSync.Application.Common;
using EmployeeSync.Domain.Employees;
using FluentValidation;

namespace EmployeeSync.Application.Providers.Provider1;

public sealed class Provider1Adapter : ProviderAdapterBase<Provider1EmployeeRequest>
{
    public Provider1Adapter(
        IValidator<Provider1EmployeeRequest> validator,
        IValidator<MappedEmployee> mappedValidator)
        : base(ProviderNames.Provider1, validator, mappedValidator, Provider1Mapper.Map)
    {
    }
}
