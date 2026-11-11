# ADR-001 — Spec-driven documentation without the Spec Kit CLI or Trace Docs

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** Renan Silva
- **Related:** `docs/specs/001-employee-sync/`, planning package `DECISIONS.md`

## Context

The challenge rewards clear behaviour, sensible structure, tests and ease of
running. It is a small service with two mandatory endpoints, but the user wants
a spec-driven workflow and a well-organized repository.

Two tools were considered:

- **GitHub Spec Kit** — an open-source toolkit (`github/spec-kit`) with a
  `specify` CLI, Python 3.11+/uv, `.specify/` scaffolding and slash commands for
  AI agents. It produces `spec.md`, `plan.md`, `tasks.md` and related artifacts.
- **Trace Docs** — the user's behavior-traceability CLI. It documents
  **implemented behavior only** and requires Node 22, JUnit XML and lcov.

## Decision

Use the **shape** of Spec Kit without the CLI:

- `docs/specs/001-employee-sync/spec.md`
- `plan.md`
- `tasks.md`
- `research.md`
- `data-model.md`
- `contracts/openapi.yaml`
- `quickstart.md`
- `docs/decisions/ADR-*.md`
- `docs/architecture.md`
- `docs/testing.md`

Do not add `.specify/`, Python/uv, `package.json`, `trace.config.yaml` or
`docs/features/` rule gates.

## Consequences

- The repository reads as a mature spec-driven project without a second
  toolchain.
- The reviewer needs only a Markdown reader.
- The docs are maintained by hand; the plan and tasks must be updated as the
  implementation changes.
- There is no automated rule-to-test traceability. The tests and the spec are
  linked by naming and review, not by a CLI gate.

## Alternatives considered

- **Spec Kit CLI** — rejected: Python/uv and `.specify/` scaffolding are more
  ceremony than a two-endpoint service needs.
- **Trace Docs as a submission gate** — rejected: it is a behavior-traceability
  tool, not a spec tool; it adds Node 22, JUnit and lcov to a .NET challenge.
- **README + ADRs only** — rejected: weaker spec-driven signal and less useful
  to the reviewer.
