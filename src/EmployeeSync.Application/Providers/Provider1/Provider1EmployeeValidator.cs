using System.Globalization;
using EmployeeSync.Domain.Employees;
using FluentValidation;

namespace EmployeeSync.Application.Providers.Provider1;

public sealed class Provider1EmployeeValidator : AbstractValidator<Provider1EmployeeRequest>
{
    public Provider1EmployeeValidator()
    {
        RuleFor(x => x.EmpId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EmailAddress).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.JobTitle).MaximumLength(120);
        RuleFor(x => x.Department).MaximumLength(120);
        RuleFor(x => x.HireDate)
            .Must(BeValidDate)
            .When(x => !string.IsNullOrWhiteSpace(x.HireDate))
            .WithMessage("hire_date must be a valid date in yyyy-MM-dd format.");
        RuleFor(x => x.EmploymentStatus)
            .NotEmpty()
            .Must(BeValidStatus)
            .WithMessage("employment_status must be active, inactive or terminated.");
    }

    private static bool BeValidDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool BeValidStatus(string? value) =>
        EmployeeStatusExtensions.TryParse(value, out _);
}
