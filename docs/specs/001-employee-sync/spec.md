# 001 — Employee Sync Service Specification

- **Status:** Accepted
- **Date:** 2026-11-11
- **Owner:** Renan Silva
- **Feature ID:** `001-employee-sync`
- **Challenge:** TrackTik .NET Employee Sync Service (test ID `55bb3eca-352c-4516-b810-16570592d8b1`)
- **Decisions:** `docs/decisions/`, `DECISIONS.md` in the planning package
- **Contract:** [`contracts/openapi.yaml`](contracts/openapi.yaml)
- **Data model:** [`data-model.md`](data-model.md)
- **Plan:** [`plan.md`](plan.md)
- **Tasks:** [`tasks.md`](tasks.md)

---

## 1. Problem

TrackTik customers keep workforce data in external identity providers. Those
providers push employee records to a bridge service. The bridge must store each
record, translate it into TrackTik's employee shape, and forward it to TrackTik.
The external call is unreliable, so the bridge must keep a durable record of
every attempt, expose the sync state, and allow a retry without duplicating the
employee.

The challenge is intentionally small. The evaluation cares about clear
behaviour, sensible structure, tests and ease of running more than the number of
features.

## 2. Goals

- Accept employee records from two providers with different JSON shapes.
- Validate and map them deterministically into TrackTik employees.
- Store them in SQL Server through EF Core with committed migrations.
- Persist intent before calling TrackTik and never lose a failed record.
- Obtain and cache an OAuth2 token; call TrackTik with `POST` or `PUT`.
- Record `pending`, `synced` or `failed`, with attempts and error details.
- Return consistent `ProblemDetails` for every error.
- Publish `EmployeeSynced` only after a successful sync is committed.
- Expose an OpenAPI document and a reviewer-friendly README.
- Ship unit tests, `WebApplicationFactory` integration tests and a Docker
  Compose environment.

## 3. Non-goals

- A UI, login system or multi-tenant authorisation model.
- A full HR platform or a generic employee directory.
- Merging employees across providers by email.
- Exactly-once event delivery in v1 (an outbox is a documented stretch goal).
- Automatic retries of TrackTik 5xx/429 (resubmission is the retry mechanism).
- Production-grade observability, inbound API rate limiting or secret
  management.
- A public API for providers other than `provider1` and `provider2`.

## 4. Actors

| Actor | Responsibility |
|---|---|
| Provider 1 | Sends flat snake_case employee payloads. |
| Provider 2 | Sends nested `personal_info` / `work_info` payloads. |
| Reviewer / operator | Runs the API, sends samples, inspects the database and sync state. |
| TrackTik mock | Accepts employee create/update calls with an OAuth2 bearer token. |

## 5. Functional requirements

### FR-001 — Accept two provider shapes

The API must accept `POST /api/v1/providers/{provider}/employees` for
`provider1` and `provider2`.

- `provider1` is flat snake_case with `emp_id`, `first_name`, `last_name`,
  `email_address`, optional `phone`, `job_title`, `dept`, `hire_date`,
  `employment_status`.
- `provider2` is nested with `employee_number`, `personal_info` and `work_info`.
- The provider name is case-insensitive and is normalised to lowercase.
- An unknown provider returns `404 ProblemDetails` and does not touch the
  database.

### FR-002 — Identity

The unique identity is `(provider, providerEmployeeId)`.

- Provider 1: `providerEmployeeId = emp_id`.
- Provider 2: `providerEmployeeId = employee_number`.
- The TrackTik `employeeId` is `{provider}-{providerEmployeeId}`.
- Two providers with the same email are two distinct employees.
- `providerEmployeeId` is case-sensitive: `P1_001` and `p1_001` are different
  identities. SQL Server uses a binary collation on the column; SQLite is
  case-sensitive by default.
- A unique database index enforces the identity pair.

### FR-003 — Validation

Incoming payloads must be validated before mapping and before any database
write.

- Required provider fields must be present, non-null and non-empty.
- Email must be a valid format.
- Dates must be valid `yyyy-MM-dd` values.
- Status values must be from the documented enums.
- Unknown JSON properties are ignored.
- A payload that passes the provider schema but lacks a TrackTik-required field
  (`firstName`, `lastName`, `email`, `status`) is rejected with `400` because it
  cannot produce a valid TrackTik employee.
- A validation failure returns `400 ValidationProblemDetails`; nothing is
  persisted and nothing is sent to TrackTik.

### FR-004 — Mapping

The service must map both provider shapes to the TrackTik employee:

| TrackTik | Provider 1 | Provider 2 |
|---|---|---|
| `employeeId` | `provider1-{emp_id}` | `provider2-{employee_number}` |
| `firstName` | `first_name` | `personal_info.given_name` |
| `lastName` | `last_name` | `personal_info.family_name` |
| `email` | `email_address` | `personal_info.email` |
| `phoneNumber` | `phone` | `personal_info.mobile` |
| `position` | `job_title` | `work_info.role` |
| `department` | `dept` | `work_info.division` |
| `startDate` | `hire_date` | `work_info.start_date` |
| `status` | `employment_status` | `employed → active`, `on_leave → inactive`, `terminated → terminated` |

