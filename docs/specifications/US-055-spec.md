---
artifact_type: specification
story: US-055
version: 1
status: APPROVED
created_at: 2026-10-25T10:10:00Z
updated_at: 2026-10-25T11:00:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-055-grade-columns-fit-any-value.md
    version: null
  - path: trebovaniya.md
    version: 97
  - path: docs/decisions/US-055-open-decisions.md
    version: 1
supersedes: null
---

# US-055 Specification — Points and grades of any size are stored as Google returned them

## 1. Overview

`trebovaniya.md` §3 stores a task's maximum points and a submission's assigned
and draft grades «в баллах как есть», and since v83 Google data is a mirror
stored as Google returned it. The database does not allow that today: the three
columns `course_work.max_points`, `submission.assigned_grade` and
`submission.draft_grade` are `numeric(10,4)` (US-015 database design), which
holds at most 999 999.9999 and rounds to four decimal places.

On the college's real domain (deployment rehearsal P-9, 2026-10-23) one value
above that limit stopped the run with PostgreSQL error 22003 after 4 793
courses, at the same course on every run (US-017 FR-005, unchanged by US-053
OD-001). The first full synchronization can never finish.

This Story brings the storage to the requirement. It changes the type of the
three columns, ships the migration, and changes nothing else: no requirement, no
endpoint, no user-visible text, no rule of the run (OD-001 a).

Code facts this Specification relies on (read 2026-10-25, not re-designed here):

- `CourseWorkConfiguration.cs:47` and `SubmissionConfiguration.cs:47–48` declare
  `HasColumnType("numeric(10,4)")`; the domain properties are `decimal?`
  (`CourseWork.MaxPoints`, `Submission.AssignedGrade`, `Submission.DraftGrade`,
  carried by `CourseWorkDetails` / `SubmissionDetails`).
- `GoogleClassroomReader.cs:352` and `384–385` convert the API's `double?` to
  `decimal?` with an explicit cast. That conversion is untouched (I-1).
- The journal page, the report page and the report export format a stored value
  with `"0.############################"` (`Journal/Index.cshtml:144`,
  `Report/Index.cshtml:268`, `ReportWorkbookMapper.cs:208`); no formatter limits
  the size of the number.
- `ReportBuilder.Grade` (`ReportBuilder.cs:256`) computes a percentage
  `points / max * 100` for a scale template and clamps it to 0…100.

## 2. Business Goal

One task with unusually large points, or a grade with more than four decimal
places, must not stop the school's whole synchronization on every run, and the
stored value must be the value Google gave (§3, v83 mirror rule). This is a
pre-pilot blocker.

## 3. Business Flow

Unchanged. A synchronization run reads courses, their items and submissions from
Google and stores them (US-015, US-018); the journal and reports show what is
stored (US-020, US-027, US-028). After this Story a value that previously
overflowed the column is stored like any other, so the run continues to the next
courses and to the Meet step.

## 4. Functional Requirements

### FR-001 The three columns do not limit the value

`course_work.max_points`, `submission.assigned_grade` and
`submission.draft_grade` store every value the program can hold in its number
type (`decimal`: up to 28–29 significant digits, magnitude below
≈ 7.9 × 10²⁸) without overflow and without rounding. Neither the size nor the
number of decimal places is limited by the column. The concrete PostgreSQL type
is chosen at DB_DESIGN (I-2).

### FR-002 The stored value equals the value read from Google

What the Google port delivers for maximum points, assigned grade and draft grade
is stored unchanged and read back unchanged — equal as a number, including every
decimal place it carries (I-1). Nullability is unchanged: absent maximum points
still means ungraded work (BR-052, US-015 VR-007); absent grades stay absent.

### FR-003 Migration

One EF Core migration (PC-2) changes the type of the three columns. It keeps
every stored value unchanged and keeps nullability. The model snapshot is
updated with it. No `EnsureCreated()`, no raw schema change outside the
migration.

### FR-004 The run continues past such a value

