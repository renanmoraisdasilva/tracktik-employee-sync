using EmployeeSync.Application.Abstractions;
using EmployeeSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EmployeeSync.Testcontainers;

public sealed class SqlServerApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public SqlServerApiFactory(string connectionString)
    {
        _connectionString = connectionString;
        Environment.SetEnvironmentVariable("Database__UseSqlite", "false");
    }

    public FakeTrackTikClient TrackTik { get; } = new();

    public FakeEventPublisher Events { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<ITrackTikClient>();
            services.RemoveAll<IEventPublisher>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(_connectionString, sql => sql.EnableRetryOnFailure()));
            services.AddSingleton<ITrackTikClient>(TrackTik);
            services.AddSingleton<IEventPublisher>(Events);
        });
    }

    public void MigrateDatabase()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    }
}
