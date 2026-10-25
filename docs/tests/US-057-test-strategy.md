---
artifact_type: test_strategy
story: US-057
version: 1
status: DRAFT
created_at: 2026-10-25T20:30:00Z
updated_at: 2026-10-25T20:30:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-057-out-of-range-grades-do-not-stop-sync.md
    version: null
  - path: docs/specifications/US-057-spec.md
    version: 2
  - path: docs/decisions/US-057-open-decisions.md
    version: 2
  - path: docs/designs/api/US-057-api-design.md
    version: 2
  - path: docs/designs/database/US-057-db-design.md
    version: 2
supersedes: null
---

# US-057 Test Strategy — A course with points beyond the program's range is skipped, and the school scale never fails a report

## 1. Scope

Two behaviours (spec §1):

1. **Synchronization** (FR-001 … FR-004): the Google adapter turns a coursework maximum, an
   assigned grade or a draft grade beyond the `decimal` range into a course-scoped
   `GoogleReadFailureKind.ValueOutOfRange`; the run skips that course before its transaction,
   does not count it, writes one Warning (event 5135 `SyncCourseValueOutOfRange`) with no value
   or identity of the course, goes on and completes; the next run reads the course again.
2. **School scale** (FR-005): `ReportBuilder.Grade` gives 100 % for points ≥ maximum and 0 % for
   points ≤ 0 without dividing, so no pair overflows on the page or in the export.

API design v2 and DB design v2 are NOT_APPLICABLE: no endpoint, schema, migration, translation
or UI change. No security policy, role or audit event changes (spec §7).

## 2. Test levels

| Level | Why | Class |
|---|---|---|
| Unit — adapter over a scripted transport (TC-4) | only the real adapter sees Google's `double`; proves the classification, the range bounds and that the failure carries no value or id | `Infrastructure/Google/GoogleClassroomReaderValueRangeTests` |
| Unit — use case, ports substituted (TC-1) | skip semantics, counting, a later failure, the next run (incremental rule) | `Application/UseCases/ValueOutOfRangeSkipTests` |
| Integration — real host, background service, PostgreSQL (TC-2) **and the real Google adapter over a routed scripted transport** | end to end with the value sent as the JSON number Google would send: stored rows, the log line, absence of the value from the log | `Web/BackgroundServices/ValueOutOfRangeSyncTests` |
| Unit — report and export use cases, journal port substituted (TC-1) | FR-005 rules on both use cases, including a negative pair no table can hold (AC-007 b, OD-001) | `Application/UseCases/ReportScaleBoundsTests` |
| Integration — HTTP as Dean, PostgreSQL | stored extreme and zero pairs on the report page and the export | `Web/Pages/ScaleBoundsReportPageTests` |

**Why the real adapter inside the host.** The host's `FakeClassroomReader` holds `decimal`
values, so 1·10³⁰ cannot exist there and "the value is not in the log" (AC-004) would be vacuous.
`TestInfrastructure/GoogleClassroomScript` (new) answers Google's REST routes as JSON and
registers the production `GoogleClassroomReader` through the host's `ConfigureServices` seam; the
Meet port stays the substituted `FakeMeetReportsReader`. Nothing reaches a network (TC-4).

No contract level: no API change. No persistence-shape test: no schema change (DB design v2).

## 3. Scenarios

### Positive
- Three courses, the second with `maxPoints` 1·10³⁰: courses 1 and 3 stored, course 2 not, Meet
  step ran, run `completed`, 2 counted (AC-001) — host and use case.
- Assigned 1·10³⁰ and draft −1·10³⁰ in two courses: both skipped, run `completed` (AC-002).
- An archived course skipped on its first appearance is read in full and stored by the next,
  not weekly-full run at the same `updateTime` (AC-005).
- 7·10²⁸ of 0.0001 → label for 100 %, assigned and draft, page and export (AC-006).
- 0 of 0.0001 → label for 0 % (AC-007 a); −7·10²⁸ of 0.0001 via the substituted port → 0 %
  (AC-007 b).
- 12-point preset: 6, 12, 0 of 12 → `6`, `12`, `1` (AC-008).

### Negative
- A stored course reporting an out-of-range value keeps every stored row exactly (AC-003) —
  host (text fingerprint of course, items, memberships, submissions) and use case.
