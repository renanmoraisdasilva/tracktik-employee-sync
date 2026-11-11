# ADR-003 — Persist before forwarding; keep failed rows

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** Renan Silva
- **Related:** `docs/specs/001-employee-sync/spec.md` (FR-005, FR-009)

## Context

The TrackTik call is unreliable. The challenge explicitly requires the service
to store a record, forward it, and record the sync outcome. A failed forward
must not lose the employee: the row must stay `failed` and a resubmission must
retry it.

## Decision

The sync flow is:

1. validate;
2. map;
3. insert or update the row with `syncStatus = pending`;
4. **commit**;
5. call TrackTik with a cached OAuth2 token;
6. update the row to `synced` or `failed` and **commit** again.

On a forward failure:

- the row stays in the database;
- it becomes `failed`;
- `attempts` increments;
- `lastError` and `lastAttemptAt` are recorded;
- no `EmployeeSynced` event is published;
- the API returns `502 ProblemDetails` with the sync state.

The only statuses are `pending`, `synced` and `failed`.

## Consequences

- There are two commits, not one transaction around the whole flow. That is
  deliberate: the first commit is the durable record of intent.
- A failed external call is recoverable via resubmission.
- The GET endpoint can show `pending` during a sync.
- If TrackTik succeeds but the status update fails, the row can be stale. The
  service retries the status update, then returns `500` and logs the
  inconsistency. An outbox/reconciliation is the next step.
- Rolling back the row on failure is forbidden; the failure path is a feature.

## Alternatives considered

- **Call TrackTik first, save on success** — loses the record on failure.
- **One transaction around the external call** — holds a transaction open across
  a network call and cannot persist a failed row cleanly.
- **Delete the row on failure** — makes retry and inspection impossible.
- **Add more statuses** — violates the contract.
