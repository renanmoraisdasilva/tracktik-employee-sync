using EmployeeSync.Application.Contracts;

namespace EmployeeSync.Application.Commands.SubmitEmployee;

public sealed record SubmitEmployeeResult(EmployeeResponse Employee, bool Created);
