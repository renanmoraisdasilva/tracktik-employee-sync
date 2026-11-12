# ADR-008 — Upstream failures are recorded, not retried

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** Renan Silva
- **Related:** `docs/specs/001-employee-sync/spec.md` (FR-009), ADR-003,
  ADR-004, ADR-007

## Context

The mock TrackTik API allows 100 requests per minute and 1,000 per hour per
client. It returns `429` when a client exceeds either limit, and it can also
return `401`, `403`, `500` or time out. The challenge defines resubmission as the
retry mechanism and requires a `502` with the persisted sync state after a
forward failure.

Automatic retries are tempting, but they interact badly with this contract.
`attempts` is defined as one increment per logical sync attempt, so a general
retry loop makes the counter ambiguous. Retrying a rate-limited call immediately
can also extend the rate-limit window.

## Decision

- Treat every upstream token or employee-call failure as one failed attempt:
  token-endpoint timeouts, `401` after the one refresh, `403`, `404`, `409`
  after the PUT fallback, `429`, `5xx` and timeouts.
- Record `failed`, increment `attempts`, store a safe `lastError` and
  `lastAttemptAt`, keep the row, publish no event and return `502 ProblemDetails`.
- Do not retry `429` or `5xx` automatically. Do not parse `Retry-After`.
- Keep exactly one exception: a `401` invalidates the cached token, refreshes
  once and retries the employee call once.
- A 401 refresh and the 409→PUT fallback are internal to the same sync attempt;
  they do not increment `attempts` again.
- Treat the mock's rate limits as a property of the mock, not as a requirement
  on this API. This API does not rate-limit its own callers in v1.

## Consequences

- `attempts` stays a truthful count of logical sync attempts; it is not an
  HTTP-request counter.
- A caller that resubmits immediately after a `429` can hit the limit again. The
  API surfaces the failure instead of hiding it behind a retry loop.
- The reviewer sees the whole failure contract in one request: `502`, a `failed`
  row and no event.
- A production deployment would add an outbound resilience policy (exponential
  backoff with jitter and `Retry-After`) or a queue between persistence and
  forwarding. Both are documented as next steps, not claimed in v1.

## Alternatives considered

- **Polly retry with backoff** — better for transient faults, but it changes
  `attempts` semantics and can amplify a rate limit.
- **Honour `Retry-After` inline** — keeps the HTTP request open for an unknown
  time and complicates the timeout budget.
- **Return the upstream status code** — breaks the `502` contract and leaks
  TrackTik's error taxonomy to providers.
- **Rate-limit the inbound API** — not required; it would add reviewer friction
  without improving the TrackTik integration.
- **Queue the forward and return `202`** — a larger design that changes the
  synchronous contract.
