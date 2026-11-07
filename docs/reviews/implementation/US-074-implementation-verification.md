---
artifact_type: implementation_verification
story: US-074
version: 1
status: APPROVED
created_at: 2026-11-06T21:07:05Z
updated_at: 2026-11-06T21:07:05Z
produced_by: implementation-verifier
inputs:
  - path: docs/stories/US-074-meet-code-organizer-by-name.md
    version: null
  - path: docs/specifications/US-074-spec.md
    version: 1
  - path: docs/decisions/US-074-open-decisions.md
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
  - path: docs/evidence/US-074-test-generation-report.md
    version: 1
  - path: docs/evidence/US-074-implementation-report.md
    version: 1
supersedes: null
---

# US-074 Implementation Verification

## 1. Summary

Verdict **PASS**. The three Meet code lists render each organizer as the Meet reports render a person, the page search and
the header search find a code by its organizer's chosen name, and display and search share one query definition
(`MeetCodesReadSource.OrganizerNames`). Every AC has at least one test that would fail without the behaviour. The three
disclosed deviations are sound. No Critical or Major finding. The open IMPLEMENTATION finding m-1 is confirmed and stays
open (it is a test change, not this stage's to make). One INFO note on report accuracy.

Delegation: `cheap-worker` ran the build and the Story's six test classes (section 8), reporting facts only and touching no
file. All reading of tests against ACs, deviation judgement and classification were done here.

## 2. Reviewed Artifacts

As in the front matter `inputs`, all at their current versions; the implementation report names the same versions, so no
input is stale. Working tree: `git status` / `git diff` against `9661e39`, plus the untracked files listed by `git status`
(new model, partial, four test classes, Story docs).

## 3. AC Verification

| AC | Tests read | Proves the AC? |
|---|---|---|
| AC-001 | Page `TheOrganizerCell_ShowsTheNameThenTheEmail_AsTheMeetReportsDo` (3 lists × Admin/Dean × uk/en: name span precedes email span, text "name email"); `TheName_IsEncoded` (encoded in markup, decoded text equals stored); Src `TheOrganizer_CarriesTheChosenName_…`, `AnOrganizerEmailInAnotherCase_…` | Yes. Fails before the change (no name span). Both languages show the same untranslated name. |
| AC-002 | Page `WithNoUsableName_TheEmailStandsAlone` (no participant, null, blank lowest id, with a named control code on the same page); Src name-rule cases `none`, `null`, `blank-then-named` | Yes. The control code guards against "nothing is ever named". |
| AC-003 | Src `SeveralOrganizers_EachOnce_InOrdinalOrder_CaseOnlyDuplicatesMerged` (exact record list incl. `A.Dup` merged with `a.dup`, its name found through the lower-case participant); Page `SeveralOrganizers_AreOrdered_AndEachShowsOnce`; existing `MeetCodesSearchPageTests.LinkedAndNotACourse_ShowTheOrganizers_RightAfterTheCode` (markup `</span>, <span`) | Yes. The page test's `CellText` hides white space before a comma (m-1, confirmed); the separator is held by the existing test's markup assertion. |
| AC-004 | Src `AClearedOrganizer_IsNotFoundByName_AndNotListed_ASurvivorStillIs` (control before clearing: 1/1/1; after: 0/0/0 by name and email, survivor 1/1/1); Page `AnErasedOrganizer_IsNotFound_AndNotShown_TheSurvivorStays` (real erasure path, pre-erasure control) | Yes. Both have a positive control, so the "not found" is not free. |
| AC-005 | Src `ACode_IsFound_ByItsOrganizersName_OnEveryList`, `AnApostropheVariant_FindsTheName`, `WordsOfOneSearch_LandInANameAndInAnotherField` (incl. an unmatched word → 0), `OnlyTheChosenName_IsSearched` (I-3: second participant, surname, given name → 0 with chosen → 1/1/1 control); Page search, apostrophe, paging (25 → 20 + 5, links carry `q`), row action (redirect keeps `q`), other fields | Yes. |
| AC-006 | Src `TheHeadersFirstMatches_FindACodeByItsOrganizersName`; Page `TheHeaderSearch_FindsCodes_ByTheOrganizersName` (no name in group rows, other organizer's codes absent), `TheHeaderSearch_LimitsToTwenty_AndLinksToTheWholeList` | Yes. |
| AC-007 | Acc: Admin and Dean allowed on both searches; anonymous → sign-in; Dean with temporary password → forced change; read-only (suspended) works with `GoogleCallCount() == 0` and the name shown; searched word absent from audit rows and log lines with found-controls and `Assert.NotEmpty(events)`; Src `ANameWord_IsABoundParameter`, `ReadingAndSearchingNames_WritesNothing`; existing `ListSearchAccessTests`, `PermissionMatrixTests` | Yes. The two redirect tests pass by construction (unchanged rules), as the matrix states; the TC-5 pair is complete with the allowed-role test. |
| AC-008 | Src `APageOfManyRows_ReadsNamesInAsManyCommands_AsAPageOfOne` (3 lists; asserts names were really read, then equal command counts for 1 and 10 rows); Meas `SearchByOrganizerName_OnTheExpectedSize_IsMeasured` (1000/300/300, counts asserted, times recorded) | Yes. A per-row read would raise the count for ten rows. |
| AC-009 | Src `TheOrganizer_CarriesTheChosenName_AsTheMeetReportsChooseIt` compares each of the six cases with `MeetReportSource.GetPeopleAsync` on the same data; the blank test is the same `MeetReportRules.Name` in both paths (`GetMeetCodesQuery.Organizers`) | Yes. |

## 4. Specification Coverage

| Requirement | Where | Verdict |
|---|---|---|
| FR-001 organizer, email comparison, I-1 merge, lowest-id name, blank = no name | `LoadOrganizersAsync` (non-null emails, group by `ToLowerInvariant`, ordinal min spelling, ordinal order); `OrganizerNames` (`NOT EXISTS` lower id with non-null name); `GetMeetCodesQuery.Organizers` → `MeetReportRules.Name` | Implemented |
| FR-002 cell on three lists, order, ", ", name then email, encoding, empty cell | `Index.cshtml` three cells → `_MeetCodeOrganizers.cshtml`; no `Html.Raw`; header/columns unchanged | Implemented |
| FR-003 search by chosen name, BR-087 via `SearchMatch.InField`, cross-field words, null email excluded | `CodesByOrganizer`: `AnyOf(email InField, non-null email ∧ lower(email) ∈ named)` | Implemented |
| FR-004 header search | No own code; `GetFirstMatchesAsync`/`GetCountsAsync` compose the same queries | Implemented |
| FR-005 bounded queries, search in database | Two commands per page; `named` is an `IQueryable` subquery | Implemented |
| VR-001…VR-003 | No new input; Razor encoding; word bound (test) | Implemented |
| Section 7 security | No policy, log, audit, Google or outbound change in the diff | Implemented (SECURITY_REVIEW checks independently) |

No behaviour beyond the artifacts was found.

## 5. Design Conformance

- **OpenAPI / API design:** no new endpoint; `OrganizerEmails` replaced by `Organizers : IReadOnlyList<PersonRef>` on the three
  DTOs, as `MeetCodesPageModelDelta`. The view renders the `_MeetPerson` structure instead of the file itself (D-2).
- **Database design / entity model:** no schema change, no migration (§8); `MeetOrganizerRecord(Email, StoredFullName)` as
  §1; row models §2 and DTOs §3 match; `OrganizerNames` and `CodesByOrganizer` follow §3 and §5; no index (§6) and the
  measurement backs it. `OrganizerNameRow` shape differs (D-3).
- **Architecture:** AD-3 — no `DbContext` in Web, Application references only Domain-side types; AD-4 — no Google type;
  AD-6 — reads only; AD-8 — Razor views receive `PersonRef` DTOs, no entity. `MeetReportRules.Name` is `internal` in
  Application and used from the same assembly.

## 6. Deviations

| Id | Departs from | Reason | Verdict |
|---|---|---|---|
| D-1 | US-070 test `MeetCodesSearchPageTests.LinkedAndNotACourse_ShowTheOrganizers_RightAfterTheCode` expected plain text "a, b" | With person elements the helper inserts a space for each tag; the assertion now ignores that space and a new markup assertion checks `</span>, <span`. Same order and content, stricter about what the user sees | Sound, accepted |
| D-2 | API design §1 / openapi line 71 / entity model §5: reuse `_MeetPerson.cshtml` | Reusing the file put its trailing line break before each comma, a visible space; a one-line partial renders the same `meet-person-name` / `meet-person-email` structure. Spec FR-002 allows "reuse that partial or render the same structure" | Sound, accepted |
| D-3 | Db-design §3: `OrganizerNameRow` a positional record | EF Core cannot compose a filter on a constructor projection; `required init` properties, same name and members, private to Infrastructure | Sound, accepted |

No undisclosed deviation found.

## 7. Report Accuracy

The change set in implementation report §4 equals the diff and the untracked files: 11 changed production/test files plus the
new model, partial and four test classes, each traced. AC-to-test locations in §3 exist with the names given. Build, test and
format claims match the run in section 8. INFO-1: §4 lists `workflow-state.yaml` and `history.jsonl` but not
`docs/workflow/findings.jsonl`, which is also modified; it is in the Git Policy closed list, so no scope issue.

Clean change: no `TODO`, `TBD`, `FIXME`, skipped test or `NotImplementedException` in the changed or new files; no
remaining `OrganizerEmails` anywhere in `src` or `tests`.

## 8. Story Test Run

Delegated to `cheap-worker`, 2026-11-06T21:03:42Z–21:06:35Z:

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| `--filter FullyQualifiedName~MeetCodeOrganizerNameSourceTests` | 39/39 |
| `~MeetCodeOrganizerNamePageTests` | 48/48 |
| `~MeetCodeOrganizerNameAccessTests` | 6/6 |
| `~MeetCodeNameSearchMeasurementTests` | 1/1 |
| `~MeetCodesSearchPageTests` | 38/38 |
| `~SearchMatchingSourceTests` | 45/45 |

Measurement output of this run (temp file `US-074-name-search-measure.txt`): one teacher's name median 41 ms (worst 60),
a word common to all 300 teachers 29 ms (31), nothing 19 ms (21) — usable at the expected volume. The full suite is not rerun
here (SECURITY_REVIEW runs it).

## 9. Findings

- **US-074/IMPLEMENTATION/m-1 (Minor, open, confirmed).** `MeetCodeOrganizerNamePageTests.SeveralOrganizers_AreOrdered_AndEachShowsOnce`
  normalises white space before commas, so alone it would not catch D-2. The existing test's markup assertion covers the
  separator, so no AC is unproven. Not closed here: the fix is a test edit.
- **INFO-1 (Info).** Implementation report §4 omits `docs/workflow/findings.jsonl` from the change set (section 7).
- **D-1, D-2, D-3 (Deviation, accepted).** Section 6.

No Critical or Major finding.

## 10. Verdict

**PASS** → SECURITY_REVIEW.
