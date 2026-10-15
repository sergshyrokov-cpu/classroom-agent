---
artifact_type: database_design
story: US-048
version: 1
status: DRAFT
created_at: 2026-10-15T19:20:00Z
updated_at: 2026-10-15T19:20:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-048-spec.md
    version: 1
  - path: docs/designs/api/US-048-api-design.md
    version: 1
  - path: docs/decisions/US-048-open-decisions.md
    version: 1
supersedes: null
---

# US-048 Database Design — A Dean's failed-attempt count starts again when a lockout begins

**Verdict: NOT_APPLICABLE.** This Story changes no schema, so no entity model is
produced.

**There is deliberately no `docs/designs/database/US-048-entity-model.md`.** Its
absence is this stage's recorded decision, not a missing artifact. Downstream
stages that list `database_design` or `entity_model` among their inputs read
this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when the approved Specification
states the Story does not change persistence behaviour. The approved
Specification (v1) states it in §10: "no table, column, index, constraint or
migration; the same `access_failed_count` and `lockout_end` columns hold a
different value. The only persistence-layer code is the conflict translation
fully specified in FR-005." §1 repeats: "No … schema or migration changes."

## 2. What stays as it is

- `app_user.access_failed_count` (required, default 0) and the existing
  `ck_app_user` check that it is never negative; `app_user.lockout_end`.
- `app_user.concurrency_stamp` as the EF Core concurrency token
  (`IsConcurrencyToken()`); every `AppUser` mutator already rotates it, which is
  what makes the conflicts of FR-003 and AC-005…AC-007 observable.
- No migration (PC-2 is satisfied vacuously: no entity change).

## 3. Persistence-layer code, already specified

The conflict translation in `UnitOfWork` (FR-005) is code, not schema: on a
`DbUpdateConcurrencyException` whose entries include an `AppUser`, remove the
failed `AppUser` entry and the staged audit rows from the tracker and throw an
exception declared in `Application`; every other failure keeps its current
handling. It follows the US-032 `MeetingCodeLink` precedent already in
`UnitOfWork`. The exception's name is an implementation detail.

## 4. Existing data

No data fix. A row left at a count of five or more by the current defect locks
once more on its next wrong password and then returns to zero (spec review M-1,
accepted at `HUMAN_SPEC_APPROVAL`). No installation is in production yet.

## 5. Delegation

Nothing was delegated; `AppUserConfiguration`, `AppUser` and `UnitOfWork` were
read directly during this Story's review.
