# ADR-007 — All four bonus items, gated; outbox deferred

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** Renan Silva
- **Related:** `docs/specs/001-employee-sync/plan.md`, `DECISIONS.md`

## Context

The challenge lists four bonus items: CQRS, a NATS domain-event publisher in
Docker Compose, a T-SQL stored procedure/report query, and Testcontainers
integration tests. The user wants all of them.

The deadline is tight enough that bonus work can damage the mandatory score if
it is not gated. The evaluator cares more about the contract, tests and ease of
running than the number of features.

## Decision

Implement all four bonus items in this order:

1. **CQRS** — hand-rolled command/query handlers.
2. **T-SQL report** — a read-only query or stored procedure for employee counts
   by sync status, optionally exposed as an endpoint.
3. **Testcontainers** — an opt-in SQL Server integration-test category.
4. **NATS** — a publisher in Compose, with an in-memory fallback.

Gates:

- If the mandatory contract and tests are not green by Saturday afternoon, drop
  NATS first, then Testcontainers.
- CQRS and the report query stay; they are low-risk.
- A transactional outbox is a **formal stretch** only after all four bonus items
  and CI are green. It is not part of v1.
- No auth, automatic retries or OpenTelemetry in v1.

## Consequences

- The submission has a strong feature story without a second runtime stack.
- NATS is visible in Compose but not required for `dotnet run` or tests.
- Testcontainers is real but opt-in, so `dotnet test` stays portable.
- The outbox remains a documented next step; reliable event delivery is not
  claimed.
- The README has a "Bonus items" table so the reviewer can find each one.

## Alternatives considered

- **Mandatory only** — leaves evaluation/portfolio value on the table.
- **All four plus outbox as a commitment** — extra table, worker and tests;
  deadline risk.
- **High-value three (skip NATS)** — less complete than the user wants.
- **NATS as the default for every run** — fails without the broker.
- **Testcontainers as the default test database** — breaks the simple
  `dotnet test` path.
