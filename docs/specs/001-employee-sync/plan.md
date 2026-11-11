# 001 — Employee Sync Implementation Plan

- **Status:** Accepted
- **Date:** 2026-11-11
- **Spec:** [`spec.md`](spec.md)
- **Tasks:** [`tasks.md`](tasks.md)
- **Decisions:** `docs/decisions/`, planning package `DECISIONS.md`
- **Challenge deadline:** Monday, 16 November 2026, 11:59 PM ET

---

## 1. Technical context

| Area | Choice |
|---|---|
| Runtime | .NET 10 |
| API | ASP.NET Core controllers, `/api/v1` |
| Architecture | 4 projects: Api / Application / Domain / Infrastructure |
| CQRS | Hand-rolled `ICommandHandler` / `IQueryHandler`, no MediatR |
| Validation | FluentValidation |
| Mapping | Manual mapper classes |
| Results | `Result` / `Result<T>` for expected failures; exceptions for bugs |
| Data | EF Core 10 + SQL Server 2022 |
| Test DB | SQLite in-memory by default; Testcontainers SQL Server opt-in |
| Tests | xUnit + Shouldly + NSubstitute |
| HTTP client | `IHttpClientFactory` typed client, singleton token provider |
| Events | In-memory publisher by default; NATS in Compose |
| API docs | Built-in OpenAPI + Scalar |
| Containers | Docker Compose: API + SQL Server + NATS |
| CI | GitHub Actions: restore, build, unit tests, secret scan |

## 2. Constraints

- Keep it lightweight. No MediatR, AutoMapper, generic repository, Serilog or
  OpenTelemetry.
- Persist before forwarding. Never roll back a failed sync.
- Only `pending`, `synced`, `failed`.
- One row per `(provider, providerEmployeeId)`.
- `EmployeeSynced` only after the `synced` commit.
- No secrets in git. Placeholders only in committed configuration.
- Every error is `ProblemDetails`.
- README is reviewer-first and must work from a clean clone.
- Tests cover the failure paths, not just the happy path.

## 3. Phase 0 — Repository and skeleton

**Goal:** a runnable empty shell.

- Create `EmployeeSync.sln` and the six projects.
- Add project references in the allowed direction:
  - Api → Application, Infrastructure
  - Application → Domain
  - Infrastructure → Application, Domain
  - Tests → the projects they exercise
- Add `global.json` for the .NET 10 SDK, `Directory.Build.props` for nullable and
  warnings, `.editorconfig`, `.gitignore`.
- Add `Dockerfile`, `docker-compose.yml`, `.env.example` and `appsettings.json`
  placeholders.
- Add `/health`, OpenAPI, Scalar and a hello endpoint.
- Add the initial `docs/` skeleton and `AGENTS.md`.

**Exit criteria:** `dotnet build` passes; `docker compose up --build` starts the
API and SQL Server; `/health` returns healthy; no secret is committed.

## 4. Phase 1 — Domain and database

**Goal:** the durable model and schema.

- Add `Employee`, `EmployeeIdentity`, `SyncStatus`, `EmployeeStatus` and
  `EmployeeSynced`.
- Add `AppDbContext` and the `Employees` configuration:
  - unique index `(Provider, ProviderEmployeeId)`;
  - check constraints for `SyncStatus` and `Status`;
  - `datetimeoffset(7)` timestamps, `date` for `StartDate`;
  - `rowversion` is a future option; v1 uses the unique index + retry.
- Add `IEmployeeRepository` and the EF Core implementation.
- Add the initial migration.
- Add a startup migration path with retry and the `Database__AutoMigrate` flag.

**Exit criteria:** `dotnet ef database update` creates the schema; a clean
database can be created from the committed migration; the unique index rejects a
duplicate identity.

## 5. Phase 2 — Providers, validation and mapping

**Goal:** turn both payloads into a TrackTik employee.

- Add the Provider 1 and Provider 2 DTOs with the correct JSON names.
- Add `Provider1EmployeeValidator` and `Provider2EmployeeValidator`.
- Add the outbound `TrackTikEmployeeValidator` for TrackTik-required fields.
- Add `Provider1Mapper` and `Provider2Mapper`.
- Add `IProviderAdapter` / `ProviderAdapterFactory`.
- Unit-test both mappings against the golden sample output.
- Unit-test validation: missing fields, invalid email, bad date, unknown status,
  extra fields, null optional fields.

**Exit criteria:** the four sample records map exactly; invalid payloads fail
with useful messages; mapping and validation tests pass.

## 6. Phase 3 — API, CQRS and error model

**Goal:** the two mandatory endpoints with a consistent error shape.

- Add `Result` / `Result<T>` and the error codes.
- Add `SubmitEmployeeCommand` + handler.
- Add `GetEmployeeQuery` + handler.
- Add `EmployeesController`:
  - POST route; 201 first insert with `Location`, 200 on resubmission;
  - GET route; 200 or 404.
- Add `ResultExtensions` mapping to `ProblemDetails` / `ValidationProblemDetails`.
- Add FluentValidation wiring and automatic 400s.
- Add OpenAPI metadata and examples.
- Add a report endpoint for the bonus query.

**Exit criteria:** the sample POST returns 201/200; GET returns the stored
record; validation returns 400 with no write; unknown provider returns 404;
OpenAPI lists the routes.

## 7. Phase 4 — TrackTik client and sync orchestration

**Goal:** the real integration and failure path.

- Add `TrackTikOptions` and the typed `TrackTikClient`.
- Add `ITrackTikTokenProvider` with cache, expiry skew, single-flight refresh
  and 401 invalidation.
