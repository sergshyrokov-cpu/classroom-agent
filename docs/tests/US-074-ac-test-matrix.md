---
artifact_type: ac_test_matrix
story: US-074
version: 1
status: DRAFT
created_at: 2026-11-06T20:18:38Z
updated_at: 2026-11-06T20:18:38Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-074-meet-code-organizer-by-name.md
    version: null
  - path: docs/specifications/US-074-spec.md
    version: 1
  - path: docs/designs/api/US-074-openapi.yaml
    version: 1
  - path: docs/designs/database/US-074-db-design.md
    version: 1
  - path: docs/designs/database/US-074-entity-model.md
    version: 1
  - path: docs/tests/US-074-test-strategy.md
    version: 1
supersedes: null
---

# US-074 Acceptance Criteria to Test Matrix

Classes (all under `tests/ClassroomAgent.Tests/`):

- **Src** — `Infrastructure/Persistence/MeetCodeOrganizerNameSourceTests`
- **Page** — `Web/Pages/MeetCodeOrganizerNamePageTests`
- **Acc** — `Web/Security/MeetCodeOrganizerNameAccessTests`
- **Meas** — `Web/UseCases/MeetCodeNameSearchMeasurementTests`

Status is the red-phase result of 2026-11-06 (see the test generation report): **RED** fails for the missing
implementation; **GREEN-BY-CONSTRUCTION** passes before implementation by design, with a red control named.

