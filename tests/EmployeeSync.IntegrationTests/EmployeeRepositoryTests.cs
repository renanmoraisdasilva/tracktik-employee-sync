using EmployeeSync.Domain.Employees;
using EmployeeSync.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace EmployeeSync.IntegrationTests;

public sealed class EmployeeRepositoryTests
{
    private static readonly MappedEmployee Mapped = new(
        "P1_001",
        "Alice",
        "Johnson",
        "alice.johnson@provider1.com",
        "+1-555-0101",
        "Security Officer",
        "Security Operations",
        new DateOnly(2024, 1, 15),
        EmployeeStatus.Active);

    [Fact]
    public async Task TryAdd_persists_and_rejects_a_duplicate_identity()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var repository = new EmployeeRepository(dbContext);
        var identity = new EmployeeIdentity("provider1", "P1_001");

        var first = Employee.Create(identity, Mapped, DateTimeOffset.UtcNow);
        (await repository.TryAddAsync(first, CancellationToken.None)).ShouldBeTrue();

        var duplicate = Employee.Create(identity, Mapped, DateTimeOffset.UtcNow);
        (await repository.TryAddAsync(duplicate, CancellationToken.None)).ShouldBeFalse();

        var stored = await repository.FindAsync(identity, CancellationToken.None);
        stored.ShouldNotBeNull();
        stored!.Id.ShouldBe(first.Id);

        var missing = await repository.FindAsync(
            new EmployeeIdentity("provider1", "MISSING"),
            CancellationToken.None);
        missing.ShouldBeNull();
    }

    [Fact]
    public async Task SaveChanges_persists_the_sync_state()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var repository = new EmployeeRepository(dbContext);
        var identity = new EmployeeIdentity("provider1", "P1_002");
        var employee = Employee.Create(identity, Mapped, DateTimeOffset.UtcNow);

        (await repository.TryAddAsync(employee, CancellationToken.None)).ShouldBeTrue();
        employee.BeginAttempt(DateTimeOffset.UtcNow);
        employee.MarkSynced(DateTimeOffset.UtcNow);
        await repository.SaveChangesAsync(CancellationToken.None);

        using var readScope = provider.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await readContext.Employees.SingleAsync(
            candidate => candidate.Provider == identity.Provider
                && candidate.ProviderEmployeeId == identity.ProviderEmployeeId);

        stored.SyncStatus.ShouldBe(SyncStatus.Synced);
        stored.Attempts.ShouldBe(1);
        stored.ExistsInTrackTik.ShouldBeTrue();
        stored.LastError.ShouldBeNull();
    }
}
