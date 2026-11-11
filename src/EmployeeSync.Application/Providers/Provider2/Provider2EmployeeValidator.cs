using System.Globalization;
using EmployeeSync.Domain.Employees;
using FluentValidation;

namespace EmployeeSync.Application.Providers.Provider2;

public sealed class Provider2EmployeeValidator : AbstractValidator<Provider2EmployeeRequest>
{
    public Provider2EmployeeValidator()
    {
        RuleFor(x => x.EmployeeNumber).NotEmpty().MaximumLength(128);
        RuleFor(x => x.PersonalInfo!).NotNull().SetValidator(new PersonalInfoValidator());
        RuleFor(x => x.WorkInfo!).NotNull().SetValidator(new WorkInfoValidator());
    }
}

public sealed class PersonalInfoValidator : AbstractValidator<PersonalInfo>
{
    public PersonalInfoValidator()
    {
        RuleFor(x => x.GivenName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FamilyName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Mobile).MaximumLength(40);
    }
}

public sealed class WorkInfoValidator : AbstractValidator<WorkInfo>
{
    public WorkInfoValidator()
    {
        RuleFor(x => x.Role).MaximumLength(120);
        RuleFor(x => x.Division).MaximumLength(120);
        RuleFor(x => x.StartDate)
            .Must(BeValidDate)
            .When(x => !string.IsNullOrWhiteSpace(x.StartDate))
            .WithMessage("start_date must be a valid date in yyyy-MM-dd format.");
        RuleFor(x => x.CurrentStatus)
            .NotEmpty()
            .Must(BeValidStatus)
            .WithMessage("current_status must be employed, on_leave or terminated.");
    }

    private static bool BeValidDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool BeValidStatus(string? value) =>
        value?.Trim().ToLowerInvariant() is "employed" or "on_leave" or "terminated";
}