| AC | Scenario | Level | Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Named organizer, 3 lists × Admin/Dean × uk/en: name span then email span | Integration HTTP | Page | `TheOrganizerCell_ShowsTheNameThenTheEmail_AsTheMeetReportsDo` | cell text "name email", name span precedes email span | RED |
| AC-001 | Name with `<b>&"'` is encoded, untranslated | Integration HTTP | Page | `TheName_IsEncoded` | encoded in body, decoded text equals the stored name | RED |
| AC-001, AC-002, AC-009 | Six name-rule cases × 3 lists: chosen stored name, equal to the Meet reports' `GetPeopleAsync` | Integration DB | Src | `TheOrganizer_CarriesTheChosenName_AsTheMeetReportsChooseIt` | `MeetOrganizerRecord(email, chosen)`; Meet reports give the same `FullName` | RED (named cases); GREEN-BY-CONSTRUCTION for "none"/"null" (control: the named cases) |
| AC-001 | Organizer email in another case than the participant's | Integration DB | Src | `AnOrganizerEmailInAnotherCase_StillFindsTheParticipantsName` | name found; name search finds the code | RED |
| AC-002 | No participant / null name / blank lowest-id name × 3 lists: email alone | Integration HTTP | Page | `WithNoUsableName_TheEmailStandsAlone` | cell equals the email, no name span; control code shows its name | RED (on the control) |
| AC-003 | Several organizers: ordinal order, each once, case-only duplicates merged | Integration DB | Src | `SeveralOrganizers_EachOnce_InOrdinalOrder_CaseOnlyDuplicatesMerged` | `[A.Dup+Іваненко, b.named+Петренко, c.unnamed]` | RED |
| AC-003 | Same, as rendered, 3 lists | Integration HTTP | Page | `SeveralOrganizers_AreOrdered_AndEachShowsOnce` | "Іваненко Петро A.Dup…, Петренко Іван b.named…, c.unnamed…", each once | RED |
| AC-004 | Cleared organizer (erasure/exclusion state): not found by name or email, not listed, survivor found | Integration DB | Src | `AClearedOrganizer_IsNotFoundByName_AndNotListed_ASurvivorStillIs` | counts 0 for the cleared, 1/1/1 for the survivor | RED (on the pre-clearing control) |
| AC-004 | Real erasure end to end: former name and email find nothing; row shows survivor only | Integration HTTP | Page | `AnErasedOrganizer_IsNotFound_AndNotShown_TheSurvivorStays` | no name or email of the erased person on the page | RED (on the pre-erasure control) |
| AC-005 | Part of name, word order, case, white space; 3 lists with counters | Integration DB | Src | `ACode_IsFound_ByItsOrganizersName_OnEveryList` | each list's code, counts 1/1/1 | RED |
| AC-005 | Same over HTTP, 3 lists × 4 texts | Integration HTTP | Page | `TheSearch_FindsACode_ByTheOrganizersName` | listed code and counters | RED |
| AC-005 | Apostrophe variants | Integration DB | Src | `AnApostropheVariant_FindsTheName` | found | RED |
| AC-005 | Apostrophe variants over HTTP | Integration HTTP | Page | `TheSearch_TreatsApostropheVariantsAsEqual` | found | RED |
| AC-005 | Words across fields and across two organizers (I-2) | Integration DB | Src | `WordsOfOneSearch_LandInANameAndInAnotherField` | matches as listed; an unmatched word excludes | RED |
| AC-005 | Only the chosen name; not another participant's, not surname/given name (I-3) | Integration DB | Src | `OnlyTheChosenName_IsSearched` | chosen found; others 0 | RED (on the chosen-name control) |
| AC-005 | The search stays across pages | Integration HTTP | Page | `PagingLinks_CarryTheNameSearch_AndWalkTheMatches` | counter 25, 20 + 5, links carry q | RED |
| AC-005 | The search stays after a row action | Integration HTTP | Page | `ARowAction_KeepsTheNameSearch` | redirect keeps q; code on Not-a-course under the search | RED |
| AC-005 | Search by code, email, course still works | Integration HTTP | Page | `TheOtherSearchFields_StillFindTheirCodes` | found | GREEN-BY-CONSTRUCTION (regression guard) |
| AC-006 | Header first matches through the same read | Integration DB | Src | `TheHeadersFirstMatches_FindACodeByItsOrganizersName` | three matches, one per list | RED |
| AC-006 | Header group rows and links, no organizer in rows | Integration HTTP | Page | `TheHeaderSearch_FindsCodes_ByTheOrganizersName` | three rows linking to `MeetUrl(list, q)` | RED |
| AC-006 | 20-result rule and whole-list link with the search | Integration HTTP | Page | `TheHeaderSearch_LimitsToTwenty_AndLinksToTheWholeList` | "показано 20 з 21", link to Unassigned with q | RED |
| AC-007 | Admin and Dean allowed on both pages under a name search | Security HTTP | Acc | `AnAdminAndADean_FindTheCode_ByTheOrganizersName` | 200, code found | RED |
| AC-007 | Anonymous sent to sign-in | Security HTTP | Acc | `Anonymous_IsSentToSignIn` | 302 to sign-in | GREEN-BY-CONSTRUCTION (unchanged rule; existing ListSearchAccessTests too) |
| AC-007 | Dean on forced change sent to the change | Security HTTP | Acc | `ARestrictedDean_IsSentToTheForcedChange` | 302 to change-password | GREEN-BY-CONSTRUCTION (unchanged rule) |
| AC-007 | Read-only mode: both work, name shown, no Google call | Security HTTP | Acc | `InReadOnlyMode_TheSearchWorks_AndCallsNoGoogleApi` | 200, found, `GoogleCallCount() == 0` | RED (on the found control) |
| AC-007 | Searched name in no audit row and no log line | Security HTTP | Acc | `TheSearchedName_IsInNoAuditRow_AndNoLogLine` | absent | RED (on the found control; the log/audit assertions run after implementation) |
| AC-007, VR-003 | Name word is a bound parameter | Integration DB | Src | `ANameWord_IsABoundParameter` | found, word absent from every command text | RED |
| AC-007 | Reading and searching write nothing | Integration DB | Src | `ReadingAndSearchingNames_WritesNothing` | fingerprints unchanged | RED (on the found control) |
| AC-008 | Page of ten codes × two named organizers takes as many commands as a page of one, 3 lists | Integration DB | Src | `APageOfManyRows_ReadsNamesInAsManyCommands_AsAPageOfOne` | equal command counts, names read | RED (on the names-read control) |
| AC-008 | Name search on 1000 students / 300 teachers / 300 codes, no new index; times recorded | Measurement | Meas | `SearchByOrganizerName_OnTheExpectedSize_IsMeasured` | counts 1/0/0, 300, 0; times to `US-074-name-search-measure.txt` | RED |
| AC-009 | Same name as the Meet reports for the same data | Integration DB | Src | `TheOrganizer_CarriesTheChosenName_AsTheMeetReportsChooseIt` | see the AC-001 row | RED |
| AC-009 | Blank chosen name gives the email alone on the page, as the Meet reports show it | Integration HTTP | Page | `WithNoUsableName_TheEmailStandsAlone` (`blank-lowest-id`) | email alone | RED (on the control) |

Every Acceptance Criterion AC-001…AC-009 has at least one RED test. Existing tests keep covering the unchanged parts the ACs
name: `MeetCodesSearchPageTests` (counters, list links, VR refusals), `MeetCodeSearchSurvivesActionsTests` (returnQ),
`ListSearchAccessTests` and `PermissionMatrixTests` (TC-5 for `ViewMeetCodes`), `SearchResultsPageTests` (header group).
