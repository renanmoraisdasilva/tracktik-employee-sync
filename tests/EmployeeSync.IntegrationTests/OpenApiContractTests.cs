using System.Net;
using System.Text.Json;
using Shouldly;

namespace EmployeeSync.IntegrationTests;

public sealed class OpenApiContractTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public OpenApiContractTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
        factory.InitializeDatabase();
    }

    [Fact]
    public async Task Served_document_matches_the_provider_contract()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var scalar = await _client.GetAsync("/scalar/v1");
        scalar.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        var post = root.GetProperty("paths")
            .GetProperty("/api/v1/providers/{provider}/employees")
            .GetProperty("post");

        var requestContent = post.GetProperty("requestBody").GetProperty("content");
        requestContent.EnumerateObject().Select(content => content.Name)
            .ShouldBe(["application/json"]);

        var requestSchema = requestContent
            .GetProperty("application/json")
            .GetProperty("schema");

        requestSchema.GetProperty("oneOf")
            .EnumerateArray()
            .Select(schema => schema.GetProperty("$ref").GetString())
            .ShouldBe(
            [
                "#/components/schemas/Provider1Employee",
                "#/components/schemas/Provider2Employee"
            ]);

        AssertContentType(post, "201", "application/json");
        AssertContentType(post, "200", "application/json");
        AssertContentType(post, "400", "application/problem+json");
        AssertContentType(post, "404", "application/problem+json");
        AssertContentType(post, "502", "application/problem+json");
        AssertContentType(post, "500", "application/problem+json");

        var get = root.GetProperty("paths")
            .GetProperty("/api/v1/employees/{provider}/{providerEmployeeId}")
            .GetProperty("get");

        AssertContentType(get, "200", "application/json");
        AssertContentType(get, "404", "application/problem+json");
        AssertContentType(get, "500", "application/problem+json");

        var schemas = root.GetProperty("components").GetProperty("schemas");
        schemas.TryGetProperty("JsonElement", out _).ShouldBeFalse();

        Required(schemas, "Provider1Employee")
            .ShouldContain("employment_status");

        Required(schemas, "PersonalInfo")
            .OrderBy(name => name)
            .ShouldBe(["email", "family_name", "given_name"]);

        Required(schemas, "WorkInfo")
            .ShouldContain("current_status");
    }

    private static void AssertContentType(
        JsonElement operation,
        string status,
        string expected)
    {
        var content = operation.GetProperty("responses")
            .GetProperty(status)
            .GetProperty("content");

        content.EnumerateObject().Select(mediaType => mediaType.Name)
            .ShouldBe([expected]);
    }

    private static string[] Required(JsonElement schemas, string schema) =>
        schemas.GetProperty(schema)
            .GetProperty("required")
            .EnumerateArray()
            .Select(name => name.GetString()!)
            .ToArray();
}
