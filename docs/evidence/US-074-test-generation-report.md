---
artifact_type: test_generation_report
story: US-074
version: 1
status: DRAFT
created_at: 2026-11-06T20:19:11Z
updated_at: 2026-11-06T20:19:11Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-074-spec.md
    version: 1
  - path: docs/designs/api/US-074-api-design.md
    version: 1
  - path: docs/designs/api/US-074-openapi.yaml
    version: 1
  - path: docs/designs/database/US-074-db-design.md
    version: 1
  - path: docs/designs/database/US-074-entity-model.md
    version: 1
  - path: docs/tests/US-074-test-strategy.md
    version: 1
  - path: docs/tests/US-074-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-074 Test Generation Report

**Overall result: PASS** — every AC has a test that fails for the missing implementation; the solution builds with 0 warnings;
the existing classes touched by the skeleton are green.

## 1. Test files created

| File | Tests (cases) | Written by |
|---|---|---|
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/MeetCodeOrganizerNameSourceTests.cs` | 13 methods (39 cases) | test-writer |
| `tests/ClassroomAgent.Tests/Web/UseCases/MeetCodeNameSearchMeasurementTests.cs` | 1 | test-writer |
| `tests/ClassroomAgent.Tests/Web/Pages/MeetCodeOrganizerNamePageTests.cs` | 13 methods (48 cases) | delegated (`cheap-worker`), reviewed |
| `tests/ClassroomAgent.Tests/Web/Security/MeetCodeOrganizerNameAccessTests.cs` | 5 methods (6 cases) | delegated (`cheap-worker`), reviewed |

## 2. Test files modified

- `tests/ClassroomAgent.Tests/Infrastructure/Persistence/SearchMatchingSourceTests.cs` — six assertions read
  `Organizers.Select(o => o.Email)` instead of `OrganizerEmails` (the property was replaced, entity model §2). Assertions are
  otherwise unchanged; the class stays green (45/45).

## 3. Compile-only skeleton

| Declaration | File |
|---|---|
| `MeetOrganizerRecord(string Email, string? StoredFullName)` (new) | `src/ClassroomAgent.Application/Models/MeetOrganizerRecord.cs` |
| `UnassignedCodeRow.Organizers : IReadOnlyList<MeetOrganizerRecord>` replaces `OrganizerEmails` | `src/ClassroomAgent.Application/Models/UnassignedCodeRow.cs` |
| `LinkedCodeRow.Organizers` (same) | `src/ClassroomAgent.Application/Models/LinkedCodeRow.cs` |
| `MarkedCodeRow.Organizers` (same) | `src/ClassroomAgent.Application/Models/MarkedCodeRow.cs` |
| `UnassignedCodeItem.Organizers : IReadOnlyList<PersonRef>` replaces `OrganizerEmails` | `src/ClassroomAgent.Application/Models/Dtos/UnassignedCodeItem.cs` |
| `LinkedCodeItem.Organizers` (same) | `src/ClassroomAgent.Application/Models/Dtos/LinkedCodeItem.cs` |
| `NotACourseCodeItem.Organizers` (same) | `src/ClassroomAgent.Application/Models/Dtos/NotACourseCodeItem.cs` |

Because the designs **replace** an existing positional member rather than add one, the callers had to be adapted to compile.
The adaptation keeps today's behaviour exactly (no name is read or shown; the cell renders the same emails):

- `MeetCodesReadSource.LoadOrganizersAsync` / `OrganizersOf` return `MeetOrganizerRecord(email, null)` lists in the same
  ordinal order; `organizerSet` takes the emails from the records. Marked in a remark as skeleton.
- `GetMeetCodesQuery` gains a private `Organizers(...)` mapping each record to `PersonRef(null, email)`; IMPLEMENTATION puts
  `MeetReportRules.Name(r.StoredFullName)` there (entity model §3).
- `MeetCodes/Index.cshtml` renders `string.Join(", ", item.Organizers.Select(o => o.Email))` — the same text as before.

No `NotImplementedException` was used: no new member exists that a test calls directly, and throwing in these reads would
break the existing green suite. No DI registration, migration, route or translation entry was added.

## 4. Commands

- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors (after the skeleton, and again by the delegate after its files).
- `dotnet test tests/ClassroomAgent.Tests --filter FullyQualifiedName~<Class>` one class per run (no `--nologo`, no `|`).
- Docker Desktop 29.8.0 / WSL2; Testcontainers PostgreSQL started normally.

## 5. Results (red phase, 2026-11-06)

| Class | Total | Failed | Passed |
|---|---|---|---|
| MeetCodeOrganizerNameSourceTests | 39 | 33 | 6 |
| MeetCodeOrganizerNamePageTests | 48 | 45 | 3 |
| MeetCodeOrganizerNameAccessTests | 6 | 4 | 2 |
| MeetCodeNameSearchMeasurementTests | 1 | 1 | 0 |

### Expected failures — all for the missing implementation

- Name absent: records carry `StoredFullName = null` where a name is expected; cells lack `meet-person-name`.
- Search by name finds nothing: counters 0/0/0, empty lists, no Meet group in the header, measurement count 0.
- Case-only duplicates not merged (`A.Dup` and `a.dup` both listed).
- Command-count test fails on its control "names were read", not on the counts.
- Access, read-only, log/audit, erasure and "writes nothing" tests fail on their first control (the code found by name), so the
  later assertions run only after implementation.

No failure comes from compilation, fixtures, host setup or a wrong assertion.

### Passing before implementation, by construction

- Src `TheOrganizer_CarriesTheChosenName_AsTheMeetReportsChooseIt` for `none` and `null` × 3 lists (6): the expected name is null,
  which today's read already gives. They also prove the AC-009 harness: the Meet reports' `GetPeopleAsync` returns the same null.
  The four named cases of the same method are red.
- Page `TheOtherSearchFields_StillFindTheirCodes` (3): a regression guard for code, email and course search.
- Acc `Anonymous_IsSentToSignIn`, `ARestrictedDean_IsSentToTheForcedChange` (2): authorization is unchanged by design (TC-5 pair).

### Regression (existing classes touched by the skeleton)

| Class | Result |
|---|---|
| SearchMatchingSourceTests | 45/45 |
| MeetCodesSearchPageTests | 38/38 |
| MeetCodeSearchSurvivesActionsTests | 23/23 |
| MeetCodeActionsHttpTests | 41/41 |
| SearchResultsPageTests | 57/57 |
| ListSearchAccessTests | 21/21 |
| SearchMeasurementTests (US-071) | 1/1 |

### Unexpected failures

None. The delegate fixed one setup mistake in its own test before the final run (an extra code linked to the same course made
the "алгебр" case find two codes); no assertion was changed.

## 6. Delegation

- `cheap-worker` (Sonnet): wrote the page and access classes against a fixed brief listing every scenario and helper, ran the
  build and both classes. The test-writer reviewed the AC-001…AC-003 tests' assertions and the report.
- Kept inline: strategy, matrix, skeleton, the source and measurement classes, the red-phase verdict.

## 7. Notes for IMPLEMENTATION

- The organizer cell separator: the page tests normalise white space before commas (`CellText`), so the view may render
  `, ` between partials in any layout that reads as "name email, name email".
- Existing tests whose organizer is a roster teacher seeded by `SeedRosterCourseAsync` (named "Test Person …") would start to
  show a name; none failed today, but re-run the Meet-code and header-search classes after the change and report any
  difference as a consequence of the Story.
- `MeetCodeNameSearchMeasurementTests` writes `US-074-name-search-measure.txt` to the temp folder for the implementation report.

## 8. Untested Acceptance Criteria

None.

## 9. Open Decisions

None.