Mapping is deterministic and unit-tested against
`sample-data/expected-tracktik-employees.json`.

### FR-005 — Persist before forward

The service must:

1. validate;
2. map;
3. insert or update the row with `syncStatus = pending`;
4. commit;
5. call TrackTik;
6. update the row to `synced` or `failed` and commit again.

The first commit is mandatory. A forward failure must not roll back the row.

### FR-006 — OAuth2 and token cache

- Use the OAuth2 client credentials flow.
- Request a token from the configured token URL with `grant_type`,
  `client_id` and `client_secret`.
- Omit `scope` unless a specific scope is required.
- Cache the token in memory until shortly before `expires_in` (60-second skew).
- Use a single-flight refresh so concurrent callers do not request multiple
  tokens.
- On `401`, invalidate the token, refresh once and retry the employee call once.
- A token-endpoint timeout or transport failure is a `TrackTikException`, so
  FR-009 applies; caller cancellation is not converted into a sync failure.
- Never log the client secret, access token or `Authorization` header.

### FR-007 — POST vs PUT

- The service persists `ExistsInTrackTik`.
- It starts `false`.
- A successful `POST` sets it to `true`.
- A successful `PUT` keeps it `true`.
- A failed `POST` keeps it `false`; a failed `PUT` keeps it `true`.
- The next sync uses `POST` when the flag is `false`, otherwise `PUT`.
- The mock's `GET` is never used to decide; it is stateless and always returns a
  sample.
- If a real `POST` returns `409`, treat it as "already exists" and retry with
  `PUT` (robustness; the mock does not do this).

### FR-008 — Sync status and metadata

Only three sync statuses exist:

- `pending` — the row is stored and a sync is in progress or about to start.
- `synced` — TrackTik accepted the create/update and the status was committed.
- `failed` — the forward attempt failed; the row and error are kept for retry.

The row records:

- `attempts` — monotonic lifetime counter, incremented once per outbound
  attempt, including token failures; never reset;
- `lastError` — safe error message, cleared on success;
- `lastAttemptAt` — UTC timestamp of the last attempt;
- `ExistsInTrackTik` — see FR-007.

### FR-009 — TrackTik failure

If the token request or employee call fails after the row is persisted:

- update the row to `failed`;
- increment `attempts`;
- record a safe `lastError`;
- record `lastAttemptAt`;
- keep the row (no rollback);
- do not publish `EmployeeSynced`;
- return `502` `ProblemDetails` with `syncStatus`, `attempts`, `lastError` and
  `lastAttemptAt`.

The upstream status code is not passed through. TrackTik `401`, `403`, `429` and
`5xx` all become a `502` from this API (after the optional 401 token refresh).

### FR-010 — Resubmission

A resubmission for the same identity:

- updates the stored employee fields;
- sets `syncStatus = pending`;
- retries the sync;
- uses `PUT` when `ExistsInTrackTik` is `true`, otherwise `POST`;
- never inserts a second row.

This applies to rows that previously `failed` and rows that were already
`synced`.

### FR-011 — EmployeeSynced event

- Published only after a successful forward and the `synced` commit.
- Never published on validation failure or sync failure.
- Published once per successful sync, including a successful resubmission.
- Payload: `eventId`, `eventType`, `occurredAt`, `provider`,
  `providerEmployeeId`, `employeeId` (TrackTik), `attempts`.
- No full PII in the event.
- In-memory publisher by default; NATS in Compose.
- If the publisher fails after the commit, log the failure and still return
  success; an outbox is the next reliability step.

### FR-012 — GET employee

`GET /api/v1/employees/{provider}/{providerEmployeeId}` returns the stored
employee, the derived TrackTik `employeeId`, and the sync state:

`provider`, `providerEmployeeId`, `employeeId`, `firstName`, `lastName`,
`email`, `phoneNumber`, `position`, `department`, `startDate`, `status`,
`syncStatus`, `attempts`, `lastError`, `lastAttemptAt`, `createdAt`, `updatedAt`.

Unknown provider or missing employee returns `404 ProblemDetails`.

### FR-013 — HTTP success semantics

- First insert of an identity: `201 Created` with a `Location` header pointing
  at the GET route.
- Resubmission/update: `200 OK`.
- Both return the same stored-record body.

### FR-014 — Error model

Every error response is `application/problem+json`:

- `400` — validation or malformed JSON; nothing persisted or sent.
- `404` — unknown provider or missing employee.
- `502` — TrackTik integration failure after persistence.
- `500` — unexpected server error, including a status update failure after
  TrackTik success.
- No stack traces, secrets or full PII in the response.

### FR-015 — Report query (bonus)

- Provide a T-SQL script or stored procedure that returns employee counts by
  sync status.
- Optionally expose it at
  `GET /api/v1/reports/employees-by-sync-status`.
- The query is read-only and uses parameterised SQL/EF Core.

### FR-016 — OpenAPI and docs

