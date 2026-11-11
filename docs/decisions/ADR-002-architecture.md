# ADR-002 — Controllers, four projects and hand-rolled CQRS

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** Renan Silva
- **Related:** `docs/specs/001-employee-sync/spec.md`, `docs/architecture.md`

## Context

The challenge allows controllers or minimal APIs and suggests a lightweight
API / Application / Domain / Infrastructure structure. The user wants the CQRS
bonus without turning the solution into a framework showcase.

## Decision

- Use **ASP.NET Core controllers**.
- Use **four projects**: `EmployeeSync.Api`, `EmployeeSync.Application`,
  `EmployeeSync.Domain`, `EmployeeSync.Infrastructure`.
- Use **hand-rolled CQRS**: `ICommandHandler<TCommand, TResult>` and
  `IQueryHandler<TQuery, TResult>`, registered in DI.
- Use a **thin repository interface** and a **Result** type. No MediatR, no
  generic repository, no unit-of-work abstraction.
- Use FluentValidation for input rules and manual mapping for the provider
  shapes.

## Consequences

- The API surface is conventional and predictable for .NET reviewers.
- `[ApiController]`, filters and `[ProducesResponseType]` make validation and
  OpenAPI straightforward.
- The Application layer stays free of EF Core and HTTP.
- There are more files than a single-project solution; the boundaries are worth
  it for the architecture criterion and the CQRS bonus.
- The handlers are explicit and easy to test; there is no pipeline magic.
- The OpenAPI document is generated and then normalized by one operation
  transformer: the provider POST body is `oneOf` `Provider1Employee` /
  `Provider2Employee`, and success/error media types are `application/json` /
  `application/problem+json`. A contract test pins this so the served document
  cannot drift from the committed contract.

## Alternatives considered

- **Minimal APIs** — viable and concise, but more explicit validation/OpenAPI
  wiring and less conventional for this evaluator.
- **MediatR** — adds a dependency and indirection for two use cases.
- **Single project with folders** — weaker architecture signal.
- **Generic repository + unit of work** — over-abstraction for one aggregate.
