# 001 — Research Notes

- **Status:** Accepted
- **Date:** 2026-11-11
- **Sources:** the challenge README, API documentation, submission instructions,
  the three JSON schemas and the sample data.

---

## 1. Challenge facts

| Fact | Value |
|---|---|
| Company | TrackTik |
| Test ID | `55bb3eca-352c-4516-b810-16570592d8b1` |
| Deadline | Monday, 16 November 2026, 11:59 PM ET |
| Stack | .NET 10, ASP.NET Core, SQL Server, EF Core |
| External API | Mock TrackTik REST API on AWS API Gateway |
| Auth | OAuth2 client credentials |
| Submission | Public GitHub repository URL through an OAuth-protected endpoint |

The package includes a test-only `oauth-credentials.json` that must never be
committed. The credentials are short-lived; contact the recruiter if they expire.

## 2. Mock TrackTik API

### URLs

| Purpose | URL |
|---|---|
| API root | `https://cnvc2vp9q8.execute-api.us-east-2.amazonaws.com/prod` |
| API base (v1) | `https://cnvc2vp9q8.execute-api.us-east-2.amazonaws.com/prod/v1` |
| Token URL | `https://cnvc2vp9q8.execute-api.us-east-2.amazonaws.com/prod/oauth/token` |
| Submission | `https://cnvc2vp9q8.execute-api.us-east-2.amazonaws.com/prod/submit` |

The token endpoint is **not** under `/v1`.

### OAuth2

- `POST` form-urlencoded: `grant_type=client_credentials`, `client_id`,
  `client_secret`.
- Omit `scope` to receive every scope registered for the client, or request
  `submissions:write` for the submission endpoint.
- Response: `access_token`, `token_type: Bearer`, `expires_in` (example 3600),
  `scope`.
- Cache the token until shortly before expiry; refresh on `401`.
- Scopes: `employees:read`, `employees:write`, `submissions:write`.
- Token errors: `400 invalid_request`, `400 unsupported_grant_type`,
  `400 invalid_scope`, `401 invalid_client`.

### Employee endpoints

| Method | Path | Scope | Success |
|---|---|---|---|
| `POST` | `/v1/employees` | `employees:write` | 201 |
| `PUT` | `/v1/employees/{employeeId}` | `employees:write` | 200 |
| `GET` | `/v1/employees/{employeeId}` | `employees:read` | 200 |

Success envelope:

```json
{
  "success": true,
  "data": {
    "id": "3f0c2a8e-1d4b-4a8f-9a57-0d7c9f1e2b11",
    "employeeId": "provider1-P1_001",
    "firstName": "Alice",
    "lastName": "Johnson",
    "email": "alice.johnson@provider1.com",
    "phoneNumber": "+1-555-0101",
    "position": "Security Officer",
    "department": "Security Operations",
    "startDate": "2024-01-15",
    "status": "active",
    "createdAt": "2024-01-15T10:00:00Z",
    "updatedAt": "2024-01-15T10:00:00Z"
  }
}
```

Error envelope:

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid employee data",
    "details": [
      { "field": "email", "message": "Invalid email format" }
    ]
  }
}
```

The mock validates `employeeId`, `firstName`, `lastName`, `email` (required,
valid format) and `status` (`active`, `inactive`, `terminated`).

### Mock quirks

- **Stateless:** it does not store data between calls.
- `GET` always returns a sample record, even if the employee was never created.
- `PUT` accepts any `employeeId`.
- Decide POST vs PUT from your own persisted `ExistsInTrackTik` flag.
- The mock does not simulate outages; test failures with a fake
  `HttpMessageHandler`.
- Rate limits: 100 requests/minute and 1,000/hour per client.
- Status codes: 200, 201, 400, 401, 403, 404, 429, 500.

## 3. Provider schemas

### Provider 1

Flat snake_case. Required: `emp_id`, `first_name`, `last_name`,
`email_address`. Optional: `phone`, `job_title`, `dept`, `hire_date`,
`employment_status` (`active`, `inactive`, `terminated`).

### Provider 2

Nested. Required: `employee_number`, `personal_info`, `work_info`. The schema
does not require the children, but a valid TrackTik employee needs the name,
email and status. `work_info.current_status` is `employed`, `on_leave` or
`terminated`.

### TrackTik

Camel case. Required: `employeeId`, `firstName`, `lastName`, `email`, `status`.
Optional: `phoneNumber`, `position`, `department`, `startDate`.

## 4. Mapping

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

## 5. Implications

1. **The mock cannot be the source of truth for existence.** Persist
   `ExistsInTrackTik`.
2. **The token must be cached.** A token per call violates the brief and risks
   rate limits.
3. **Persistence comes before the external call.** A failed call must leave a
   retryable row.
4. **A TrackTik failure is a 502 from our API.** Do not pass upstream 401/403/429
   through.
5. **The event is post-commit.** It is a fact about durable state.
6. **Provider schemas are looser than TrackTik.** Validate the fields that
   TrackTik requires before writing.

## 6. Assumptions

- The test credentials will be valid during implementation; if not, contact the
  recruiter.
- The mock behaves as documented; no hidden state.
- The reviewer has Docker for Compose and, optionally, Testcontainers.
- .NET 10 SDK is available.
- The submission repository can be public while the secret stays local.

## 7. Open questions

None for implementation. The behavioral and tooling decisions are recorded in
[`docs/decisions/`](../../decisions/).