Because the value is stored (FR-001), the course that holds it commits like any
other; the run continues to the following courses and to the Meet step and ends
`completed` when nothing else fails. The rule that an unexpected error stops the
run is unchanged (OD-001 a, US-017 FR-005).

### FR-005 Journal, report and export show the value as stored

The course journal, the report page and the report export show such a maximum
points value and such grades like any other value, using the existing
formatting, and none of them fails. No formatting code is changed by this Story
unless a test shows that it fails on such a value (I-3).

## 5. Acceptance Criteria

Ids AC-001 … AC-006 are the Story's. All values are synthetic (TC-4); Google
ports are substituted; persistence runs on real PostgreSQL (TC-2).

### AC-001 Large maximum points are stored as returned
Given a course whose task has maximum points 12 345 678.5 in Google, when
synchronization runs, then the run completes and the stored maximum points of
that task equal 12 345 678.5. (FR-001, FR-002, FR-004)

### AC-002 Large grades are stored as returned
Given a submission whose assigned grade is 2 500 000 and whose draft grade is
1 000 000.25 in Google, when synchronization runs, then the run completes and
both grades are stored with exactly those values. (FR-001, FR-002, FR-004)

### AC-003 Decimal places are not rounded
Given a task with maximum points 10.123456 and a submission graded 7.654321 in
Google, when synchronization runs, then the stored values are 10.123456 and
7.654321, not rounded to four decimal places. (FR-001, FR-002)

### AC-004 The courses after the unusual one still load
Given three courses, the second of which holds a task with maximum points above
999 999.9999, when synchronization runs, then all three courses and their items
are stored, the Meet step runs, and `SyncState` is `completed`. (FR-004)

### AC-005 Existing values survive the migration
Given an installation database at the schema before this Story's migration, with
stored maximum points and grades within the old limit (including four-decimal
values and nulls), when the new migration is applied, then every stored value
and every null is unchanged. (FR-003)

### AC-006 The journal and reports show such values
Given the data of AC-001 and AC-002, when a Dean opens the course journal and
builds a report for that course, then the cell shows the grade and the maximum
points as stored, and neither page fails. (FR-005; the report is built with a
template that shows raw points — I-4)

### AC-006a The report export shows such values (derived)
Given the data of AC-001 and AC-002, when the Dean exports that report, then the
export is produced and its cell carries the grade and the maximum points as
stored. Derived from the Story's in-scope list ("the reports and their exports").
(FR-005, I-4)

## 6. Validation Rules

### VR-001 Values from Google
No new validation is added and none is removed. The size and decimal places of
maximum points and grades are not validated by the program — §3 stores them «как
есть»; only the column limited them, and that limit is removed (FR-001). A value
the program cannot represent at all (not a number, infinity, or beyond the range
of `decimal` when it is read) is out of scope: it still fails the read, and the
run stops with `Unexpected` like any other unexpected error (Story, Out of scope).

### VR-002 Equality
"Equal" in AC-001 … AC-006a means numeric equality of `decimal` values read back
through EF Core, and the exact decimal digits for the stored text (e.g.
10.123456, not 10.1235). Trailing zeros are not part of the comparison.

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | No endpoint, page, policy, route or role check is added or changed | SC-4, AD-3 |
| S-02 | Every Google scope stays read-only; no Google call is added | Hard Stops, §6 |
| S-03 | Maximum points and grades are student data: no test fixture uses a real value; the value that overflowed on the college's domain is not read or reproduced | TC-4, Hard Stops |
| S-04 | No log line, audit row or error text writes a grade or maximum points value; the run's failure and success records keep their existing shape | SC-10, SC-11 |
| S-05 | The schema change ships only as an EF Core migration; no generated database file is committed | PC-2, Git Policy |
| S-06 | No NuGet package is added | AGENTS.md Technology Stack |

## 8. Error Handling

- A value that fits `decimal` no longer produces PostgreSQL error 22003; nothing
  else in the run's error handling changes.
