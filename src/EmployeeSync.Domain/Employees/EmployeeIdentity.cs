namespace EmployeeSync.Domain.Employees;

public sealed record EmployeeIdentity(string Provider, string ProviderEmployeeId)
{
    public string TrackTikEmployeeId => $"{Provider}-{ProviderEmployeeId}";
}
