---
artifact_type: test_generation_report
story: US-070
version: 1
status: DRAFT
created_at: 2026-11-03T07:34:36Z
updated_at: 2026-11-03T07:34:36Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-070-spec.md
    version: 4
  - path: docs/designs/api/US-070-api-design.md
    version: 1
  - path: docs/designs/database/US-070-db-design.md
    version: 1
  - path: docs/designs/database/US-070-entity-model.md
    version: 1
  - path: docs/tests/US-070-test-strategy.md
    version: 1
  - path: docs/tests/US-070-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-070 Test Generation Report

**Result: red phase verified.** 215 new test cases: 182 red for missing behaviour, 33 green by design (explained below). 20 adapted `PageSearchHintTests` cases are red for the new hint texts. Every other existing test passes, some only on a rerun alone after environment timeouts in the full run (INFO-1).

## Files

Created, under `tests/ClassroomAgent.Tests/`:

| File | Cases |
|---|---|
| `Infrastructure/Persistence/SearchMatchingSourceTests.cs` | 45 |
| `Web/Pages/TeacherListSearchPageTests.cs` | 21 |
| `Web/Pages/MeetCodesSearchPageTests.cs` | 38 |
| `Web/Pages/MeetCodeSearchSurvivesActionsTests.cs` | 23 |
| `Web/Pages/CourseListSearchPageTests.cs` | 13 |
| `Web/Security/ListSearchAccessTests.cs` | 21 |
| `Web/Pages/PickerNarrowMarkupTests.cs` | 15 |
| `Architecture/PickerNarrowScriptTests.cs` | 25 |
| `Web/Localization/ListSearchTranslationTests.cs` | 13 |
| `TestInfrastructure/ListSearchTestData.cs`, `ListSearchReadLog.cs`, `RecordingTeacherListSource.cs`, `RecordingMeetCodesReadSource.cs` | helpers |

Modified:

- `TestInfrastructure/FakeTeacherListSource.cs`: `search` parameter, ignored.
- `Infrastructure/Persistence/TeacherListSourceTests.cs`, `DatabaseStatisticsReadSourceTests.cs`, `MembershipStartReadersTests.cs`: pass `null` for the new parameter.
- `Web/Pages/TeacherListPageTests.cs`: `q` removed from the "unknown parameters are not echoed" test, as it is now read (api-design §6).
- `TestInfrastructure/PageSearchHintTestData.cs`, `Web/Pages/PageSearchHintTests.cs`: the variants and texts of FR-008 (spec section 13, api-design D-10).
- `Web/Localization/PageSearchHintTranslationTests.cs`: one new fact, `TheListFieldTemplate_IsAnEntry_AndTheRetiredHintKeysAreGone`.

No existing test pinned the old Meet page-past-the-last behaviour or the old Cancel target, so none needed changing for api-design §6.

## Compile-only skeleton

| Declaration | File |
|---|---|
| `GetCountsAsync(string? search, …)`, `GetUnassignedPageAsync(…, string? search, …)`, `GetLinkedPageAsync(…, string? search, …)`, `GetMarkedPageAsync(…, string? search, …)` | `Application/Ports/IMeetCodesReadSource.cs` |
| `CountTeachersAsync(state, string? search, …)`, `GetTeacherPageAsync(state, string? search, …)` | `Application/Ports/ITeacherListSource.cs` |
| `OrganizerEmails` right after `MeetingCode` | `Application/Models/LinkedCodeRow.cs`, `MarkedCodeRow.cs` |
| The new parameters ignored, `OrganizerEmails` empty | `Infrastructure/Persistence/MeetCodesReadSource.cs`, `TeacherListSource.cs` |
| Callers pass `null` | `Application/UseCases/GetMeetCodesQuery.cs`, `GetTeacherListQuery.cs` |

The signatures of the entity model compile with 0 warnings. There is no new member, so nothing throws `NotImplementedException`, and behaviour does not change. `SearchMatch` is not declared (entity model §3 leaves its members to the implementer), and the request and page models of the API design are not declared since no test names them.

