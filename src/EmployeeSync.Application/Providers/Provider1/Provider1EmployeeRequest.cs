using System.Text.Json.Serialization;

namespace EmployeeSync.Application.Providers.Provider1;

public sealed class Provider1EmployeeRequest
{
    [JsonPropertyName("emp_id")]
    public string? EmpId { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("email_address")]
    public string? EmailAddress { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("job_title")]
    public string? JobTitle { get; set; }

    [JsonPropertyName("dept")]
    public string? Department { get; set; }

    [JsonPropertyName("hire_date")]
    public string? HireDate { get; set; }

    [JsonPropertyName("employment_status")]
    public string? EmploymentStatus { get; set; }
}
