---
artifact_type: implementation_report
story: US-074
version: 1
status: DRAFT
created_at: 2026-11-06T20:54:37Z
updated_at: 2026-11-06T20:54:37Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-074-meet-code-organizer-by-name.md
    version: null
  - path: docs/decisions/US-074-open-decisions.md
    version: 1
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
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
---

# US-074 Implementation Report

## 1. Summary

The three Meet code lists show each organizer as the Meet reports show a person (name, then email; email alone without a
name), and the page search and the header search find a code by its organizer's chosen name. Names come from stored
Classroom participants in one query definition shared by display and search; no schema change, no new endpoint, policy,
translation key or package. Build, full suite (7759/7759) and the format check pass. `security_sensitive: false`: no change
to authentication, authorization, credentials, stored data or outbound flows; the change reads personal data already shown
to the same roles, which SECURITY_REVIEW still checks.

## 2. Source artifacts

As in the front matter `inputs`. The compile-only skeleton was laid at TEST_WRITING (test generation report §3) and is
completed here.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (class → method) | Status |
|---|---|---|---|
| AC-001 | `MeetCodesReadSource.LoadOrganizersAsync`, `OrganizerNames`; `GetMeetCodesQuery.Organizers`; `Views/MeetCodes/_MeetCodeOrganizers.cshtml` | Page → `TheOrganizerCell_ShowsTheNameThenTheEmail_AsTheMeetReportsDo`, `TheName_IsEncoded`; Src → `TheOrganizer_CarriesTheChosenName_AsTheMeetReportsChooseIt`, `AnOrganizerEmailInAnotherCase_StillFindsTheParticipantsName` | PASS |
| AC-002 | `OrganizerNames` (lowest id with non-null name), `MeetReportRules.Name` in `GetMeetCodesQuery.Organizers` | Page → `WithNoUsableName_TheEmailStandsAlone`; Src → name-rule cases | PASS |
| AC-003 | `LoadOrganizersAsync` grouping by `ToLowerInvariant`, ordinal-first spelling, ordinal order; partial's ", " separator | Src → `SeveralOrganizers_EachOnce_InOrdinalOrder_CaseOnlyDuplicatesMerged`; Page → `SeveralOrganizers_AreOrdered_AndEachShowsOnce`; existing `MeetCodesSearchPageTests.LinkedAndNotACourse_ShowTheOrganizers_RightAfterTheCode` | PASS |
| AC-004 | Both branches of `CodesByOrganizer` require a non-null `organizer_email`; display reads non-null emails only | Src → `AClearedOrganizer_IsNotFoundByName_AndNotListed_ASurvivorStillIs`; Page → `AnErasedOrganizer_IsNotFound_AndNotShown_TheSurvivorStays` | PASS |
| AC-005 | `CodesByOrganizer` name branch through `SearchMatch.InField` (BR-087); used by `UnassignedCodes`, `LinkedLinks`, `MarkedLinks` | Src → `ACode_IsFound_ByItsOrganizersName_OnEveryList`, `AnApostropheVariant_FindsTheName`, `WordsOfOneSearch_LandInANameAndInAnotherField`, `OnlyTheChosenName_IsSearched`; Page → search, apostrophe, paging, row-action, other-fields tests | PASS |
| AC-006 | No code of its own: `GetFirstMatchesAsync` composes the same three queries | Src → `TheHeadersFirstMatches_FindACodeByItsOrganizersName`; Page → `TheHeaderSearch_FindsCodes_ByTheOrganizersName`, `TheHeaderSearch_LimitsToTwenty_AndLinksToTheWholeList` | PASS |
| AC-007 | Reads only; no policy, log or audit change | Acc → all five methods; Src → `ANameWord_IsABoundParameter`, `ReadingAndSearchingNames_WritesNothing`; existing `ListSearchAccessTests`, `PermissionMatrixTests` | PASS |
| AC-008 | Two commands per page (`LoadOrganizersAsync`); name search a subquery in SQL; no index (db-design §6) | Src → `APageOfManyRows_ReadsNamesInAsManyCommands_AsAPageOfOne`; Meas → `SearchByOrganizerName_OnTheExpectedSize_IsMeasured` | PASS |
| AC-009 | Same lowest-id-with-name rule as `MeetReportSource.GetPeopleAsync`; same `MeetReportRules.Name` blank test | Src → `TheOrganizer_CarriesTheChosenName_AsTheMeetReportsChooseIt` (compares with `GetPeopleAsync`); Page → `WithNoUsableName_TheEmailStandsAlone` (blank case) | PASS |

### 3.1 Measurement (AC-008)

1000 named students, 300 named teachers, 300 codes, no new index; counts + first page, five runs after a warm-up:

| Text | Median | Worst |
|---|---|---|
| `викладачка123 олена` (one teacher) | 103 ms | 107 ms |
| `викладачка` (all 300) | 101 ms | 113 ms |
| `ъъъъъъ` (nothing) | 55 ms | 61 ms |

