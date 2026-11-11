using System.Net;
using System.Net.Http.Json;
using EmployeeSync.Application.Contracts;
using EmployeeSync.Domain.Employees;
using EmployeeSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.MsSql;

namespace EmployeeSync.Testcontainers;

[Trait("Category", "Testcontainers")]
public sealed class SqlServerIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithDatabase("EmployeeSync")
        .Build();

    private SqlServerApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _factory = new SqlServerApiFactory(_container.GetConnectionString());
        _client = _factory.CreateClient();
        _factory.MigrateDatabase();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Post_persists_and_syncs_against_real_sql_server()
    {
        const string id = "TC_001";

        var response = await _client.PostAsync(
            "/api/v1/providers/provider1/employees",
            JsonContent.Create(new Dictionary<string, object?>
            {
                ["emp_id"] = id,
                ["first_name"] = "Real",
                ["last_name"] = "SqlServer",
                ["email_address"] = "real.sqlserver@provider1.com",
                ["employment_status"] = "active"
            }));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.ShouldNotBeNull();
        body.EmployeeId.ShouldBe("provider1-TC_001");
        body.SyncStatus.ShouldBe("synced");

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.Employees.SingleAsync(
            employee => employee.Provider == "provider1"
                && employee.ProviderEmployeeId == id);

        stored.SyncStatus.ShouldBe(SyncStatus.Synced);
        stored.Attempts.ShouldBe(1);
        stored.ExistsInTrackTik.ShouldBeTrue();

        _factory.Events.Events.ShouldContain(
            domainEvent => domainEvent.ProviderEmployeeId == id);
    }
}
