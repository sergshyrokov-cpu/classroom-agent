---
artifact_type: database_design
story: US-057
version: 1
status: DRAFT
created_at: 2026-10-25T18:22:12Z
updated_at: 2026-10-25T18:22:12Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-057-spec.md
    version: 1
  - path: docs/designs/api/US-057-api-design.md
    version: 1
  - path: docs/decisions/US-057-open-decisions.md
    version: 1
supersedes: null
---

# US-057 Database Design — A course with points beyond the program's range is skipped, and the school scale never fails a report

**Verdict: BLOCKED.** The approved Specification (v1) states no schema change
(§9 "no migration; no schema … change"), and the stage would otherwise be
NOT_APPLICABLE. But two of its Acceptance Criteria require stored values that
the **existing** schema refuses, so no design that keeps the schema unchanged
can satisfy them. The fix belongs to SPECIFICATION (the human decides whether to
reopen it; `DB_DESIGN` has no loop-back map). No entity model is produced.

## 1. The existing constraints

Both are in force since US-015 (migration `20260929052443_AddCourseWorkAndSubmissions`,
db-design US-015 §3, "Data sanity on external input (VR-001)") and were
re-pinned by US-055 (`GradeColumnsPersistenceTests.NegativeValues_AreStillRefusedByTheExistingChecks`):

| Constraint | Text |
|---|---|
| `ck_course_work_max_points_non_negative` | `max_points IS NULL OR max_points >= 0` |
| `ck_submission_grades_non_negative` | `(assigned_grade IS NULL OR assigned_grade >= 0) AND (draft_grade IS NULL OR draft_grade >= 0)` |

The Domain entities (`CourseWork`, `Submission`) do not check the sign; the
constraint is the only guard, so a negative value fails the course's
transaction at `SaveChanges`.

## 2. Blocking issues

### B-1 AC-010 (derived) is unsatisfiable for its negative half

Spec AC-010: "A coursework `maxPoints`, an `assignedGrade` and a `draftGrade`
just inside the range (7.9·10²⁸ and **−7.9·10²⁸**, synthetic) **are stored** as
today's conversion gives them, and the course is not skipped."

−7.9·10²⁸ is within the `decimal` range, so FR-001 does not skip the course; the
course's transaction then writes the value and PostgreSQL refuses it under the
constraints above. The run stops (`catch (Exception)` → `Unexpected`), the
opposite of "stored". The positive half (7.9·10²⁸) is satisfiable — the columns
are unconstrained `numeric` since US-055.

### B-2 AC-007's Given cannot be stored

Spec AC-007 (= Story AC-007): "a stored grade of −7·10²⁸ against a maximum of
0.0001 … the on-screen report and the export both show the scale label for 0 %".
A submission row with a negative `assigned_grade` (or `draft_grade`) cannot
exist (`ck_submission_grades_non_negative`), so the criterion cannot be set up
through the database the report reads. VR-002's premise "any value the column
holds, US-055" overlooks the sign constraint.

FR-005 rule 2 ("points ≤ 0 → 0 %") itself is not wrong — `ReportBuilder.Grade`
is a pure function and the rule is the literal §4 v98 text; it is only the
**stored** framing of AC-007 that cannot be met. Points = 0 is storable.

## 3. Choices for the SPECIFICATION fix (for the human, not decided here)

1. **Keep the schema** (consistent with spec §9 and US-015/US-055): AC-010 tests
   only the positive boundary; AC-007 is restated either as points = 0, or as a
   `ReportBuilder`-level test of a negative pair without a stored row — then
   DB_DESIGN is NOT_APPLICABLE. *(Recommended: the constraints are a deliberate,
   twice-pinned design decision, and §4 v98 does not ask to store negatives.)*
2. **Drop the two constraints** so negatives are stored as Google returned them
   (the "mirror" rule, v83) — a schema change with a migration, a reversal of
   US-015 VR-001 and of the US-055 pin; DB_DESIGN would then PASS with that
   migration.

## 4. Observation outside this Story's scope (for the Owner)

With the schema as it is, an **in-range** negative grade or maximum from Google
(for example −1) still fails the course's transaction and stops the whole run as
`Unexpected` — the very pattern §4 v98 removes for out-of-range values (I-1
skips a value below −7.9·10²⁸, but −1 stops the run). Whether Classroom can send
a negative value at all is not established in the requirements. If it matters,
it is a new `trebovaniya.md` §7 item, not a fix here.

The specification review (`docs/reviews/specifications/US-057-spec-review.md`)
did not catch B-1/B-2: it checked the casts and the report code, not the table
constraints.

## 5. Delegation

Nothing was delegated; the constraints were read from
`Infrastructure/Persistence/Configurations/{CourseWork,Submission}Configuration.cs`,
the US-015 db design and the US-055 persistence test directly.
