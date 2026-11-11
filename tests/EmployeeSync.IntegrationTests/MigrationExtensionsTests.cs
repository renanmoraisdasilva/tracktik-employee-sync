using EmployeeSync.Api.Extensions;
using EmployeeSync.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace EmployeeSync.IntegrationTests;

public sealed class MigrationExtensionsTests
{
    [Fact]
    public async Task MigrateDatabaseAsync_applies_the_committed_migration()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connection)
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));

        using var provider = services.BuildServiceProvider();
        await provider.MigrateDatabaseAsync();

        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var applied = await dbContext.Database.GetAppliedMigrationsAsync();

        applied.ShouldContain(migration => migration.Contains("InitialCreate", StringComparison.Ordinal));
    }
}
