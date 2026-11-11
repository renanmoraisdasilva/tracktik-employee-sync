# 001 — Data Model

- **Status:** Accepted
- **Date:** 2026-11-11
- **Spec:** [`spec.md`](spec.md)
- **Provider:** SQL Server 2022 through EF Core 10

---

## 1. `Employees` table

| Column | SQL type | Null | Default | Notes |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No | `NEWSEQUENTIALID()` / app | Primary key. The selected design uses `uniqueidentifier`. |
| `Provider` | `nvarchar(32)` | No | — | `provider1` / `provider2`, normalised lowercase. |
| `ProviderEmployeeId` | `nvarchar(128)` | No | — | Source ID. Part of the unique key. |
| `TrackTikEmployeeId` | `nvarchar(160)` | No | — | `{Provider}-{ProviderEmployeeId}`. |
| `FirstName` | `nvarchar(100)` | No | — | TrackTik required. |
| `LastName` | `nvarchar(100)` | No | — | TrackTik required. |
| `Email` | `nvarchar(320)` | No | — | Valid email; TrackTik required. |
| `PhoneNumber` | `nvarchar(40)` | Yes | — | Optional. |
| `Position` | `nvarchar(120)` | Yes | — | Optional. |
| `Department` | `nvarchar(120)` | Yes | — | Optional. |
| `StartDate` | `date` | Yes | — | Optional; `DateOnly` in .NET. |
| `Status` | `nvarchar(16)` | No | — | `active`, `inactive`, `terminated`. |
| `SyncStatus` | `nvarchar(16)` | No | `pending` | `pending`, `synced`, `failed`. |
| `Attempts` | `int` | No | `0` | Monotonic lifetime counter. |
| `LastError` | `nvarchar(2000)` | Yes | — | Safe, truncated message; no secrets. |
| `LastAttemptAt` | `datetimeoffset(7)` | Yes | — | UTC. |
| `ExistsInTrackTik` | `bit` | No | `0` | Drives POST vs PUT. |
| `CreatedAt` | `datetimeoffset(7)` | No | — | UTC. |
| `UpdatedAt` | `datetimeoffset(7)` | No | — | UTC. |
| `SyncedAt` | `datetimeoffset(7)` | Yes | — | Last successful sync. |
| `RowVersion` | `rowversion` | No | — | Optional optimistic concurrency token. |

> The timestamps are modelled as `DateTimeOffset` and mapped to
> `datetimeoffset(7)`. If you prefer UTC `DateTime`, use `datetime2(7)` and keep
> the convention consistent. Do not mix both.

## 2. Indexes and constraints

### Unique index

```sql
CREATE UNIQUE INDEX UX_Employees_Provider_ProviderEmployeeId
ON dbo.Employees (Provider, ProviderEmployeeId);
```

This is the rule that prevents duplicates. It also makes the upsert race safe:
a duplicate-key error is retried as an update.

### Check constraints

```sql
ALTER TABLE dbo.Employees
ADD CONSTRAINT CK_Employees_SyncStatus
CHECK (SyncStatus IN ('pending', 'synced', 'failed'));

ALTER TABLE dbo.Employees
ADD CONSTRAINT CK_Employees_Status
CHECK (Status IN ('active', 'inactive', 'terminated'));
```

### Supporting index

```sql
CREATE INDEX IX_Employees_SyncStatus
ON dbo.Employees (SyncStatus);
```

Used by the report query and by failure inspection.

## 3. Enums

### `SyncStatus`

| Value | Meaning |
|---|---|
| `pending` | Stored and a sync is in progress or about to start. |
| `synced` | TrackTik accepted the create/update and the status was committed. |
| `failed` | The forward attempt failed; the row is kept for retry. |

### `EmployeeStatus`

| Value | Meaning |
|---|---|
| `active` | Currently employed. |
| `inactive` | On leave or otherwise not active. |
| `terminated` | No longer employed. |

Status values are stored as strings for readability and check constraints.

## 4. `ExistsInTrackTik`

| Current flag | Last outcome | Next call | After success | After failure |
|---|---|---|---|---|
| `false` | Never created, or create failed | `POST` | `true` | `false` |
| `true` | Created at least once | `PUT` | `true` | `true` |

Never read the mock's `GET` to decide. The mock always returns a sample record.

## 5. Concurrency

- The unique index is the primary protection against duplicates.
- On a duplicate-key race during insert, re-read the row and retry as an update.
- `RowVersion` is optional. If used, a concurrent update can be detected and
  retried or returned as `409`. The v1 behavior is **unique index + retry**,
  not `409`.
- Last-write-wins is acceptable for this challenge; a lost update is possible
  under extreme concurrency. Document it.

## 6. Migrations

- The initial migration is committed in
  `src/EmployeeSync.Infrastructure/Migrations/`.
- Compose applies migrations at startup with retry when
  `Database__AutoMigrate=true`.
- Manual command:

```bash
dotnet ef database update \
  --project src/EmployeeSync.Infrastructure \
  --startup-project src/EmployeeSync.Api
```

- Do not use `EnsureCreated()`; use migrations.

## 7. Report query

```sql
SELECT SyncStatus, COUNT(*) AS EmployeeCount
FROM dbo.Employees
GROUP BY SyncStatus
ORDER BY SyncStatus;
```

The optional stored procedure is
`dbo.usp_EmployeeCountsBySyncStatus`. The API exposes the same data at
`GET /api/v1/reports/employees-by-sync-status`.

## 8. Deferred tables

### `SyncAttempts` (optional, not v1)

One row per attempt: employee ID, attempt number, started/finished timestamps,
outcome, error and duration. Useful for audit; adds a table and writes.

### `OutboxMessages` (stretch, not v1)

| Column | Purpose |
|---|---|
| `Id` | Message ID. |
| `Type` | `EmployeeSynced`. |
| `Payload` | JSON event body. |
| `OccurredAt` | UTC. |
| `ProcessedAt` | Null until published. |
| `Attempts` | Publish attempts. |

Written in the same transaction as the `synced` status, then published by a
background worker. This is the reliable alternative to D-027.

## 9. PII and logging

The table contains names, emails and phone numbers. Log identifiers and
outcomes, not full payloads. Do not log tokens, secrets or the full row. If an
error message contains upstream details, keep it short and safe.

## 10. Connection strings

- Host run: `Server=localhost,1433;Database=EmployeeSync;...`
- Compose API: `Server=sqlserver,1433;Database=EmployeeSync;...`
- Use `Encrypt=True;TrustServerCertificate=True` only for local Docker.
- Never commit the real SA password.
