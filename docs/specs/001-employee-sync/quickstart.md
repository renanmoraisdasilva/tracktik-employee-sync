# 001 — Quick Start

The reviewer path. The full instructions are in the root `README.md`.

## 1. Prerequisites

- .NET 10 SDK
- Docker Desktop with Compose v2
- The TrackTik test credentials from the challenge package

## 2. Configure

```bash
cp .env.example .env
```

> Windows PowerShell: use `Copy-Item .env.example .env`; the HTTP examples use
> `curl.exe`.

Edit `.env`:

```text
MSSQL_SA_PASSWORD=CHANGE_ME_Local_123!
TRACKTIK_BASE_URL=https://.../prod/v1
TRACKTIK_TOKEN_URL=https://.../prod/oauth/token
TRACKTIK_CLIENT_ID=YOUR_CLIENT_ID
TRACKTIK_CLIENT_SECRET=YOUR_CLIENT_SECRET
```

Never commit `.env` or `oauth-credentials.json`.

## 3. Run

```bash
docker compose up --build
```

Wait for the SQL Server health check, then open:

- API: <http://localhost:8080>
- OpenAPI: <http://localhost:8080/openapi/v1.json>
- Scalar: <http://localhost:8080/scalar/v1>
- Health: <http://localhost:8080/health>

## 4. Send a Provider 1 employee

```bash
curl.exe -X POST "http://localhost:8080/api/v1/providers/provider1/employees" \
  -H "Content-Type: application/json" \
  -d "@provider1-one.json"
```

Expected: `201 Created` on the first insert, with a `Location` header and
`syncStatus: "synced"` in the body.

Send the same payload again: `200 OK`, same row, `PUT` to TrackTik.

## 5. Read it back

```bash
curl.exe "http://localhost:8080/api/v1/employees/provider1/P1_001"
```

## 6. Run the tests

```bash
dotnet test
dotnet test tests/EmployeeSync.Testcontainers   # Docker required
```

## 7. Apply migrations manually (optional)

```bash
dotnet ef database update \
  --project src/EmployeeSync.Infrastructure \
  --startup-project src/EmployeeSync.Api
```

Compose applies migrations automatically when `Database__AutoMigrate=true`.

## 8. Force a failure (manual check)

Point `TrackTik__BaseUrl` at a host that returns 500, or stop the mock, then
POST a new employee.

Expected:

- `502` `application/problem+json`;
- `syncStatus: "failed"`;
- the row remains in the database;
- no `EmployeeSynced` event.

Restore the URL and POST the same employee again to see the retry.
