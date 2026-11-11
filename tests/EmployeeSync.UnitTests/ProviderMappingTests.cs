using System.Text.Json;
using EmployeeSync.Application.Providers.Provider1;
using EmployeeSync.Application.Providers.Provider2;
using EmployeeSync.Application.Validation;
using EmployeeSync.Domain.Employees;
using Shouldly;

namespace EmployeeSync.UnitTests;

public sealed class ProviderMappingTests
{
    private static readonly Provider1Adapter Provider1 =
        new(new Provider1EmployeeValidator(), new MappedEmployeeValidator());

    private static readonly Provider2Adapter Provider2 =
        new(new Provider2EmployeeValidator(), new MappedEmployeeValidator());

    [Fact]
    public async Task Provider1_maps_the_sample_employee()
    {
        var result = await Provider1.MapAsync(Parse("""
            {
              "emp_id": "P1_001",
              "first_name": "Alice",
              "last_name": "Johnson",
              "email_address": "alice.johnson@provider1.com",
              "phone": "+1-555-0101",
              "job_title": "Security Officer",
              "dept": "Security Operations",
              "hire_date": "2024-01-15",
              "employment_status": "active"
            }
            """), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var employee = result.Value;
        employee.ProviderEmployeeId.ShouldBe("P1_001");
        employee.FirstName.ShouldBe("Alice");
        employee.LastName.ShouldBe("Johnson");
        employee.Email.ShouldBe("alice.johnson@provider1.com");
        employee.PhoneNumber.ShouldBe("+1-555-0101");
        employee.Position.ShouldBe("Security Officer");
        employee.Department.ShouldBe("Security Operations");
        employee.StartDate.ShouldBe(new DateOnly(2024, 1, 15));
        employee.Status.ShouldBe(EmployeeStatus.Active);
    }

    [Fact]
    public async Task Provider2_maps_the_nested_sample_employee()
    {
        var result = await Provider2.MapAsync(Parse("""
            {
              "employee_number": "P2_001",
              "personal_info": {
                "given_name": "Carol",
                "family_name": "Davis",
                "email": "carol.davis@provider2.com",
                "mobile": "+1-555-0201"
              },
              "work_info": {
                "role": "Security Guard",
                "division": "Night Shift Security",
                "start_date": "2024-02-01",
                "current_status": "employed"
              }
            }
            """), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var employee = result.Value;
        employee.ProviderEmployeeId.ShouldBe("P2_001");
        employee.FirstName.ShouldBe("Carol");
        employee.LastName.ShouldBe("Davis");
        employee.Email.ShouldBe("carol.davis@provider2.com");
        employee.PhoneNumber.ShouldBe("+1-555-0201");
        employee.Position.ShouldBe("Security Guard");
        employee.Department.ShouldBe("Night Shift Security");
        employee.StartDate.ShouldBe(new DateOnly(2024, 2, 1));
        employee.Status.ShouldBe(EmployeeStatus.Active);
    }

    [Fact]
    public async Task Provider2_maps_on_leave_to_inactive()
    {
        var result = await Provider2.MapAsync(Parse("""
            {
              "employee_number": "P2_002",
              "personal_info": {
                "given_name": "David",
                "family_name": "Wilson",
                "email": "david.wilson@provider2.com"
              },
              "work_info": {
                "current_status": "on_leave"
              }
            }
            """), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(EmployeeStatus.Inactive);
    }

    [Fact]
    public async Task Provider2_maps_terminated_to_terminated()
    {
        var result = await Provider2.MapAsync(Parse("""
            {
              "employee_number": "P2_003",
              "personal_info": {
                "given_name": "Eve",
                "family_name": "Taylor",
                "email": "eve.taylor@provider2.com"
              },
              "work_info": {
                "current_status": "terminated"
              }
            }
            """), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(EmployeeStatus.Terminated);
    }

    [Fact]
    public async Task Provider1_keeps_optional_fields_optional()
    {
        var result = await Provider1.MapAsync(Parse("""
            {
              "emp_id": "P1_003",
              "first_name": "Minimal",
              "last_name": "Employee",
              "email_address": "minimal@provider1.com",
              "employment_status": "terminated"
            }
            """), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PhoneNumber.ShouldBeNull();
        result.Value.Position.ShouldBeNull();
        result.Value.Department.ShouldBeNull();
        result.Value.StartDate.ShouldBeNull();
        result.Value.Status.ShouldBe(EmployeeStatus.Terminated);
    }

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement;
}