- Serve an OpenAPI document at `/openapi/v1.json`.
- Serve the Scalar UI at `/scalar/v1`.
- Document both mandatory endpoints, both provider request shapes, the stored
  employee response and the 400/404/502 responses.
- The served POST body is `oneOf` `Provider1Employee` / `Provider2Employee`;
  success responses are `application/json` and errors are
  `application/problem+json`.
- An integration test asserts the served document matches the committed
  contract.
- Keep the `/api/v1` prefix.

## 6. Behavioural contract summary

```text
validate → map → persist pending → forward → commit synced/failed
```

```text
new request → pending ──success──→ synced
                    └──failure──→ failed

synced or failed ──resubmission──→ pending
```

| Situation | Stored row | HTTP | Event |
|---|---|---|---|
| Validation failure | none | 400 | no |
| Unknown provider | none | 404 | no |
| First insert, sync success | `synced` | 201 | yes |
| First insert, sync failure | `failed` | 502 | no |
| Resubmission, sync success | `synced` | 200 | yes |
| Resubmission, sync failure | `failed` | 502 | no |
| DB status update fails after TrackTik success | `pending`/stale | 500 | no |
| Event publish fails after commit | `synced` | 200 | missing (logged) |

## 7. Data requirements

See [`data-model.md`](data-model.md). The essential requirements:

- `Employees` table with the mapped fields and sync metadata.
- Unique index on `(Provider, ProviderEmployeeId)`.
- `SyncStatus` and `Status` check constraints.
- `ExistsInTrackTik` boolean.
- UTC `DateTimeOffset` timestamps and SQL `date` for `StartDate`.
- Committed initial migration.
- Optional `SyncAttempts`/`OutboxMessages` are deferred.

## 8. Security requirements

- Never commit `oauth-credentials.json`, the client secret or real passwords.
- Credentials come from environment variables, user-secrets or an untracked
  `.env`.
- Committed configuration uses placeholders only.
- EF Core parameterisation; no string-built SQL.
- Do not log tokens, secrets or full employee payloads.
- Do not send the bearer token to a host other than the configured TrackTik
  host.

## 9. Acceptance criteria

### AC-001 — Provider 1 mapping

Given the Provider 1 sample, the mapped employee equals the expected TrackTik
record, including `employeeId = provider1-P1_001`.

### AC-002 — Provider 2 mapping

Given the Provider 2 sample, the mapped employee equals the expected TrackTik
record, including `employeeId = provider2-P2_001` and the status mapping.

### AC-003 — Identity

Two providers with the same email produce two rows and two TrackTik employees.

### AC-004 — Validation

An invalid payload returns 400 `ProblemDetails`, stores nothing and makes no
TrackTik call.

### AC-005 — First success

A valid first POST stores `pending`, calls TrackTik, commits `synced`, returns
201 with `Location`, and publishes one `EmployeeSynced`.

### AC-006 — First failure

A forced TrackTik 500 stores `failed`, returns 502 with the sync fields, keeps
the row and publishes no event.

### AC-007 — Resubmission after failure

The same identity updates the existing row, retries with POST (never created),
returns 200 on success and becomes `synced`; exactly one row exists.

### AC-008 — Resubmission after success

The same identity updates the existing row, returns 200 and uses PUT; exactly
one row exists; a new event is published on success.

### AC-009 — Token reuse

Two calls within the token lifetime make one token request.

### AC-010 — Token expiry

A call after the expiry skew requests a new token.

### AC-011 — 401 refresh

A 401 from TrackTik invalidates the token, refreshes once and retries the
employee call once.

### AC-012 — Unknown provider

POST and GET for an unknown provider return 404 `ProblemDetails`.

### AC-013 — GET missing

GET for a missing employee returns 404 `ProblemDetails`.

### AC-014 — OpenAPI

The OpenAPI document is reachable and lists both mandatory endpoints and the
error responses.

### AC-015 — Migrations

A clean database can be created from the committed migrations.

### AC-016 — Docker Compose

`docker compose up --build` starts the API, SQL Server and NATS; the API becomes
healthy and can serve the sample POST.

### AC-017 — Report query

The report returns counts grouped by `pending`, `synced` and `failed`.

### AC-018 — Testcontainers

The opt-in `tests/EmployeeSync.Testcontainers` project runs the integration
suite against a real SQL Server container.

### AC-019 — CI

CI restores, builds, runs the unit tests and the secret scan successfully.

### AC-020 — README

A reviewer can clone the repo, configure credentials, start Compose, apply
migrations, run the tests and send the sample payloads using only the README.

## 10. Out of scope

- Outbox (stretch; see ADR-007).
- Auth/API keys on provider endpoints.
- Automatic TrackTik retries beyond the 401 refresh.
- OpenTelemetry, Serilog, dashboards.
- Multi-tenancy.
- More than two providers.
- A UI.
- CI integration/Testcontainers runs.

## 11. Open questions

None. The decisions are recorded in the planning package's `DECISIONS.md` and in
`docs/decisions/`. If an implementation detail changes a decision, update the
decision record and this spec in the same change.
