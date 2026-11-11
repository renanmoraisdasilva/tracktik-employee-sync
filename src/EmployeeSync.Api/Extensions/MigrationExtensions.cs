using EmployeeSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmployeeSync.Api.Extensions;

public static class MigrationExtensions
{
    private const int MaxAttempts = 10;

    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseMigration");

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied.");
                return;
            }
            catch (Exception exception) when (attempt < MaxAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Database migration attempt {Attempt} failed; retrying.",
                    attempt);

                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }
}
