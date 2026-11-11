using EmployeeSync.Application.Abstractions;
using EmployeeSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EmployeeSync.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory()
    {
        Environment.SetEnvironmentVariable("Database__UseSqlite", "true");
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

            _connection.Open();

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            services.AddSingleton<ITrackTikClient>(TrackTik);
            services.AddSingleton<IEventPublisher>(Events);
        });
    }

    public void InitializeDatabase()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
