using System.Text.Json.Serialization;

namespace EmployeeSync.Application.Providers.Provider2;

public sealed class Provider2EmployeeRequest
{
    [JsonPropertyName("employee_number")]
    public string? EmployeeNumber { get; set; }

    [JsonPropertyName("personal_info")]
    public PersonalInfo? PersonalInfo { get; set; }

    [JsonPropertyName("work_info")]
    public WorkInfo? WorkInfo { get; set; }
}

public sealed class PersonalInfo
{
    [JsonPropertyName("given_name")]
    public string? GivenName { get; set; }

    [JsonPropertyName("family_name")]
    public string? FamilyName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }
}

public sealed class WorkInfo
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("division")]
    public string? Division { get; set; }

    [JsonPropertyName("start_date")]
    public string? StartDate { get; set; }

    [JsonPropertyName("current_status")]
    public string? CurrentStatus { get; set; }
}