- A skip followed by a 403 on the next course's roster: run `failed` with
  `TechnicalAccountCannotRead`, not `Unexpected`; the skip's own log line is still written
  (AC-009) — host and use case.

### Boundary
- Just beyond the range: ±8·10²⁸ (above `decimal.MaxValue` ≈ 7.9228·10²⁸) and ±1·10³⁰, each of the
  three fields, are `ValueOutOfRange` (VR-001, spec I-1).
- Just inside: ±7.9·10²⁸ converted exactly as today's cast at the adapter; +7.9·10²⁸ stored for
  all three fields through the host (AC-010). The negative bound is not stored (L-1).
- Report bounds: points equal to the maximum → 100 %, 0 → 0 %, 1 of 2 → middle label (FR-005).

### Validation
VR-001 is covered by the boundary scenarios. VR-002 adds no validation.

### Security (SC-10, AD-4)
- The adapter's failure — message and inner exceptions — carries neither the value nor the
  item, submission or user id.
- The host log of AC-001's run: exactly one Warning 5135 with the run id and course 2's id;
  no line carries `1E+30`, `1e+30`, `1E30`, `1000000000000000000000000000000`, course 2's item id
  or title, student name, address or id, or submission id. The skipped course's identities are
  unique to it (ordinal 52), so their absence is meaningful; the comparison is ordinal so an
  exponent form cannot match a lower-case GUID by chance; completion is asserted first.
- No endpoint, policy or role changes, so no new allowed/forbidden-role test (TC-5 is
  unaffected); the report tests run as the Dean and existing role tests cover access.

### Persistence
Stored rows asserted through PostgreSQL in the host tests (AC-001 … AC-003, AC-010). No migration.

## 4. Fixtures

- `SyncWorld` (use case), `ReportTemplateWorld`, `JournalExportWorld`, `FakeJournalFieldSource`.
- `SyncHostExtensions.StartAsync` with `GoogleClassroomScript.InstallIn(host)` in its `seed`
  callback; `CourseRows` / `CourseWorkRows` for AC-003's stored course.
- `JournalHostExtensions.StartAsync(Actor.Dean)`, `ReportHostExtensions.InsertTemplateAsync`
  with a three-row bounds scale (`TSZERO` 0–0, `TSMID` 1–99, `TSFULL` 100–100) so each label
  names one percent; a course seeded with exactly one item so no other grade yields a label.

## 5. Decisions taken in this stage

- **Skeleton**: only `GoogleReadFailureKind.ValueOutOfRange` (named by FR-001). The new outcome
  list of FR-002.2 is **not** named by any artifact, so no test references it; it is observed
  through the 5135 log line instead (AC-004, AC-009).
- **SPEC_REVIEW m-2** (Diagnosis of `ValueOutOfRange`): left to IMPLEMENTATION as the review
  says; no test asserts the diagnosis.
- **AC-009 "another failure"**: a configuration 403 (`TechnicalAccountCannotRead`, one attempt)
  rather than a final transient failure, which would need the retry pauses driven on the
  manual clock; the spec gives the transient case only as an example.
- **AC-005** is tested at the use-case level, where US-018's incremental rule lives
  (`IncrementalSyncRunTests` pattern), with a control course proving the second run is not full.
- **AC-008** export: the workbook is not searched for a one-character label (other cells may
  hold it); the page cell is asserted and the export's equality with the page report is
  US-028's existing `TheWorkbook_IsTheMappedReportOfThePage_ForTheSameInputs`.

## 6. Green before implementation, by design

- Adapter: ±7.9·10²⁸ converted (AC-010) — today's cast already does it.
- Host: AC-010 stored — the US-055 columns already hold it.
- Report: FR-005 bounds 2/2, 0/2, 1/2 and the 12-point preset (AC-008), zero pair stored
  (AC-007 a) — today's clamp gives the same result for every pair that does not overflow.

## 7. Excluded scenarios

- `NaN` / infinity (spec §10). In-range negatives from Google (L-1, still stop the run).
- UI display of skipped courses (out of scope).

## 8. Known limitations

- The routed transport answers a single page per list; paging is proven by the existing
  `GoogleClassroomReaderTests`.
- The host log check covers the formats listed in AC-004, not every conceivable formatting.

## 9. Open Decisions affecting testing

None open. OD-001 (a) is reflected in AC-007 b and AC-010.
