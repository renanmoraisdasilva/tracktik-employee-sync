namespace EmployeeSync.Domain.Employees;

public sealed class Employee
{
    private Employee()
    {
    }

    public Guid Id { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string ProviderEmployeeId { get; private set; } = string.Empty;

    public string TrackTikEmployeeId { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? PhoneNumber { get; private set; }

    public string? Position { get; private set; }

    public string? Department { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public EmployeeStatus Status { get; private set; }

    public SyncStatus SyncStatus { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? LastAttemptAt { get; private set; }

    public bool ExistsInTrackTik { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? SyncedAt { get; private set; }

    public EmployeeIdentity Identity => new(Provider, ProviderEmployeeId);

    public static Employee Create(EmployeeIdentity identity, MappedEmployee mapped, DateTimeOffset now)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            Provider = identity.Provider,
            ProviderEmployeeId = identity.ProviderEmployeeId,
            TrackTikEmployeeId = identity.TrackTikEmployeeId,
            SyncStatus = SyncStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        employee.Apply(mapped, now);
        return employee;
    }

    public void UpdateFrom(MappedEmployee mapped, DateTimeOffset now) => Apply(mapped, now);

    public void MarkPending(DateTimeOffset now)
    {
        SyncStatus = SyncStatus.Pending;
        UpdatedAt = now;
    }

    public void BeginAttempt(DateTimeOffset now)
    {
        Attempts++;
        LastAttemptAt = now;
        UpdatedAt = now;
    }

    public void MarkSynced(DateTimeOffset now)
    {
        SyncStatus = SyncStatus.Synced;
        ExistsInTrackTik = true;
        LastError = null;
        SyncedAt = now;
        UpdatedAt = now;
    }

    public void MarkFailed(DateTimeOffset now, string error)
    {
        SyncStatus = SyncStatus.Failed;
        LastError = error;
        UpdatedAt = now;
    }

    public void SetExistsInTrackTik(bool value) => ExistsInTrackTik = value;

    private void Apply(MappedEmployee mapped, DateTimeOffset now)
    {
        FirstName = mapped.FirstName;
        LastName = mapped.LastName;
        Email = mapped.Email;
        PhoneNumber = mapped.PhoneNumber;
        Position = mapped.Position;
        Department = mapped.Department;
        StartDate = mapped.StartDate;
        Status = mapped.Status;
        UpdatedAt = now;
    }
}
