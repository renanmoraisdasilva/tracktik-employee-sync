# AGENTS.md — TrackTik Employee Sync

A small ASP.NET Core 10 service that receives employees from two providers,
stores them in SQL Server, and synchronises them to a mock TrackTik API.

## Read first

- `README.md` — what it does and how to run it.
- `docs/specs/001-employee-sync/spec.md` — the behavioural contract.
- `docs/decisions/` — why the system is shaped this way.
- `docs/architecture.md` — layers, flow and known limitations.
- `docs/testing.md` — test strategy and commands.

## Commands

```powershell
dotnet build
dotnet test
dotnet test tests/EmployeeSync.Testcontainers   # Docker
docker compose up --build
dotnet ef database update `
  --project src/EmployeeSync.Infrastructure `
  --startup-project src/EmployeeSync.Api
```

## Boundaries

- `src/EmployeeSync.Api/` — controllers, validation wiring, ProblemDetails,
  OpenAPI, DI. No business rules, SQL or TrackTik HTTP.
- `src/EmployeeSync.Application/` — commands/queries, sync orchestration,
  provider adapters, validators, mappers, interfaces. No EF Core or ASP.NET.
- `src/EmployeeSync.Domain/` — `Employee`, `EmployeeIdentity`, `SyncStatus`,
  `EmployeeSynced`, `Result`. No infrastructure.
- `src/EmployeeSync.Infrastructure/` — EF Core, migrations, repository, TrackTik
  client, token cache, event publishers, report query.
- `tests/` — unit and integration tests. Never call the real mock.

## Rules

1. Persist `pending` before calling TrackTik. Never roll back a failed row.
2. Only `pending`, `synced`, `failed`.
3. One row per `(provider, providerEmployeeId)`; the unique index is the
   guarantee.
4. Decide POST vs PUT from `ExistsInTrackTik`, never from the mock's GET.
5. Publish `EmployeeSynced` only after the `synced` commit.
6. `lastError` clears on success; `attempts` is monotonic.
7. Every error is `ProblemDetails`; the 502 includes the sync state.
8. No secrets in git. Use user-secrets or an untracked `.env`.
9. Parameterised EF Core only; no string-built SQL.
10. Keep the architecture lightweight: no MediatR, AutoMapper, generic
    repository, Serilog or OpenTelemetry.
11. Update the spec, ADR and README when behavior changes.
12. Tests must cover the failure paths, not only the happy path.

## Commits

- Author: `Renan Silva <81625697+renanmoraisdasilva@users.noreply.github.com>`.
- One-line messages, no body or trailers.
- Small, coherent commits.

## Definition of done

- Build and default tests pass.
- Testcontainers pass separately.
- Compose works from a clean clone.
- OpenAPI and Scalar are reachable.
- No secret in git history.
- README matches the actual behavior.
