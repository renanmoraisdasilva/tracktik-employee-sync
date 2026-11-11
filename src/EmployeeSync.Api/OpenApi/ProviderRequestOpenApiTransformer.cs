using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EmployeeSync.Api.OpenApi;

/// <summary>
/// The provider endpoint accepts either provider shape, so the action binds
/// <see cref="System.Text.Json.JsonElement"/>. Replace the generated
/// <c>JsonElement</c> request body with the same <c>oneOf</c> contract that is
/// committed in <c>docs/specs/001-employee-sync/contracts/openapi.yaml</c>.
/// </summary>
internal sealed class ProviderRequestOpenApiTransformer : IOpenApiOperationTransformer
{
    private const string ProvidersPath = "api/v1/providers/{provider}/employees";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Description.HttpMethod != HttpMethods.Post
            || context.Description.RelativePath != ProvidersPath)
        {
            return Task.CompletedTask;
        }

        var document = context.Document;
        if (document is null)
        {
            return Task.CompletedTask;
        }

        AddProviderSchemas(document);

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        OneOf =
                        [
                            SchemaReference("Provider1Employee", document),
                            SchemaReference("Provider2Employee", document)
                        ]
                    },
                    Examples = new Dictionary<string, IOpenApiExample>
                    {
                        ["provider1"] = new OpenApiExample
                        {
                            Summary = "Flat Provider 1 employee",
                            Value = JsonNode.Parse(
                                """
                                {
                                  "emp_id": "P1_001",
                                  "first_name": "Alice",
                                  "last_name": "Johnson",
                                  "email_address": "alice.johnson@provider1.com",
                                  "phone": "+1-555-0101",
                                  "job_title": "Security Officer",
                                  "dept": "Security Operations",
                                  "hire_date": "2024-01-15",
                                  "employment_status": "active"
                                }
                                """)
                        },
                        ["provider2"] = new OpenApiExample
                        {
                            Summary = "Nested Provider 2 employee",
                            Value = JsonNode.Parse(
                                """
                                {
                                  "employee_number": "P2_001",
                                  "personal_info": {
                                    "given_name": "Carol",
                                    "family_name": "Davis",
                                    "email": "carol.davis@provider2.com",
                                    "mobile": "+1-555-0201"
                                  },
                                  "work_info": {
                                    "role": "Security Guard",
                                    "division": "Night Shift Security",
                                    "start_date": "2024-02-01",
                                    "current_status": "employed"
                                  }
                                }
                                """)
                        }
                    }
                }
            }
        };

        return Task.CompletedTask;
    }

    private static void AddProviderSchemas(OpenApiDocument document)
    {
        var schemas = document.Components?.Schemas;
        if (schemas is null)
        {
            return;
        }

        schemas.TryAdd("PersonalInfo", PersonalInfoSchema());
        schemas.TryAdd("WorkInfo", WorkInfoSchema());
        schemas.TryAdd("Provider1Employee", Provider1Schema());
        schemas.TryAdd("Provider2Employee", Provider2Schema(document));
    }

    private static OpenApiSchema Provider1Schema() => new()
    {
        Type = JsonSchemaType.Object,
        Description = "Flat snake_case payload from Provider 1.",
        Required = new HashSet<string>
        {
            "emp_id",
            "first_name",
            "last_name",
            "email_address",
            "employment_status"
        },
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["emp_id"] = String("P1_001"),
            ["first_name"] = String("Alice"),
            ["last_name"] = String("Johnson"),
            ["email_address"] = String("alice.johnson@provider1.com", "email"),
            ["phone"] = NullableString("+1-555-0101"),
            ["job_title"] = NullableString("Security Officer"),
            ["dept"] = NullableString("Security Operations"),
            ["hire_date"] = NullableString("2024-01-15", "date"),
            ["employment_status"] = EnumString(
                ["active", "inactive", "terminated"],
                "active")
        }
    };

    private static OpenApiSchema Provider2Schema(OpenApiDocument document) => new()
    {
        Type = JsonSchemaType.Object,
        Description = "Nested payload from Provider 2.",
        Required = new HashSet<string>
        {
            "employee_number",
            "personal_info",
            "work_info"
        },
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["employee_number"] = String("P2_001"),
            ["personal_info"] = SchemaReference("PersonalInfo", document),
            ["work_info"] = SchemaReference("WorkInfo", document)
        }
    };

    private static OpenApiSchema PersonalInfoSchema() => new()
    {
        Type = JsonSchemaType.Object,
        Required = new HashSet<string> { "given_name", "family_name", "email" },
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["given_name"] = String("Carol"),
            ["family_name"] = String("Davis"),
            ["email"] = String("carol.davis@provider2.com", "email"),
            ["mobile"] = NullableString("+1-555-0201")
        }
    };

    private static OpenApiSchema WorkInfoSchema() => new()
    {
        Type = JsonSchemaType.Object,
        Required = new HashSet<string> { "current_status" },
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["role"] = NullableString("Security Guard"),
            ["division"] = NullableString("Night Shift Security"),
            ["start_date"] = NullableString("2024-02-01", "date"),
            ["current_status"] = EnumString(
                ["employed", "on_leave", "terminated"],
                "employed")
        }
    };

    private static OpenApiSchema String(string example, string? format = null) => new()
    {
        Type = JsonSchemaType.String,
        Format = format,
        Example = JsonValue.Create(example)
    };

    private static OpenApiSchema NullableString(string example, string? format = null) => new()
    {
        Type = JsonSchemaType.String | JsonSchemaType.Null,
        Format = format,
        Example = JsonValue.Create(example)
    };

    private static OpenApiSchema EnumString(string[] values, string example) => new()
    {
        Type = JsonSchemaType.String,
        Enum = values.Select(value => (JsonNode)JsonValue.Create(value)!).ToList(),
        Example = JsonValue.Create(example)
    };

    private static OpenApiSchemaReference SchemaReference(
        string id,
        OpenApiDocument document) => new(id, document, null!);
}

/// <summary>
/// The POST body no longer binds to <see cref="System.Text.Json.JsonElement"/>
/// in the documented contract, so drop the unused generated component.
/// </summary>
internal sealed class OpenApiDocumentCleanupTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components?.Schemas?.Remove("JsonElement");
        return Task.CompletedTask;
    }
}
