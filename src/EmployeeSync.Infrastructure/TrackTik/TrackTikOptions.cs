namespace EmployeeSync.Infrastructure.TrackTik;

public sealed class TrackTikOptions
{
    public const string SectionName = "TrackTik";

    public string BaseUrl { get; set; } = string.Empty;

    public string TokenUrl { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string? Scope { get; set; }

    public int TimeoutSeconds { get; set; } = 15;
}
