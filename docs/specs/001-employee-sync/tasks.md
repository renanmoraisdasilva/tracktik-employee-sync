# 001 — Employee Sync Task Breakdown

- **Status:** Implemented; challenge submission pending
- **Spec:** [`spec.md`](spec.md)
- **Plan:** [`plan.md`](plan.md)
- **Rule:** one task at a time; keep the build and tests green; commit in
  one-line messages.

> Every task below is complete except **T-087** (send the challenge submission),
> which remains open. The checkboxes record implementation, not planned work.

---

## Phase 0 — Repository and skeleton

- [x] **T-001** Create the repository `tracktik-employee-sync`, add the
  `.gitignore`, `.editorconfig`, `global.json` and `Directory.Build.props`.
- [x] **T-002** Create `EmployeeSync.sln` and the six projects.
- [x] **T-003** Add the project references in the allowed direction.
- [x] **T-004** Add `Dockerfile`, `docker-compose.yml` and `.env.example`.
- [x] **T-005** Add `appsettings.json` placeholders and the options classes.
- [x] **T-006** Add `/health`, OpenAPI and Scalar.
- [x] **T-007** Verify `docker compose up --build` starts the API and SQL Server.
- [x] **T-008** Add the `docs/` skeleton and `AGENTS.md`.

## Phase 1 — Domain and database

- [x] **T-010** Add `Employee`, `EmployeeIdentity`, `SyncStatus`,
  `EmployeeStatus` and `EmployeeSynced`.
- [x] **T-011** Add `AppDbContext` and the `Employees` entity configuration.
- [x] **T-012** Add the unique index and check constraints.
- [x] **T-013** Add `IEmployeeRepository` and the EF Core implementation.
- [x] **T-014** Add the initial migration and apply it to a clean database.
- [x] **T-015** Add the startup migration path with retry and the
  `Database__AutoMigrate` flag.
- [x] **T-016** Add a test that the unique index rejects a duplicate identity.

## Phase 2 — Providers, validation and mapping

- [x] **T-020** Add the Provider 1 and Provider 2 request DTOs.
- [x] **T-021** Add `Provider1EmployeeValidator`.
- [x] **T-022** Add `Provider2EmployeeValidator`.
- [x] **T-023** Add `MappedEmployeeValidator` for the TrackTik-required fields.
- [x] **T-024** Add `Provider1Mapper` and `Provider2Mapper`.
- [x] **T-025** Add `IProviderAdapter` and the provider factory.
- [x] **T-026** Add the golden mapping tests for both providers.
- [x] **T-027** Add validation tests for missing fields, invalid email, bad date,
  unknown status, extra fields and null optional fields.
- [x] **T-028** Add the status-mapping tests.

## Phase 3 — API, CQRS and error model

- [x] **T-030** Add `Result` / `Result<T>` and the error codes.
- [x] **T-031** Add `SubmitEmployeeCommand` and the handler.
- [x] **T-032** Add `GetEmployeeQuery` and the handler.
- [x] **T-033** Add `EmployeesController` with the POST and GET routes.
- [x] **T-034** Add 201 first insert with `Location`, 200 on resubmission.
- [x] **T-035** Add `ResultExtensions` for `ProblemDetails` and
  `ValidationProblemDetails`.
- [x] **T-036** Wire FluentValidation and automatic 400s.
- [x] **T-037** Add OpenAPI metadata and examples.
- [x] **T-038** Add the report query handler and endpoint.
- [x] **T-039** Add tests for 201/200, 400, 404 and the GET shape.

## Phase 4 — TrackTik client and sync

- [x] **T-040** Add `TrackTikOptions` and the typed `TrackTikClient`.
- [x] **T-041** Add `ITrackTikTokenProvider` with cache, expiry skew,
  single-flight refresh and 401 invalidation.
- [x] **T-042** Implement the sync orchestration:
  validate → map → pending → token → POST/PUT → synced/failed.
- [x] **T-043** Add `ExistsInTrackTik` handling and the POST/PUT decision.
- [x] **T-044** Add the 502 `ProblemDetails` with the sync fields.
- [x] **T-045** Add a fake `HttpMessageHandler` and request recording.
- [x] **T-046** Add sync tests: success, 500, timeout, 401, 429, retry, token
  reuse and POST/PUT.
