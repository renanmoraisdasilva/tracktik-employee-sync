using System.Text.Json;
using EmployeeSync.Application.Contracts;
using EmployeeSync.Application.Providers.Provider1;
using EmployeeSync.Application.Providers.Provider2;
using EmployeeSync.Application.Validation;
using EmployeeSync.Domain.Employees;
using Shouldly;

namespace EmployeeSync.UnitTests;

public sealed class GoldenSampleMappingTests
{
    private static readonly Provider1Adapter Provider1 =
        new(new Provider1EmployeeValidator(), new MappedEmployeeValidator());

    private static readonly Provider2Adapter Provider2 =
        new(new Provider2EmployeeValidator(), new MappedEmployeeValidator());

    [Fact]
    public async Task Provider_samples_match_the_expected_tracktik_records()
    {
        var actual = new List<TrackTikEmployee>();

        foreach (var payload in ReadArray("provider1-employees.json"))
        {
            var result = await Provider1.MapAsync(payload, CancellationToken.None);
            result.IsSuccess.ShouldBeTrue();
            actual.Add(ToTrackTik("provider1", result.Value));
        }

        foreach (var payload in ReadArray("provider2-employees.json"))
        {
            var result = await Provider2.MapAsync(payload, CancellationToken.None);
            result.IsSuccess.ShouldBeTrue();
            actual.Add(ToTrackTik("provider2", result.Value));
        }

        var expected = JsonSerializer.Deserialize<List<TrackTikEmployee>>(
            ReadFixture("expected-tracktik-employees.json"));

        expected.ShouldNotBeNull();
        actual.ShouldBe(expected);
    }

    private static IEnumerable<JsonElement> ReadArray(string fixture) =>
        JsonDocument.Parse(ReadFixture(fixture))
            .RootElement
            .EnumerateArray()
            .ToArray();

    private static TrackTikEmployee ToTrackTik(
        string provider,
        MappedEmployee mapped) =>
        new(
            $"{provider}-{mapped.ProviderEmployeeId}",
            mapped.FirstName,
            mapped.LastName,
            mapped.Email,
            mapped.PhoneNumber,
            mapped.Position,
            mapped.Department,
            mapped.StartDate,
            mapped.Status.ToApiString());

    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}