## Delegation

Kept here: the test design, the skeleton, `ListSearchTestData`, the recording sources, `SearchMatchingSourceTests`, `TeacherListSearchPageTests`, `MeetCodesSearchPageTests`, `MeetCodeSearchSurvivesActionsTests`, the red-phase verdict and the corrections below. Delegated to `cheap-worker` against a fixed brief: `CourseListSearchPageTests`, `ListSearchAccessTests`, `PickerNarrowMarkupTests`, `PickerNarrowScriptTests`, `ListSearchTranslationTests`, the adaptations of existing tests, and every build and test run.

## Commands and results

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | succeeded, 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln` (free RAM 5.47 GB, Docker 29.8.0) | total 7336, passed 7006, failed 330, skipped 0; 19 min 12 s; 207 timeout or Ryuk lines |
| `--filter FullyQualifiedName~SearchMatchingSourceTests` (before the corrections) | 45: 7 passed, 38 failed |
| `--filter FullyQualifiedName~TeacherListSearchPageTests` | 21: 0 passed, 21 failed |
| `--filter FullyQualifiedName~MeetCodesSearchPageTests` (before the corrections) | 38: 4 passed, 34 failed |
| `--filter FullyQualifiedName~MeetCodeSearchSurvivesActionsTests` (before the corrections) | 23: 5 passed, 18 failed |
| `--filter FullyQualifiedName~CourseListSearchPageTests` | 13: 7 passed, 6 failed |
| `--filter FullyQualifiedName~ListSearchAccessTests` | 21: 11 passed, 10 failed |
| `--filter FullyQualifiedName~PickerNarrowMarkupTests` | 15: 6 passed, 9 failed |
| `--filter FullyQualifiedName~PickerNarrowScriptTests` | 25: 1 passed, 24 failed |
| `--filter FullyQualifiedName~ListSearchTranslationTests` | 13: 0 passed, 13 failed |
| `--filter FullyQualifiedName~PageSearchHint` | 67: 46 passed, 21 failed |
| `~SearchMatchingSourceTests.TheNumberOfWords`, `.AnyStoredSession`, `.Searching_WritesNothing` (after the corrections) | 7: all failed, Expected 1, Actual 2 |
| `~MeetCodesSearchPageTests.ACode_IsFound` (after the corrections) | 4: all failed |
| `~MeetCodeSearchSurvivesActionsTests.AMalformedForm` (after the corrections) | 1: passed |

## The failures of the full run

330 failed rows: 209 in the classes of this Story, 121 elsewhere.

**Story classes, 209.** 181 in the new classes, as this report's table counts them, 1 translation fact, and 27 in `PageSearchHintTests`: the 20 red alone plus 7 of the 11 rows with an environment marker. `PageSearchHint` alone gives exactly 21 failures, and those classes were not touched by the corrections.

**Other classes, 121, in 21 classes.** Each class was rerun alone and came back fully green, 0 failed: `OwnerUiLanguageTests` 21/21, `MeetingCodeLinkSchemaTests` 65/65, `UiLanguageTranslationTests` 10/10, `JournalExportTranslationTests` 92/92, `RetentionPurgeMeetLinkTests` 7/7, `RetentionPurgeSchemaTests` 26/26, `ErasureExclusionEntryPageTests` 13/13, `ChangeDeanLoginPageTests` 29/29, `LastSynchronizationBlockTests` 34/34, `TeacherListPageTests` 17/17, `MarkedCourseMeetCodesPageTests` 7/7, `AccessCheckFailureTests` 14/14, `SetupAuditTests` 6/6, `MeetPullTranslationTests` 4/4, `DataHealthBannerTests` 25/25, `SetupCodeServiceModeTests` 2/2, `ControlPlaneKeyEncryptionTests` 3/3, `AllowedAdminRevocationTests` 16/16, `AllowedAdminAuthorizationTests` 18/18, `InstallationKeyEncryptionTests` 3/3, `InstallationRenameTests` 6/6.

These are not regressions, but the full run was not clean: see INFO-1 and INFO-2.

## Red phase

**Expected failures, 182 + 20.** Each is an assertion about missing behaviour: the search not applied (unfiltered rows, counts 2 for 1), no field or form, `200` where `400` is due, no `q` in a link or redirect, an empty `OrganizerEmails`, a missing translation key, no `data-narrow`, and `FileNotFoundException` from `File.ReadAllText` of the script not yet written. No failure is in setup, seeding or a helper.

**Green by design, 33.** Each was checked; none is free:

| Tests | Cases | Why green now, and what makes it mean something |
|---|---|---|
| `SearchMatchingSourceTests`: `OutsideACodeField_AHyphenIsOrdinary`, `OnTheCourseList_StatusAndTeacher_CombineWithTheSearch` | 2 | The course search already holds this; guards against the move to the new rules. |
| `CourseListSearchPageTests`: refusals, one word in capitals, no match, filters | 7 | US-020 already does it; same reason. |
| `ListSearchAccessTests`: courses in allowed, read-only, Google and log tests | 5 | The course list already has `q`. The other two lists are red in the same tests, and each asserts the field shows the text as its control. |
| `ListSearchAccessTests`: anonymous and restricted | 6 | Authorization is unchanged; TC-5 wants them on each list, and the same requests as allowed roles are red. |
| `MeetCodeSearchSurvivesActionsTests`: bad and repeated `returnQ` (3), no search emits nothing, malformed form, read-only refusal | 6 | An unread field is ignored today. They guard FR-005.5, D-2 and the guard's order against the implementation; `Marking_RedirectsToTheSearch` is the red control that the field is read. The repeated test also has a same-host control: a repeated known field is `400`. |
| `PickerNarrowMarkupTests`: no hook where nothing narrows (5), selected option kept (1) | 6 | Nothing has hooks yet, so these bite only once the hooks exist. `ASelectPicker_…` and `TheCourseChoice_…` are their red controls, and each asserts its page rendered (status, row, select or form). |
| `PickerNarrowScriptTests.NoView_HoldsInlineNarrowingCode` | 1 | Guards SC-15 against the implementation. |

**Corrections after the first run.** Nine new cases passed for the wrong reason: `TheNumberOfWords_DoesNotChangeTheNumberOfCommands` for three lists, `AnyStoredSessionOfACode_LendsItsOrganizer`, `Searching_WritesNothing` and `ACode_IsFound_ByEveryFieldOfTheFeature` (four rows) each held one row in the list, so an unfiltered read looked like a match. Each now seeds a row the text does not match, and all nine are red. `AMalformedForm_AnswersTheErrorPage_NotTheSearch` failed for a defect of the test: the text it looked for was part of the code in the page. It now looks for `Ls.Needle`, and it passes as a guard. `AWordOfHyphensOnly_FindsNoCode_ButMayMatchAnotherField` was rewritten before the first run: the test domain `school-one.example.test` holds a hyphen, so a `-` would rightly match every organizer email.

## Untested Acceptance Criteria

None. AC-009's narrowing as behaviour is the person's check on a computer and a phone, as the Specification says; it must be recorded in the implementation report.

## Open Decisions

None.

## Findings

- **INFO-1.** The full run took 19 min 12 s with 207 timeout or Ryuk lines and 121 failed rows outside the Story, all green alone. 215 cases were added, most with their own host. The load may be near what this machine holds; IMPLEMENTATION's full run may need a rerun of the same kind. A full run after IMPLEMENTATION is the check that counts.
- **INFO-2.** Three of those 121 rows carried no environment marker: one each in `MarkedCourseMeetCodesPageTests`, `SetupCodeServiceModeTests` and `InstallationRenameTests`. Each class is green alone. They were not diagnosed here.

## Overall result

PASS.
