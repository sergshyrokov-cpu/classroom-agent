---
artifact_type: test_generation_report
story: US-043
version: 1
status: DRAFT
created_at: 2026-10-14T06:29:14Z
updated_at: 2026-10-14T06:29:14Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-043-test-strategy.md
    version: 1
  - path: docs/tests/US-043-ac-test-matrix.md
    version: 1
  - path: docs/specifications/US-043-spec.md
    version: 2
  - path: docs/designs/api/US-043-openapi.yaml
    version: 1
supersedes: null
---

# US-043 Test Generation Report

## Files created (tests only — no production file touched)

- `tests/ClassroomAgent.Tests/TestInfrastructure/GroupLessonsBlank.cs` — the form's facts of spec FR-002 / FR-003, request bodies, the copy plan of FR-006.
- `tests/ClassroomAgent.Tests/TestInfrastructure/GroupLessonsBlankHostExtensions.cs` — synthetic course seeding of any size, the scale template, the screen oracle (US-028 program export of the same request), copy / footer / "untouched" assertions.
- `tests/ClassroomAgent.Tests/Web/Pages/GroupLessonsBlankExportTests.cs`
- `tests/ClassroomAgent.Tests/Infrastructure/Export/GroupLessonsBlankFormTests.cs`
- `tests/ClassroomAgent.Tests/Web/Security/GroupLessonsBlankAuthorizationTests.cs`
- `tests/ClassroomAgent.Tests/Web/Pages/GroupLessonsBlankPageTests.cs`
- `tests/ClassroomAgent.Tests/Web/Logging/GroupLessonsBlankLoggingTests.cs`
- `tests/ClassroomAgent.Tests/Web/Localization/GroupLessonsBlankTranslationTests.cs`

Files modified: none.

## Delegation

Test design, the two helpers, `GroupLessonsBlankExportTests`,
`GroupLessonsBlankFormTests` and the red-phase verdict were written inline. The
authorization, page, logging and translation test files were written by a
`cheap-worker` subagent from a fixed brief; the allowed-role test was then
strengthened inline (see Findings). The first build, the filtered run and the
full-suite run were delegated to a `cheap-worker`; the re-runs after the fixes
were run inline.

## Commands

- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` (full suite, before the last fixes, which
  touched only US-043 test files) — 5640 tests: 5578 passed, 62 failed, 0
  skipped; **every failure was in a `GroupLessonsBlank*` class** — no regression.
- `dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~GroupLessonsBlank"`
  (final) — 79 tests: 11 passed, 68 failed, 0 skipped.

## Passing tests (existing behaviour, expected)

| Test | Why it passes now |
|---|---|
| `TheProgramLayout_IsExactlyTheUs028File` (5 cases) | AC-004 *is* "unchanged": today the members are ignored. After implementation it guards that the new members leave the US-028 file intact. |
| `WithTheForm_AnUnknownOrientation_IsStillRefused` | US-028 VR-001 orientation rule, kept with the form (I-8). |
| `AnUnknownCourse_Is404_BeforeTheSheetLimit` | US-028 `404` order, which must stay before the sheet limit. |
| `Anonymous_Is401_WithTheApiBody`, `ADeanOnTheForcedPasswordChange_Is403_WithTheApiBody`, `WithoutTheToken_Is400_WithTheApiBody` | host-wide rules of the existing endpoint; no new endpoint (FR-008). |
| `TheFillMethod_IsNotShown` | **vacuous before implementation** — nothing new is rendered yet; it bites once the layout choice is on the page. |

## Expected failures (red, missing production behaviour)

The 68 failures, by group:

| Group | Failure |
|---|---|
| Form file tests (AC-013, I-1) | `group-lessons-blank.xlsx` not shipped (`FileNotFoundException`, "must be shipped"); no embedded resource |
| Copy content (AC-001 … AC-003, AC-006, AC-007, AC-010, AC-015, FR-012) | the response is the US-028 file: plan names differ (`Оцінювання` vs `Оцінювання 1`, `Grading` for English), material titles present, `A2` is not the course, landscape applied, or the form file is missing when names coincide |
| Validation (AC-005) | `400` expected, `200` returned; or the new message keys are missing (`Translation key 'Export.Validation.…' is missing`) |
| Sheet limit (AC-014) | key missing; 100-sheet file is 2 sheets |
| Read-only, audit, allowed roles (AC-008, AC-009, FR-010) | the file carries no copy-number footer / not the planned sheets |
| Page (FR-004, FR-012) | no layout choice, no number inputs, no translations |
| Logging (FR-011) | no layout in the line; refusals answer `200` |
| Translations (FR-012) | keys missing |

Every failure message names missing behaviour; none is a compile, fixture or
host-configuration failure.

## Findings during the red phase

- **Seed corrected.** The first seed gave every returned submission a turn-in
  date, so no grade cell was "a grade alone" and `ASmallReport_…` found no number
  cell to compare (its sanity check failed). `turnedInAt` was removed from the
  seed; the check now passes and the test reaches the form.
- **Vacuous passes removed.** US-028's Ukrainian sheet names are "Оцінювання"
  and "Теми занять", identical to the form's. Tests that only compared names for
  a one-copy file passed against the US-028 file (`InReadOnlyMode_…`,
  `AFormExport_WritesOneAuditRow_…`, `AnAdminAndADean_ExportTheBlankForm`). They
  now also assert the copy-number footers (`AssertIsTheForm`) or the multi-copy
  plan, and fail as expected.
- **Students without lessons.** With nothing published the screen lists no
  student (US-027 FR-005.7), yet I-6 writes them; the test takes the roster from
  an October report of the same course.

## Untested Acceptance Criteria

None. AC-011 is structural (every test seeds synthetic data in PostgreSQL and
reads the workbook by sheets and cells). Excluded scenarios and their reasons:
test strategy §6 (`500` for a broken embedded form, phone width, browser script).

## Notes for IMPLEMENTATION

- The tests fix no C# signature of the new port, fill model or request record.
  Existing tests construct `JournalExportRequest` positionally
  (`JournalExportWorld.Request`): adding members must keep them compiling
  (optional parameters or an updated test helper within the Story's scope).
- The embedded resource name must end in `group-lessons-blank.xlsx`
  (`TheInfrastructureAssembly_EmbedsTheShippedForm`).
- The success log line must carry the layout token, the per-copy values and the
  fill method name `Basic`; refusals log the rule name (`StudentsPerCopyInvalid`,
  `LayoutInvalid`, `TooManySheets`).

## Open Decisions

None raised.

## Overall result

PASS — every AC mapped; tests compile with no warnings; existing suite green;
new behaviour tests fail only for the missing implementation.