- [x] **T-047** Add a test that a failed forward keeps the row and does not
  roll back.
- [x] **T-048** Add a test that TrackTik success + DB update failure returns 500
  and logs.

## Phase 5 — Event publishing and NATS

- [x] **T-050** Add `IEventPublisher` and `InMemoryEventPublisher`.
- [x] **T-051** Add `NatsEventPublisher` and `NatsOptions`.
- [x] **T-052** Add the NATS service to Compose and `Events__Mode=Nats` for the
  API container.
- [x] **T-053** Publish `EmployeeSynced` only after the `synced` commit.
- [x] **T-054** Add the event payload with identifiers and metadata, no full PII.
- [x] **T-055** Log publisher failures and keep the request successful.
- [x] **T-056** Add tests for event-on-success and no-event-on-failure.
- [x] **T-057** Document the NATS subject and the in-memory fallback.

## Phase 6 — Test suite

- [x] **T-060** Add the unit test project references and test helpers.
- [x] **T-061** Add the integration test host with `WebApplicationFactory`.
- [x] **T-062** Replace the DbContext with SQLite in-memory for the default
  integration tests.
- [x] **T-063** Add the integration tests: 201/200, 400, 404, 502, resubmission,
  PUT after create, GET and OpenAPI.
- [x] **T-064** Add the `tests/EmployeeSync.Testcontainers` SQL Server tests
  (tagged `Category=Testcontainers`).
- [x] **T-065** Document the test commands and the Docker requirement.
- [x] **T-066** Run the full suite from a clean clone.

## Phase 7 — Bonus report, docs and CI

- [x] **T-070** Add `db/reports/employee-counts-by-sync-status.sql`.
- [x] **T-071** Add the report endpoint and test.
- [x] **T-072** Finish the OpenAPI document and the Scalar UI.
- [x] **T-073** Write the README with the reviewer-first structure.
- [x] **T-074** Write `docs/architecture.md` and `docs/testing.md`.
- [x] **T-075** Write the ADRs and link them from the README.
- [x] **T-076** Add the GitHub Actions workflow: restore, build, unit tests,
  SQLite and Testcontainers integration tests, secret scan.
- [x] **T-077** Add `.env.example`, user-secrets instructions and migration
  commands.
- [x] **T-078** Run the secret scan and inspect the git history.

## Phase 8 — Polish and optional outbox

- [x] **T-080** Run the manual Compose flow with both provider samples.
- [x] **T-081** Force a TrackTik 500 and verify the 502, failed row and no event.
- [x] **T-082** Resubmit and verify 200, same row and PUT after create.
- [x] **T-083** Verify the report endpoint and the OpenAPI/Scalar pages.
- [x] **T-084** Fresh-clone the repository and follow the README.
- [x] **T-085** Check CI is green.
- [x] **T-086** If all four bonus items and CI are green, consider the outbox
  stretch; otherwise document it as next step.
- [ ] **T-087** Prepare the submission payload and send it.

---

## Dependency notes

- T-040 depends on T-030–T-036 (Result, handlers, controller).
- T-050 depends on T-042 (the sync flow must exist before the event).
- T-060 depends on T-033–T-035 and T-040–T-044.
- T-070 depends on T-010–T-016 and T-030–T-038.
- T-073 depends on T-070–T-072 and the final API behaviour.
- T-086 depends on T-050–T-057, T-060–T-065 and T-076.

## Verification map

| Acceptance criterion | Tasks |
|---|---|
| AC-001/AC-002 mapping | T-024, T-026 |
| AC-003 identity | T-012, T-016, T-026 |
| AC-004 validation | T-021–T-023, T-027, T-039 |
| AC-005 first success | T-033–T-035, T-042, T-046 |
| AC-006 first failure | T-044–T-047 |
| AC-007/AC-008 resubmission | T-043, T-046, T-063 |
| AC-009–AC-011 token | T-041, T-046 |
| AC-012/AC-013 not found | T-033, T-039 |
| AC-014 OpenAPI | T-037, T-072 |
| AC-015 migrations | T-014, T-015 |
| AC-016 Compose | T-004, T-007 |
| AC-017 report | T-070, T-071 |
| AC-018 Testcontainers | T-064, T-065 |
| AC-019 CI | T-076, T-085 |
| AC-020 README | T-073, T-084 |
