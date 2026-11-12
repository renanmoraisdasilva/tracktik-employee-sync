using EmployeeSync.Application.Common;
using EmployeeSync.Domain.Employees;
using FluentValidation;

namespace EmployeeSync.Application.Providers.Provider2;

public sealed class Provider2Adapter : ProviderAdapterBase<Provider2EmployeeRequest>
{
    private static readonly IReadOnlyDictionary<string, string> MappedPropertyNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ProviderEmployeeId"] = "employee_number",
            ["FirstName"] = "personal_info.given_name",
            ["LastName"] = "personal_info.family_name",
            ["Email"] = "personal_info.email",
            ["PhoneNumber"] = "personal_info.mobile",
            ["Position"] = "work_info.role",
            ["Department"] = "work_info.division",
            ["StartDate"] = "work_info.start_date",
            ["Status"] = "work_info.current_status"
        };

    public Provider2Adapter(
        IValidator<Provider2EmployeeRequest> validator,
        IValidator<MappedEmployee> mappedValidator)
        : base(
            ProviderNames.Provider2,
            validator,
            mappedValidator,
            Provider2Mapper.Map,
            MappedPropertyNames)
    {
    }
}
