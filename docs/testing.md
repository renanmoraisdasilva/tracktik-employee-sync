# Testing

- **Status:** Accepted
- **Date:** 2026-11-11
- **Spec:** [`specs/001-employee-sync/spec.md`](specs/001-employee-sync/spec.md)
- **Decisions:** [ADR-006](decisions/ADR-006-testing-and-ci.md)

---

## 1. Test pyramid

| Layer | What it proves | Speed |
|---|---|---|
| Unit | Mapping, validation, sync rules, token cache, event rules | Fast |
| Integration (SQLite) | HTTP pipeline, DI, JSON, ProblemDetails, EF Core, persistence | Fast |
| Integration (Testcontainers) | Real SQL Server migration/persistence, case-sensitive identity and real NATS publish | Slower; Docker |
| Manual | Compose, real mock credentials, forced failure, reviewer path | Manual |

## 2. Commands

```bash
# Default suite: unit + SQLite integration. No Docker required.
dotnet test

# Unit tests only
dotnet test tests/EmployeeSync.UnitTests

# Integration tests only
dotnet test tests/EmployeeSync.IntegrationTests

# Testcontainers bonus (Docker required)
dotnet test tests/EmployeeSync.Testcontainers
```

## 3. Unit test matrix

| Area | Scenario | Expected |
|---|---|---|
| Mapping | Both provider sample sets | Match `expected-tracktik-employees.json` |
| Mapping | Provider 1 full sample | Matches the expected fields |
| Mapping | Provider 1 optional fields missing | Optional fields null/omitted |
| Mapping | Provider 2 full sample | Matches the expected fields |
| Mapping | Provider 2 nested values | Correct flattening |
| Status | `employed`, `on_leave`, `terminated` | `active`, `inactive`, `terminated` |
| Status | Invalid value | `TryParse` returns false and `Unknown`, never `Active` |
| Identity | `provider1` + `P1_001` | `provider1-P1_001` |
| Validation | Missing name/email/status | 400; message |
| Validation | Invalid email/date/status | 400; message |
| Validation | Error keys | Provider JSON names (`email_address`, `personal_info.email`) |
| Validation | Extra field | Ignored |
| Validation | Optional fields omitted | Accepted |
| Sync | TrackTik success | `synced`, attempts++, error null |
| Sync | TrackTik 500/timeout/429 | `failed`, attempts++, error recorded |
| Sync | Flag false | POST |
| Sync | Flag true | PUT |
| Sync | Failed, never created | Retry POST |
| Sync | Failed after create | Retry PUT |
| Sync | TrackTik success + DB failure | Retry 3 times, then unexpected error |
| Ordering | First success | Pending commit before TrackTik; event after synced commit |
| Event | Successful sync | One `EmployeeSynced` after commit |
| Event | Failed sync | No event |
| Token | Two calls before expiry | One token request |
| Token | Short-lived token | Skew capped at half the lifetime; not immediately expired |
| Token | Expiry skew reached | New token |
| Token | 401 | Invalidate, refresh once, retry once |
| Token | Concurrent callers | Single-flight refresh |
| Token | Token endpoint timeout | `TrackTikException`, not an unhandled cancellation |
| Token | Unsupported token content read | `TrackTikException`, not `NotSupportedException` |
| Token | Caller cancellation | `OperationCanceledException` propagates |

## 4. Integration test matrix

| Scenario | Request | Expected |
|---|---|---|
| First insert success | Valid Provider 1 POST; fake TrackTik 201 | 201, `Location`, `synced`, one row, event |
| Update success | Same identity again; fake expects PUT | 200, same row, PUT, event |
| TrackTik failure | Valid POST; fake 500 | 502, `application/problem+json`, `failed`, row kept, no event |
| Resubmit after failure | Same identity; fake 201 | 200, same row, POST, `synced`, one event |
| Same email, different providers | POST both providers with the same email | Two rows and two TrackTik IDs; no merge |
| Identity case | POST `P1_001` and `p1_001` | Two rows; case-sensitive on SQLite and SQL Server |
| Validation failure | Missing required field | 400, no row, fake TrackTik never called |
| Unknown provider | POST `/providers/provider3/...` | 404 ProblemDetails, no row |
| GET found | GET after success | 200, full response |
| GET missing | GET unknown identity | 404 ProblemDetails |
| OpenAPI | GET `/openapi/v1.json` | `oneOf` provider bodies, `application/json` success, `application/problem+json` errors, required fields |
| Scalar | GET `/scalar/v1` | 200 HTML |
| Report | GET report | 200, counts by status |

Token-endpoint timeout is proven at the provider unit boundary. The API-level
`502`/failed-row path is covered by the fake TrackTik failure test; the token
HTTP client is not simulated through `WebApplicationFactory`.

## 5. `WebApplicationFactory`

