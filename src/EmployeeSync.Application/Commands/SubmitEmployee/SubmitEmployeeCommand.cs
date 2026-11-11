using System.Text.Json;

namespace EmployeeSync.Application.Commands.SubmitEmployee;

public sealed record SubmitEmployeeCommand(string Provider, JsonElement Payload);
