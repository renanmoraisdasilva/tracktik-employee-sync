using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Contracts;

public static class EmployeeMappings
{
    public static TrackTikEmployee ToTrackTikEmployee(this Employee employee) =>
        new(
            employee.TrackTikEmployeeId,
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.PhoneNumber,
            employee.Position,
            employee.Department,
            employee.StartDate,
            employee.Status.ToApiString());
}
