# ADR-006 — Contract-first tests, SQLite by default, Testcontainers opt-in

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** Renan Silva
- **Related:** `docs/testing.md`, `docs/specs/001-employee-sync/spec.md` (AC-001–AC-020)

## Context

The challenge requires unit tests for mapping and sync logic and at least one
API integration test using `WebApplicationFactory`. The evaluator will probe the
failure paths: 400 with no write, 502 with a failed row, resubmission without a
duplicate, PUT after create, and the event rules.

The reviewer may or may not have Docker running for `dotnet test`. Compose
already requires Docker, but the default test command should stay fast and
portable.

## Decision

- Use **xUnit + Shouldly + NSubstitute**. Avoid FluentAssertions because of its
  v8 commercial-license consideration.
- Unit-test mapping, validation, sync success/failure/retry, POST/PUT, token
  cache and event rules.
- Use **`WebApplicationFactory<Program>`** with **SQLite in-memory** for the
  default integration tests.
- Use a **recording `HttpMessageHandler`** to fake TrackTik. Never call the real
  mock from automated tests.
- Add **Testcontainers SQL Server** tests in the opt-in
  `tests/EmployeeSync.Testcontainers` project, tagged `Category=Testcontainers`.
- CI runs restore, build, **unit tests**, the **SQLite integration tests** and a
  secret scan. It does not run the SQL Server/Testcontainers tests.
- Do not chase 100% line coverage; cover every contract branch and the failure
  paths.

## Consequences

- `dotnet test` is fast and does not require Docker.
- The Testcontainers bonus is real and can be demonstrated separately.
- SQLite is relational but not SQL Server; a few SQL Server-specific behaviors
  are only covered by the opt-in project.
- CI is deterministic and meaningful, and it exercises the HTTP pipeline through
  SQLite. It does not prove SQL Server-specific behaviour; the README tells the
  reviewer how to run Testcontainers locally.

## Alternatives considered

- **FluentAssertions** — licensing concern for company work.
- **Testcontainers as the default** — slower and Docker-dependent; would break a
  simple `dotnet test`.
- **EF Core InMemory** — hides relational constraints and duplicate-key bugs.
- **Real mock in tests** — network-dependent, rate-limited and unable to simulate
  outages.
- **SQL Server/Testcontainers tests in CI** — more configuration and flakiness
  than the challenge needs; the SQLite integration tests stay in CI.