## 4. Change set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Application/Models/MeetOrganizerRecord.cs` | new | entity model §1 |
| `src/ClassroomAgent.Application/Models/UnassignedCodeRow.cs`, `LinkedCodeRow.cs`, `MarkedCodeRow.cs` | `OrganizerEmails` → `Organizers` | entity model §2 |
| `src/ClassroomAgent.Application/Models/Dtos/UnassignedCodeItem.cs`, `LinkedCodeItem.cs`, `NotACourseCodeItem.cs` | `OrganizerEmails` → `Organizers : IReadOnlyList<PersonRef>` | entity model §3, openapi `MeetCodesPageModelDelta` |
| `src/ClassroomAgent.Application/UseCases/GetMeetCodesQuery.cs` | private `Organizers` mapping with `MeetReportRules.Name` | entity model §3, FR-002, AC-009 |
| `src/ClassroomAgent.Infrastructure/Persistence/MeetCodesReadSource.cs` | `OrganizerNameRow`, `OrganizerNames`, `LoadOrganizersAsync`, `CodesByOrganizer`, `organizerSet` from records | db-design §3, §4, §5 |
| `src/ClassroomAgent.Web/Views/MeetCodes/_MeetCodeOrganizers.cshtml` | new partial: organizers in the `_MeetPerson` structure, ", " between | FR-002 |
| `src/ClassroomAgent.Web/Views/MeetCodes/Index.cshtml` | the three organizer cells render the partial | FR-002 |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/SearchMatchingSourceTests.cs` | assertions read `Organizers.Select(o => o.Email)` | TEST_WRITING (renamed member) |
| `tests/ClassroomAgent.Tests/Web/Pages/MeetCodesSearchPageTests.cs` | one assertion adapted, one markup assertion added (see §7 D-1) | AC-003, FR-002 |
| `tests/.../MeetCodeOrganizerNameSourceTests.cs`, `Web/Pages/MeetCodeOrganizerNamePageTests.cs`, `Web/Security/MeetCodeOrganizerNameAccessTests.cs`, `Web/UseCases/MeetCodeNameSearchMeasurementTests.cs` | new (TEST_WRITING) | `ac_test_matrix` |
| `docs/tests/US-074-*.md`, `docs/evidence/US-074-*.md` | workflow artifacts | artifact-paths `{story_id}` |
| `docs/workflow/workflow-state.yaml`, `history.jsonl` | orchestrator | Git Policy closed list |

No secret, database file, generated export or IDE config is in the change set. No migration (db-design §8).

## 5. Validation evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| New classes | `dotnet test tests/ClassroomAgent.Tests --no-build --filter FullyQualifiedName~<Class>` × 4 | Src 39/39, Page 48/48, Acc 6/6, Meas 1/1 |
| Full suite, run 1 | `dotnet test ClassroomAgent.sln --no-build` (20:24–20:35Z) | 7759 total, 4 failed — `MeetCodesSearchPageTests.LinkedAndNotACourse_ShowTheOrganizers_RightAfterTheCode` × 4 (§7 D-1, D-2) |
| Fix check | `--filter FullyQualifiedName~MeetCodesSearchPageTests`, `~MeetCodeOrganizerNamePageTests` | 38/38, 48/48 |
| Full suite, run 2 | `dotnet test ClassroomAgent.sln --no-build` (20:42–20:54Z) | **7759/7759 passed, 0 skipped** |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes --include <15 changed .cs files>` | exit 0 |

Delegation: the page and access test classes were written by `cheap-worker` at TEST_WRITING. In this stage the production
change was small and the one failure needed diagnosis of rendered markup, so the build and test runs were done inline rather
than delegated; no mechanical edit was large enough to hand off.

## 6. Configuration changes

None.

## 7. Deviations and discovered problems

- **D-1 Existing test adapted (`MeetCodesSearchPageTests.LinkedAndNotACourse_ShowTheOrganizers_RightAfterTheCode`).** It compared
  the cell text to `"a@…, b@…"`. The helper `Ls.CellsOfCode` replaces every tag with a space, so with the person elements FR-002
  requires the text always reads `"a@… , b@…"`. The assertion now removes the space before the comma and a new assertion checks
  the markup itself holds `</span>, <span` — strictly stronger about what the user sees. The expected order and content are
  unchanged.
- **D-2 A real defect found by that check and fixed.** The first version of the partial reused `_MeetPerson.cshtml`, whose
  trailing line break rendered as a visible space before each comma ("a@… , b@…") in the browser. The partial now renders the
  same structure (`meet-person-name` then `meet-person-email`) on one line; the API design's reuse of the file is replaced by
  reuse of its structure, which spec FR-002 allows ("may reuse that partial or render the same structure").
- **D-3 `OrganizerNameRow` shape.** Db-design §3 names it a positional private record; it is a private record with `required init`
  properties, because EF Core cannot compose a filter on members of a constructor projection. Same name, same two members, still
  private to Infrastructure.
- **m-1 (non-blocking, tests).** `MeetCodeOrganizerNamePageTests.SeveralOrganizers_AreOrdered_AndEachShowsOnce` normalises white
  space before commas (`CellText`), so it did not catch D-2; the existing test did. Worth tightening to a markup assertion.

## 8. Open Decisions

None touched or newly required.
