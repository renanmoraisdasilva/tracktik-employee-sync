namespace EmployeeSync.Infrastructure.Events;

public sealed class EventOptions
{
    public const string SectionName = "Events";

    public string Mode { get; set; } = "InMemory";
}
