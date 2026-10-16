---
artifact_type: database_design
story: US-053
version: 1
status: DRAFT
created_at: 2026-10-16T16:07:33Z
updated_at: 2026-10-16T16:07:33Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-053-spec.md
    version: 1
  - path: docs/designs/api/US-053-api-design.md
    version: 1
  - path: docs/decisions/US-053-open-decisions.md
    version: 1
supersedes: null
---

# US-053 Database Design — A failed synchronization run is always recorded as failed

**Verdict: NOT_APPLICABLE.** This Story changes no schema, so no entity model is
produced.

**There is deliberately no `docs/designs/database/US-053-entity-model.md`.** Its
absence is this stage's recorded decision, not a missing artifact — as for
US-048 and US-052. Downstream stages that list `database_design` or
`entity_model` among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when the approved Specification
states the Story does not change persistence behaviour. The approved
Specification (v1) states it:

- **§1** — "It changes no requirement, no endpoint, no schema and no
  user-visible text (OD-001 a)."
- **§9** — "No new NuGet package; no migration; no new translation key."

The failure path now writes the `sync_state` row the existing rules already
require (US-013 FR-006, US-017 FR-005/FR-006, US-031 FR-010, US-032 FR-006);
what is stored in which column is unchanged.

## 2. What stays as it is

- The `SyncState` entity, its columns (`Status`, `LastError`, `FailedStep`,
  `ProcessedCount`, `StartedAt`, `FinishedAt`, `LastSuccessfulRunAt`,
  `MeetLoadedUpTo`, `LastFullRunAt`) and their constraints, as earlier designs
  (US-013, US-017, US-018, US-031, US-032) set them. `LastError` and
  `FailedStep` stay stored by name (spec VR-001).
- Every table the run writes (courses, participants, memberships, course work,
  submissions, Meet meetings, meeting code links, audit events): no column,
  index or constraint changes.
- No migration (PC-2 is satisfied vacuously: no entity change). No data fix:
  a row left at `running` by the current defect is replaced by the next run
  (US-013 FR-010), and no installation is in production yet.

## 3. Persistence-layer code, already specified

The discard of staged, uncommitted changes before `FailRun` (spec FR-002) is
code, not schema: it is reached through the existing `IUnitOfWork` port and
implemented in `Infrastructure/Persistence/UnitOfWork.cs` (AD-3, AD-4). It must
keep the run's `SyncState` tracked, must not open a second `DbContext` without a
reason recorded in the implementation report (spec I-2), and leaves the
US-032/US-048 detaches as they are (FR-008, I-4). The method's name and shape
are the implementation's (I-2); the Story's tests reach it only through the run
(`RunSynchronizationUseCase.ExecuteAsync`), so the TEST_WRITING skeleton needs
no new signature from this design.

## 4. Test database

How a test makes PostgreSQL reject one write is the test design's choice
(spec I-5) — for example a constraint or trigger the test adds to its own
Testcontainers database. Such an object lives only in the test database and
never in a migration (spec §7: no test seam in production code).

## 5. Delegation

Nothing was delegated; the Specification, the API design record and the US-048
precedent were read directly.
