# Decision Records

Consequential choices, with context, alternatives and consequences.

| ADR | Decision |
|---|---|
| [ADR-001](ADR-001-spec-workflow.md) | Spec-driven docs without the Spec Kit CLI or Trace Docs |
| [ADR-002](ADR-002-architecture.md) | Controllers, four projects and hand-rolled CQRS |
| [ADR-003](ADR-003-persist-before-forward.md) | Persist before forwarding; keep failed rows |
| [ADR-004](ADR-004-identity-and-upsert.md) | Composite identity, unique upsert and POST/PUT |
| [ADR-005](ADR-005-validation-mapping-errors.md) | FluentValidation, manual mapping and a Result-based error model |
| [ADR-006](ADR-006-testing-and-ci.md) | Contract-first tests, SQLite by default, Testcontainers opt-in |
| [ADR-007](ADR-007-bonus-scope.md) | All four bonus items, gated; outbox deferred |
| [ADR-008](ADR-008-upstream-failures.md) | Upstream failures are recorded and resubmitted, not retried |

The complete decision log from the grilling session is in the planning package
(`test-package-guide/DECISIONS.md`). This folder contains the ADRs that belong
in the submission repository.
