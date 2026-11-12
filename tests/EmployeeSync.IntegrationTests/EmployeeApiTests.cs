using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EmployeeSync.Application.Contracts;
using EmployeeSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace EmployeeSync.IntegrationTests;

public sealed class EmployeeApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public EmployeeApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        factory.InitializeDatabase();
    }

    [Fact]
    public async Task First_insert_returns_201_and_a_synced_employee()
    {
        const string id = "IT_001";

        var response = await _client.PostAsync(
            "/api/v1/providers/provider1/employees",
            Provider1Payload(id));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.ShouldNotBeNull();
        body.EmployeeId.ShouldBe("provider1-IT_001");
        body.SyncStatus.ShouldBe("synced");
        body.Attempts.ShouldBe(1);
        body.LastError.ShouldBeNull();

        _factory.TrackTik.Created.ShouldContain(employee => employee.EmployeeId == "provider1-IT_001");
        _factory.Events.Events.ShouldContain(domainEvent => domainEvent.ProviderEmployeeId == id);
        CountRows("provider1", id).ShouldBe(1);

        var stored = await _client.GetAsync($"/api/v1/employees/provider1/{id}");
        stored.StatusCode.ShouldBe(HttpStatusCode.OK);

        var storedBody = await stored.Content.ReadFromJsonAsync<EmployeeResponse>();
        storedBody.ShouldNotBeNull();
        storedBody.EmployeeId.ShouldBe("provider1-IT_001");
        storedBody.SyncStatus.ShouldBe("synced");
    }

    [Fact]
    public async Task Resubmission_returns_200_uses_put_and_keeps_one_row()
    {
        const string id = "IT_002";

        var first = await _client.PostAsync(
            "/api/v1/providers/provider1/employees",
            Provider1Payload(id));
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await _client.PostAsync(
            "/api/v1/providers/provider1/employees",
            Provider1Payload(id, jobTitle: "Senior Officer"));
        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await second.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.ShouldNotBeNull();
        body.Attempts.ShouldBe(2);
        body.SyncStatus.ShouldBe("synced");

        _factory.TrackTik.Created.Count(employee => employee.EmployeeId == "provider1-IT_002").ShouldBe(1);
        _factory.TrackTik.Updated.Count(employee => employee.EmployeeId == "provider1-IT_002").ShouldBe(1);
        CountRows("provider1", id).ShouldBe(1);
        _factory.Events.Events.Count(
            domainEvent => domainEvent.ProviderEmployeeId == id).ShouldBe(2);
    }

    [Fact]
    public async Task Resubmit_after_failure_returns_200_uses_post_and_keeps_one_row()
    {
        const string id = "IT_006";
        const string employeeId = "provider1-IT_006";
        _factory.TrackTik.FailEmployeeIds.Add(employeeId);

        var failed = await _client.PostAsync(
            "/api/v1/providers/provider1/employees",
            Provider1Payload(id));

        failed.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        CountRows("provider1", id).ShouldBe(1);
        _factory.Events.Events.ShouldNotContain(
            domainEvent => domainEvent.ProviderEmployeeId == id);

        _factory.TrackTik.FailEmployeeIds.Remove(employeeId);

        var retry = await _client.PostAsync(
            "/api/v1/providers/provider1/employees",
            Provider1Payload(id));

        retry.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await retry.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.ShouldNotBeNull();
        body.SyncStatus.ShouldBe("synced");
        body.Attempts.ShouldBe(2);

        _factory.TrackTik.Created.Count(
            employee => employee.EmployeeId == employeeId).ShouldBe(1);
        _factory.TrackTik.Updated.ShouldNotContain(
            employee => employee.EmployeeId == employeeId);
        CountRows("provider1", id).ShouldBe(1);
        _factory.Events.Events.Count(
            domainEvent => domainEvent.ProviderEmployeeId == id).ShouldBe(1);
    }

    [Fact]
    public async Task Same_email_from_two_providers_creates_two_employees()
    {
        const string email = "shared.identity@example.com";

        var provider1 = await _client.PostAsync(
            "/api/v1/providers/provider1/employees",
            Provider1Payload("IT_020", email: email));

        var provider2 = await _client.PostAsync(
            "/api/v1/providers/provider2/employees",
            Provider2Payload("IT_021", email: email));

        provider1.StatusCode.ShouldBe(HttpStatusCode.Created);
        provider2.StatusCode.ShouldBe(HttpStatusCode.Created);

        var first = await provider1.Content.ReadFromJsonAsync<EmployeeResponse>();
        var second = await provider2.Content.ReadFromJsonAsync<EmployeeResponse>();

        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        first.EmployeeId.ShouldBe("provider1-IT_020");
        second.EmployeeId.ShouldBe("provider2-IT_021");
        first.Email.ShouldBe(email);
        second.Email.ShouldBe(email);
        CountRows("provider1", "IT_020").ShouldBe(1);
        CountRows("provider2", "IT_021").ShouldBe(1);
    }

    [Fact]
    public async Task Provider_employee_ids_are_case_sensitive()
    {
        const string lower = "IT_case";
        const string upper = "IT_CASE";

        var first = await _client.PostAsync(
            "/api/v1/providers/provider1/employees",
            Provider1Payload(lower));
        var second = await _client.PostAsync(
            "/api/v1/providers/provider1/employees",
            Provider1Payload(upper));

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        CountRows("provider1", lower).ShouldBe(1);
        CountRows("provider1", upper).ShouldBe(1);
    }

    [Fact]
    public async Task TrackTik_failure_returns_502_keeps_failed_row_and_emits_no_event()
    {
        const string id = "IT_003";
        const string employeeId = "provider1-IT_003";
        _factory.TrackTik.FailEmployeeIds.Add(employeeId);

        var response = await _client.PostAsync(
            "/api/v1/providers/provider1/employees",
            Provider1Payload(id));

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        problem.GetProperty("syncStatus").GetString().ShouldBe("failed");
        problem.GetProperty("attempts").GetInt32().ShouldBe(1);

        var getResponse = await _client.GetAsync(
            $"/api/v1/employees/provider1/{id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stored = await getResponse.Content.ReadFromJsonAsync<EmployeeResponse>();
        stored.ShouldNotBeNull();
        stored.SyncStatus.ShouldBe("failed");
        stored.Attempts.ShouldBe(1);

        CountRows("provider1", id).ShouldBe(1);
        _factory.Events.Events.ShouldNotContain(
            domainEvent => domainEvent.ProviderEmployeeId == id);
    }

    [Fact]
    public async Task Validation_failure_returns_400_and_writes_nothing()
    {
        const string id = "IT_004";

        var payload = new Dictionary<string, object?>
        {
            ["emp_id"] = id,
            ["first_name"] = "No",
            ["last_name"] = "Email",
            ["employment_status"] = "active"
        };

        var response = await _client.PostAsync(
            "/api/v1/providers/provider1/employees",
            JsonContent.Create(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()).RootElement;
        problem.GetProperty("errors").TryGetProperty("email_address", out _)
            .ShouldBeTrue();
        problem.GetProperty("errors").TryGetProperty("EmailAddress", out _)
            .ShouldBeFalse();

        CountRows("provider1", id).ShouldBe(0);
        _factory.TrackTik.Created.ShouldNotContain(
            employee => employee.EmployeeId == "provider1-IT_004");
    }

    [Fact]
    public async Task Unknown_provider_returns_404_and_writes_nothing()
    {
        var response = await _client.PostAsync(
            "/api/v1/providers/provider3/employees",
            Provider1Payload("IT_005"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        CountRows("provider3", "IT_005").ShouldBe(0);
    }

    [Fact]
    public async Task Get_unknown_provider_returns_404_problem_details()
    {
        var response = await _client.GetAsync("/api/v1/employees/provider3/UNKNOWN");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Malformed_json_returns_400_problem_details()
    {
        var response = await _client.PostAsync(
            "/api/v1/providers/provider1/employees",
            new StringContent("{", Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Unknown_route_returns_404_problem_details()
    {
        var response = await _client.GetAsync("/api/v1/does-not-exist");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Get_missing_employee_returns_404()
    {
        var response = await _client.GetAsync("/api/v1/employees/provider1/IT_404");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Provider2_payload_maps_and_syncs()
    {
        const string id = "IT_010";

        var response = await _client.PostAsync(
            "/api/v1/providers/provider2/employees",
            Provider2Payload(id));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.ShouldNotBeNull();
        body.EmployeeId.ShouldBe("provider2-IT_010");
        body.FirstName.ShouldBe("Carol");
        body.LastName.ShouldBe("Davis");
        body.Position.ShouldBe("Security Guard");
        body.Status.ShouldBe("active");

        CountRows("provider2", id).ShouldBe(1);
    }

    [Fact]
    public async Task OpenApi_document_is_available()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("/api/v1/providers/{provider}/employees");
        content.ShouldContain("/api/v1/employees/{provider}/{providerEmployeeId}");
    }

    [Fact]
    public async Task Report_returns_counts_for_each_status()
    {
        var response = await _client.GetAsync("/api/v1/reports/employees-by-sync-status");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var counts = await response.Content.ReadFromJsonAsync<List<SyncStatusCount>>();
        counts.ShouldNotBeNull();
        counts.Count.ShouldBe(3);
        counts.Select(count => count.SyncStatus).ShouldContain("pending");
        counts.Select(count => count.SyncStatus).ShouldContain("synced");
        counts.Select(count => count.SyncStatus).ShouldContain("failed");
    }

    private int CountRows(string provider, string providerEmployeeId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return dbContext.Employees.Count(
            employee => employee.Provider == provider
                && employee.ProviderEmployeeId == providerEmployeeId);
    }

    private static JsonContent Provider1Payload(
        string id,
        string? jobTitle = null,
        string? email = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["emp_id"] = id,
            ["first_name"] = "Test",
            ["last_name"] = "Employee",
            ["email_address"] = email ?? $"{id.ToLowerInvariant()}@provider1.com",
            ["employment_status"] = "active"
        };

        if (jobTitle is not null)
        {
            payload["job_title"] = jobTitle;
        }

        return JsonContent.Create(payload);
    }

    private static JsonContent Provider2Payload(string id, string? email = null) =>
        JsonContent.Create(new Dictionary<string, object?>
        {
            ["employee_number"] = id,
            ["personal_info"] = new Dictionary<string, object?>
            {
                ["given_name"] = "Carol",
                ["family_name"] = "Davis",
                ["email"] = email ?? $"{id.ToLowerInvariant()}@provider2.com",
                ["mobile"] = "+1-555-0201"
            },
            ["work_info"] = new Dictionary<string, object?>
            {
                ["role"] = "Security Guard",
                ["division"] = "Night Shift Security",
                ["start_date"] = "2024-02-01",
                ["current_status"] = "employed"
            }
        });
}