- Add `public partial class Program { }` at the end of `Program.cs`.
- Use `WebApplicationFactory<Program>`.
- Replace the `DbContext` with SQLite in-memory.
- Keep the SQLite connection open for the test lifetime; otherwise the database
  disappears when the connection closes.
- Replace `ITrackTikClient` with a recording fake.
- Apply the committed migrations (preferred). `EnsureCreated` is acceptable only
  for the fast SQLite tests; Testcontainers always applies migrations.
- Assert both the HTTP response and the database state.
- Use a unique database name per test class.

## 6. Fake TrackTik at each layer

Unit tests for `TrackTikClient` use a recording `HttpMessageHandler`;
integration tests replace `ITrackTikClient` with a fake.

```csharp
public sealed class FakeTrackTikHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();
    public Queue<HttpResponseMessage> Responses { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var response = Responses.Count > 0
            ? Responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.OK);
        return Task.FromResult(response);
    }
}
```

- Script the token response first, then the employee response.
- Use `InternalServerError` to test the 502 path.
- Inspect `Requests` to assert POST vs PUT, URL, bearer header and body.
- Simulate a timeout by delaying longer than the configured timeout or throwing
  `TaskCanceledException`.
- Never call the real mock in automated tests.

## 7. Testcontainers

- The opt-in `tests/EmployeeSync.Testcontainers` project uses `Testcontainers.MsSql`; its tests are tagged `Category=Testcontainers`.
- `Post_persists_and_syncs_against_real_sql_server` applies the real migrations and exercises the POST → SQL Server path.
- `Provider_employee_ids_are_case_sensitive` proves the binary collation keeps `TC_case` and `TC_CASE` as two rows.
- `Publish_sends_the_event_to_the_configured_subject` publishes through `NatsEventPublisher` to a real NATS container and asserts camelCase JSON.
- These are **three focused tests**, not the whole integration suite.
- The project is intentionally outside `EmployeeSync.sln`; a bare local
  `dotnet test` stays Docker-free.
- Docker must be running.
- They are excluded from the default local `dotnet test`; CI runs them on a
  Docker-capable runner.
- The README documents the command and the Docker requirement.

## 8. CI

GitHub Actions on push/PR to `main`:

1. `dotnet restore`
2. `dotnet build --no-restore`
3. `dotnet test tests/EmployeeSync.UnitTests --no-build`
4. `dotnet test tests/EmployeeSync.IntegrationTests --no-build` (SQLite)
5. `dotnet test tests/EmployeeSync.Testcontainers --configuration Release` (Docker)
6. secret scan (gitleaks or equivalent)

The default suite is Docker-free. CI runs the SQLite integration tests and the
Testcontainers tests on a Docker-capable runner. No real TrackTik network is used.

## 9. Coverage philosophy

Cover every contract branch. Do not chase 100% line coverage. The high-value
branches are:

- validation → 400, no write, no HTTP;
- sync success → `synced`, event;
- sync failure → `failed`, 502, no event, row kept;
- resubmission → same row, POST vs PUT;
- token reuse/expiry/401, timeout vs caller cancellation;
- unknown provider / missing employee → 404;
- OpenAPI reachable.

### Current coverage snapshot

Measured on `main` on 12 November 2026 from the **46 unit + 19 SQLite
integration tests**:

| Scope | Line | Branch |
|---|---:|---:|
| Overall | **74.1%** (1,626 / 2,195) | **48.8%** (208 / 426) |
| Application | 91.7% | 80.5% |
| Domain | 91.8% | 61.1% |
| API | 61.7% | 35.7% |
| Infrastructure | 71.2% | 55.4% |
| Application + Domain | 91.7% | 77.0% |
| Excluding generated OpenAPI and EF migrations | 87.1% | 68.1% |

Method coverage is 88.5%; fully covered methods are 77.4%.

The raw number is dragged down by generated OpenAPI code and EF migrations.
The NATS publisher is covered by the separate Testcontainers test, not by this
solution coverage run. The three Testcontainers tests are also not included.

Commands:

```bash
dotnet test EmployeeSync.sln --collect:"XPlat Code Coverage" --results-directory TestResults/Coverage
reportgenerator \
  "-reports:TestResults/Coverage/**/coverage.cobertura.xml" \
  "-targetdir:TestResults/CoverageReport" \
  "-reporttypes:TextSummary;Html"
```

## 10. Definition of done

- `dotnet test` passes without Docker.
- `dotnet test tests/EmployeeSync.Testcontainers` passes with Docker.
- The sample Provider 1 and Provider 2 payloads map exactly.
- A forced TrackTik failure produces the 502, failed row and no event.
- A resubmission updates the same row and uses PUT after a successful create.
- The OpenAPI document and Scalar UI are reachable.
- CI is green.
