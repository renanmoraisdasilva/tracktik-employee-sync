# ADR-004 — Composite identity, unique upsert and POST/PUT

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** Renan Silva
- **Related:** `docs/specs/001-employee-sync/spec.md` (FR-002, FR-007, FR-010)

## Context

The challenge identifies an employee by `(provider, providerEmployeeId)` and
derives the TrackTik `employeeId` as `{provider}-{providerEmployeeId}`. Records
from different providers are never merged, even when they share an email.

The mock TrackTik API is stateless: `GET` always returns a sample record and
`PUT` accepts any ID. It cannot tell us whether an employee was ever created.

## Decision

- Use a unique index on `(Provider, ProviderEmployeeId)`.
- Store the derived `TrackTikEmployeeId`.
- Persist an `ExistsInTrackTik` flag:
  - start `false`;
  - a successful `POST` sets it `true`;
  - a successful `PUT` keeps it `true`;
  - a failed `POST` keeps it `false`;
  - a failed `PUT` keeps it `true`.
- Use `POST` when the flag is `false`, otherwise `PUT`.
- Never use the mock's `GET` to decide.
- A resubmission updates the existing row and retries the sync.
- The first insert returns `201 Created` with a `Location` header; a
  resubmission returns `200 OK`.
- If a real `POST` returns `409`, treat it as "already exists" and retry with
  `PUT`.

## Consequences

- Exactly one row per identity; resubmission cannot duplicate.
- The POST/PUT decision is deterministic and testable.
- The flag has a hard edge case: if TrackTik accepts a POST but the status
  update fails, the flag can remain `false` and a retry could POST again. The
  mock is stateless, so it cannot be observed there; a real system needs an
  outbox or reconciliation. This is documented as a known limitation.
- A duplicate-key race is retried as an update; the behavior is last-write-wins.

## Alternatives considered

- **Use email as identity** — violates the contract and merges providers.
- **Use the mock's GET to decide** — the mock always returns a sample, so it
  lies.
- **Infer existence from a local row** — a local row can exist without a
  TrackTik employee.
- **Always POST** — duplicates after the first success.
- **Always PUT** — fails when the employee was never created.
- **Optimistic concurrency with 409** — more code/client handling than the
  challenge needs.