- Any other failure keeps its existing behaviour: the run stops, `SyncState` is
  `failed` with its diagnosis and step (US-017 FR-005, US-053).
- The migration fails as a whole if the database rejects it; it changes no data,
  so there is nothing to compensate.

## 9. Non-Functional Requirements

- **Table size.** `submission` holds hundreds of thousands of rows on a real
  school (821 810 in the rehearsal). DB_DESIGN states whether the type change
  rewrites the table and how long the migration is expected to hold its lock;
  a change that does not rewrite the table is preferred. (NFR note, not a new
  requirement.)
- **Storage.** No measurable growth is expected for values of ordinary size;
  DB_DESIGN confirms.
- Build with no warnings; the full suite green (Definition of Done).

## 10. Out of Scope

- What the run does after a failure — it still stops (US-017 FR-005, US-053
  OD-001; Owner 2026-10-25, OD-001 a).
- A Google value the program cannot represent at all (VR-001).
- The reader's `double` → `decimal` conversion (I-1).
- The points scale and conversion in report templates (`ReportTemplate`).
- Interface fixes from the same rehearsal (P-10, P-12, P-13) — the styling Story.
- Restoring decimal places that the old column rounded away before this
  migration (I-5).

## 11. Open Decisions

None open. OD-001 (only widen the columns; the run-stops rule unchanged) was
resolved by the Owner on 2026-10-25 and is carried in
`docs/decisions/US-055-open-decisions.md`. No item of `trebovaniya.md` §7 bears
on this Story.

## 12. Interpretations

- **I-1 "As Google returned it" is the value the Google port delivers.** The
  Classroom client library gives these fields as `double`; the reader converts
  them to `decimal` (`GoogleClassroomReader.cs:352, 384–385`). That conversion
  keeps up to 15 significant digits, which covers every value in the ACs and any
  points value a teacher can type. The Story names only the columns; the reader
  is not changed.
- **I-2 Column type at DB_DESIGN.** FR-001 states the property (holds any
  `decimal` without overflow or rounding); choosing the PostgreSQL type —
  unconstrained `numeric` being the obvious candidate — is DB_DESIGN's work.
- **I-3 Formatting is expected to work already.** The Story says the pages
  "already format whatever is stored"; the formatters (section 1) confirm no
  size limit. FR-005 therefore changes code only if a test proves a failure.
  `ReportBuilder.Grade`'s percentage cannot overflow `decimal` for values in
  `decimal`'s range divided by a positive maximum within the ACs, and is clamped.
- **I-4 AC-006 uses a raw-points template.** With a scale template a cell shows
  the scale's label, not the points, so "shows the grade and the maximum points
  as stored" is testable only with the «без перевода» (raw points) template. The
  export is covered by derived AC-006a because the Story's scope names exports.
- **I-5 Already rounded values.** No school has run in production (rehearsal
  only). A value stored before the migration keeps what the old column kept;
  whether a later run rewrites it follows the existing synchronization rules
  (US-015, US-018), unchanged by this Story.
- **I-6 AC-004 "the Meet step runs".** Proven through the substituted Meet port:
  the step is invoked and the run ends `completed`.

## 13. Traceability

| AC | FR | VR | Security |
|---|---|---|---|
| AC-001 | FR-001, FR-002, FR-004 | VR-002 | S-03 |
| AC-002 | FR-001, FR-002, FR-004 | VR-002 | S-03 |
| AC-003 | FR-001, FR-002 | VR-002 | S-03 |
| AC-004 | FR-004 | — | S-03, S-04 |
| AC-005 | FR-003 | VR-002 | S-05 |
| AC-006 | FR-005 | VR-002 | S-01 |
| AC-006a (derived) | FR-005 | VR-002 | S-01 |

## 14. Delegation note

No lookups were delegated: the code facts in section 1 came from a handful of
direct greps of `src/`, `docs/designs/database/US-015-db-design.md` and
`trebovaniya.md` §3 (lines 785–800).
