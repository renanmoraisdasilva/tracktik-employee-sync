using System.Text.Json;
using EmployeeSync.Application.Providers.Provider1;
using EmployeeSync.Application.Providers.Provider2;
using EmployeeSync.Application.Validation;
using EmployeeSync.Domain.Common;
using EmployeeSync.Domain.Employees;
using Shouldly;

namespace EmployeeSync.UnitTests;

public sealed class ProviderValidationTests
{
    private static readonly Provider1Adapter Provider1 =
        new(new Provider1EmployeeValidator(), new MappedEmployeeValidator());

    private static readonly Provider2Adapter Provider2 =
        new(new Provider2EmployeeValidator(), new MappedEmployeeValidator());

    [Fact]
    public void TryParse_invalid_value_returns_false_and_unknown()
    {
        EmployeeStatusExtensions.TryParse("retired", out var status)
            .ShouldBeFalse();

        status.ShouldBe(EmployeeStatus.Unknown);
    }

    [Fact]
    public async Task Provider1_rejects_a_missing_email()
    {
        var result = await Provider1.MapAsync(Parse("""
            {
              "emp_id": "P1_010",
              "first_name": "No",
              "last_name": "Email",
              "employment_status": "active"
            }
            """), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.ValidationErrors.ShouldNotBeNull();
        result.Error.ValidationErrors!.Keys.ShouldContain("email_address");
    }

    [Fact]
    public async Task Provider1_rejects_a_missing_name()
    {
        var result = await Provider1.MapAsync(Parse("""
            {
              "emp_id": "P1_015",
              "last_name": "Only",
              "email_address": "no.name@provider1.com",
              "employment_status": "active"
            }
            """), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Provider1_rejects_an_invalid_email()
    {
        var result = await Provider1.MapAsync(Parse("""
            {
              "emp_id": "P1_016",
              "first_name": "Bad",
              "last_name": "Email",
              "email_address": "not-an-email",
              "employment_status": "active"
            }
            """), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.ValidationErrors!.Keys.ShouldContain("email_address");
    }

    [Fact]
    public async Task Provider1_rejects_an_invalid_date()
    {
        var result = await Provider1.MapAsync(Parse("""
            {
              "emp_id": "P1_011",
              "first_name": "Bad",
              "last_name": "Date",
              "email_address": "bad.date@provider1.com",
              "hire_date": "2024-13-40",
              "employment_status": "active"
            }
            """), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Provider1_rejects_an_unknown_status()
    {
        var result = await Provider1.MapAsync(Parse("""
            {
              "emp_id": "P1_012",
              "first_name": "Bad",
              "last_name": "Status",
              "email_address": "bad.status@provider1.com",
              "employment_status": "retired"
            }
            """), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Provider2_rejects_an_invalid_date()
    {
        var result = await Provider2.MapAsync(Parse("""
            {
              "employee_number": "P2_012",
              "personal_info": {
                "given_name": "Bad",
                "family_name": "Date",
                "email": "bad.date@provider2.com"
              },
              "work_info": {
                "start_date": "2024-13-40",
                "current_status": "employed"
              }
            }
            """), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.ValidationErrors!.Keys.ShouldContain("work_info.start_date");
    }

    [Fact]
    public async Task Provider2_rejects_an_unknown_status()
    {
        var result = await Provider2.MapAsync(Parse("""
            {
              "employee_number": "P2_013",
              "personal_info": {
                "given_name": "Bad",
                "family_name": "Status",
                "email": "bad.status@provider2.com"
              },
              "work_info": {
                "start_date": "2024-02-01",
                "current_status": "retired"
              }
            }
            """), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.ValidationErrors!.Keys.ShouldContain("work_info.current_status");
    }

    [Fact]
    public async Task Provider1_requires_status_even_though_the_schema_does_not()
    {
        var result = await Provider1.MapAsync(Parse("""
            {
              "emp_id": "P1_013",
              "first_name": "No",
              "last_name": "Status",
              "email_address": "no.status@provider1.com"
            }
            """), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.ValidationErrors!.Keys.ShouldContain("employment_status");
    }

    [Fact]
    public async Task Provider2_rejects_missing_nested_identity_fields()
    {
        var result = await Provider2.MapAsync(Parse("""
            {
              "employee_number": "P2_010",
              "personal_info": {
                "given_name": "No",
                "family_name": "Email"
              },
              "work_info": {
                "current_status": "employed"
              }
            }
            """), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.ValidationErrors!.Keys.ShouldContain("personal_info.email");
    }

    [Fact]
    public async Task Provider2_rejects_a_missing_personal_info_object()
    {
        var result = await Provider2.MapAsync(Parse("""
            {
              "employee_number": "P2_011",
              "work_info": {
                "current_status": "employed"
              }
            }
            """), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Extra_json_properties_are_ignored()
    {
        var result = await Provider1.MapAsync(Parse("""
            {
              "emp_id": "P1_014",
              "first_name": "Extra",
              "last_name": "Fields",
              "email_address": "extra.fields@provider1.com",
              "employment_status": "active",
              "unexpected": "ignored"
            }
            """), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement;
}
