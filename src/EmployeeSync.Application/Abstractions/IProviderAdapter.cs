using System.Text.Json;
using EmployeeSync.Domain.Common;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Abstractions;

public interface IProviderAdapter
{
    string Name { get; }

    Task<Result<MappedEmployee>> MapAsync(JsonElement payload, CancellationToken cancellationToken);
}