- Implement the sync flow:
  1. validate;
  2. map;
  3. persist `pending`;
  4. get token;
  5. POST/PUT based on `ExistsInTrackTik`;
  6. on success: `synced`, attempts++, error cleared, exists flag true;
  7. on failure: `failed`, attempts++, error recorded, 502.
- Add a fake `HttpMessageHandler` and unit tests for success, 500, timeout, 401,
  429, token reuse and POST/PUT.
- Add the 502 `ProblemDetails` with sync fields.

**Exit criteria:** a forced 500 leaves a `failed` row and returns 502; a success
leaves `synced`; resubmission uses the right method; no duplicates.

## 8. Phase 5 — Event publishing and NATS

**Goal:** `EmployeeSynced` after the commit, with a NATS bonus.

- Add `IEventPublisher`, `InMemoryEventPublisher` and `NatsEventPublisher`.
- Publish only after the `synced` commit.
- Add the event payload: eventId, eventType, occurredAt, provider,
  providerEmployeeId, employeeId, attempts.
- Add the NATS service to Compose and `Events__Mode=Nats` for the API container.
- Keep `InMemory` as the `dotnet run` default.
- Log publisher failures; do not fail the request.
- Test that the event is published on success and not on failure.

**Exit criteria:** Compose publishes to NATS subject
`tracktik.employees.synced`; local runs use in-memory; tests use a fake.

## 9. Phase 6 — Test suite

**Goal:** confidence in the contract and the failure paths.

- Unit tests:
  - mapping (both providers, golden output);
  - status mapping and identity;
  - validation;
  - sync success/failure/retry;
  - POST/PUT decision;
  - token cache: reuse, expiry, single-flight, 401;
  - event rules.
- Integration tests with `WebApplicationFactory` + SQLite:
  - 201 first insert / 200 resubmission;
  - 400 with no row/no TrackTik call;
  - 404 unknown provider and missing employee;
  - 502 with failed row and no event;
  - PUT after a successful create;
  - GET response shape;
  - OpenAPI endpoint.
- Testcontainers category:
  - SQL Server migrations and the integration suite;
  - opt-in project `tests/EmployeeSync.Testcontainers` (tagged `Category=Testcontainers`), Docker required.

**Exit criteria:** `dotnet test` passes without Docker; the Testcontainers
category passes with Docker; every acceptance criterion in the spec has a test
or a documented manual check.

## 10. Phase 7 — Bonus report, docs and CI

**Goal:** the four bonus items and the reviewer experience.

- Add the T-SQL report script and the report endpoint.
- Finish the OpenAPI document and Scalar UI.
- Write the README, architecture, testing doc and ADRs.
- Add the GitHub Actions workflow: restore, build, unit tests, secret scan.
- Add `.env.example`, user-secrets instructions and the migration commands.
- Add a secret scan locally.

**Exit criteria:** all four bonus items are visible and documented; CI is green;
the README works from a fresh clone.

## 11. Phase 8 — Polish and optional outbox

**Goal:** final quality and stretch reliability.

- Run the full manual flow with Compose.
- Test the failure path with a forced 500.
- Test resubmission and PUT.
- Check the OpenAPI/Scalar pages.
- Run the secret scan.
- If all four bonus items and CI are green, consider the outbox stretch:
  `OutboxMessages` table, event written in the same transaction as `synced`,
  background publisher, tests, docs. If not, document it as next step.

**Exit criteria:** the repo is green, the README is accurate, no secret is
committed, and the submission is ready.

## 12. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| SQL Server startup is slow | Compose API fails on first start | Healthcheck + `depends_on` + migration retry |
| Mock is stateless | POST/PUT decision is wrong | Persist `ExistsInTrackTik`; never use GET |
| Token cache race | Multiple token requests | `SemaphoreSlim` single-flight |
| DB update fails after TrackTik success | Stale row | Retry status update; 500 + log; document |
| Event publish fails | Missing event | Log; outbox stretch |
| Too many bonus items | Mandatory quality slips | Gated order; drop NATS first |
| Testcontainers needs Docker | `dotnet test` fails | Opt-in project |
| CI cannot run integration tests | Red build | CI runs build + unit + secret scan |
| Secret committed | Public exposure | `.gitignore`, user-secrets, secret scan |
| Scope creep (auth, outbox, OTel) | Deadline risk | Explicitly out of scope |

## 13. Validation strategy

- Unit tests for pure logic and the sync orchestration.
- Integration tests through the real HTTP pipeline with SQLite.
- Testcontainers for SQL Server fidelity.
- Manual Compose run with the sample payloads and a forced failure.
- Secret scan before the first push and before submission.
- Fresh-clone test: follow the README from a new folder.

## 14. Complexity tracking

Any addition beyond this plan must answer:

1. Does it improve the evaluation score or the contract?
2. Does it add a runtime dependency the reviewer must install?
3. Does it make the README longer or the run path less reliable?
4. Can it be documented as a next step instead?

If the answer to 2 or 3 is yes and 1 is no, do not add it.

## 15. Definition of done

- All mandatory acceptance criteria pass.
- All four bonus items are implemented and visible.
- `dotnet test` passes; Testcontainers passes separately.
- `docker compose up --build` works from a clean clone.
- The README has prerequisites, run, configuration, migrations, tests and
  assumptions.
- The OpenAPI document and Scalar UI are reachable.
- No secret is in git history.
- CI is green.
- The submission is sent before the deadline.
