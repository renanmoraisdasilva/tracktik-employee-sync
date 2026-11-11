namespace EmployeeSync.Infrastructure.Events;

public sealed class NatsOptions
{
    public const string SectionName = "Nats";

    public string Url { get; set; } = "nats://localhost:4222";

    public string Subject { get; set; } = "tracktik.employees.synced";
}
