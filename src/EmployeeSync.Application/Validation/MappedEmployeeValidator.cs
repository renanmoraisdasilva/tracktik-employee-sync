using EmployeeSync.Domain.Employees;
using FluentValidation;

namespace EmployeeSync.Application.Validation;

public sealed class MappedEmployeeValidator : AbstractValidator<MappedEmployee>
{
    public MappedEmployeeValidator()
    {
        RuleFor(x => x.ProviderEmployeeId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.PhoneNumber).MaximumLength(40);
        RuleFor(x => x.Position).MaximumLength(120);
        RuleFor(x => x.Department).MaximumLength(120);
        RuleFor(x => x.Status)
            .IsInEnum()
            .NotEqual(EmployeeStatus.Unknown);
    }
}
