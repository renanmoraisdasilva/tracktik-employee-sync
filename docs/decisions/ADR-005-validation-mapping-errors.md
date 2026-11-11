# ADR-005 — FluentValidation, manual mapping and a Result-based error model

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** Renan Silva
- **Related:** `docs/specs/001-employee-sync/spec.md` (FR-003, FR-004, FR-014)

## Context

The two providers have different shapes, and Provider 2 is nested. The provider
JSON Schemas are looser than TrackTik: a payload can pass the schema and still
lack a field TrackTik requires. Expected failures (validation, not found,
conflict, sync failure) need a consistent HTTP representation.

## Decision

- Use **FluentValidation** with a validator per provider, plus an outbound
  validator for the TrackTik-required fields.
- Reject payloads that cannot produce a valid TrackTik employee with `400`
  before any write, even when the provider schema allows the field to be
  missing.
- Use **manual mapper classes** for the two provider shapes.
- Use a small **`Result` / `Result<T>`** for expected failures; throw exceptions
  only for unexpected bugs.
- Map results to **`ProblemDetails` / `ValidationProblemDetails`** in the API.
- Error statuses:
  - `400` validation or malformed JSON;
  - `404` unknown provider or missing employee;
  - `502` TrackTik failure after persistence;
  - `500` unexpected error, including a status update failure after TrackTik
    success.

## Consequences

- Validation rules are explicit, testable and independent of the controller.
- The mapper is easy to review and matches the golden sample output.
- Expected failures are data, not exceptions; the 502 body can carry the
  persisted employee and sync metadata.
- There is a small amount of custom `Result` plumbing.
- The service is stricter than the provider schema by design; this is documented
  in the README and spec.

## Alternatives considered

- **DataAnnotations only** — weak for nested/conditional rules.
- **JSON Schema validation** — strong fidelity, but an extra package and more
  error-mapping work; it would still need outbound TrackTik rules.
- **AutoMapper / Mapster** — unnecessary for a small, explicit table.
- **Exceptions for every expected failure** — makes control flow and tests less
  clear.
- **Return TrackTik's upstream status code** — violates the 502 contract.
