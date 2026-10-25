---
artifact_type: database_design
story: US-057
version: 2
status: DRAFT
created_at: 2026-10-25T19:06:00Z
updated_at: 2026-10-25T19:06:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-057-spec.md
    version: 2
  - path: docs/designs/api/US-057-api-design.md
    version: 2
  - path: docs/decisions/US-057-open-decisions.md
    version: 2
supersedes: docs/designs/database/US-057-db-design.md@v1
---

# US-057 Database Design — A course with points beyond the program's range is skipped, and the school scale never fails a report

**Version 2** — re-run after the Specification loop-back. v1 was BLOCKED (B-1, B-2, kept as
history in §4); the Owner resolved it by OD-001 (a), keep the schema, and spec v2 restated
AC-007 and AC-010 accordingly.

**Verdict: NOT_APPLICABLE.** The Story changes no persistence behaviour. No entity model is
produced: **there is deliberately no `docs/designs/database/US-057-entity-model.md`**.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when the approved Specification states the Story does
not change persistence behaviour. The approved Specification (v2) states it:

- **§9** — "no migration; no schema, endpoint or UI change."
- **§10** — "Any schema change: the non-negative constraints stay (OD-001); no migration."
- **OD-001 (a)** — the schema is kept.

## 2. What the Story touches, and why it is not a persistence change

| Spec element | Persistence effect |
|---|---|
| FR-001 range check in the reader | none — before any write |
| FR-002 course skipped; new outcome list beside `CoursesGone` | none — the list lives on the in-memory `SynchronizationRunOutcome` / run progress (`RunSynchronizationUseCase.cs:910`, `SynchronizationRunOutcome.cs:96`), read by the host for log lines; no column or table holds `CoursesGone` today, and the new list follows it |
| FR-002.1 nothing written for a skipped course | the existing course transaction is simply not entered |
| FR-002.3 not counted in `ProcessedCount` | existing `SyncState` column, same semantics |
| FR-003 log line 5135 | log file, not the database |
| FR-004 re-read next run | relies on the existing complete-read `updateTime`, written only by a complete read (US-018); no new state |
| FR-005 percent bounded | `ReportBuilder` computation over values already read |

## 3. Constraint check (the v1 gap)

The two constraints stay as they are:

| Constraint | Text |
|---|---|
| `ck_course_work_max_points_non_negative` | `max_points IS NULL OR max_points >= 0` |
| `ck_submission_grades_non_negative` | `(assigned_grade IS NULL OR assigned_grade >= 0) AND (draft_grade IS NULL OR draft_grade >= 0)` |

Every stored value spec v2 requires is accepted by them and by the unconstrained `numeric`
columns (US-055):

| AC | Stored values | Allowed |
|---|---|---|
| AC-001 … AC-003, AC-005, AC-009 | in-range non-negative values of the other courses / the earlier copy; skipped courses write nothing | yes |
| AC-002 | −1·10³⁰ draft grade — skipped before the transaction, never written | yes (nothing written) |
| AC-006 | 7·10²⁸ points, maximum 0.0001 | yes |
| AC-007a | 0 points, maximum 0.0001 | yes |
| AC-007b | no stored row (substituted journal port) | n/a |
| AC-008 | 6, 12, 0 of 12 | yes |
| AC-010 | 7.9·10²⁸ for `max_points`, `assigned_grade`, `draft_grade` | yes |

The spec's L-1 (an in-range negative value from Google is refused by these constraints and stops
the run) is unchanged by this Story and stays a possible `trebovaniya.md` §7 item for the Owner.

## 4. History — v1 findings (BLOCKED, resolved)

- **B-1** spec v1 AC-010 required storing −7.9·10²⁸; refused by the constraints. Resolved: spec v2
  AC-010 tests the upper bound only (OD-001 a).
- **B-2** spec v1 AC-007 required a stored grade of −7·10²⁸; impossible under
  `ck_submission_grades_non_negative`. Resolved: spec v2 AC-007 a) stored 0, b) negative pair via a
  substituted journal port (OD-001 a).
- v1 §4 observation (in-range negative from Google stops the run) became spec v2 L-1.

## 5. Delegation

Nothing was delegated. The constraints and `CoursesGone` usage were read directly
(`AddCourseListSupport.Designer.cs:664`, `:1220`; `RunSynchronizationUseCase.cs`;
`SynchronizationRunOutcome.cs`).
