using EmployeeSync.Application.Common;
using EmployeeSync.Domain.Employees;
using FluentValidation;

namespace EmployeeSync.Application.Providers.Provider1;

public sealed class Provider1Adapter : ProviderAdapterBase<Provider1EmployeeRequest>
{
    private static readonly IReadOnlyDictionary<string, string> MappedPropertyNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ProviderEmployeeId"] = "emp_id",
            ["FirstName"] = "first_name",
            ["LastName"] = "last_name",
            ["Email"] = "email_address",
            ["PhoneNumber"] = "phone",
            ["Position"] = "job_title",
            ["Department"] = "dept",
            ["StartDate"] = "hire_date",
            ["Status"] = "employment_status"
        };

    public Provider1Adapter(
        IValidator<Provider1EmployeeRequest> validator,
        IValidator<MappedEmployee> mappedValidator)
        : base(
            ProviderNames.Provider1,
            validator,
            mappedValidator,
            Provider1Mapper.Map,
            MappedPropertyNames)
    {
    }
}
