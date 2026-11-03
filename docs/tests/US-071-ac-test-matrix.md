---
artifact_type: ac_test_matrix
story: US-071
version: 1
status: DRAFT
created_at: 2026-11-03T17:11:55Z
updated_at: 2026-11-03T17:11:55Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-071-search-field-in-the-header.md
    version: null
  - path: docs/specifications/US-071-spec.md
    version: 2
  - path: docs/tests/US-071-test-strategy.md
    version: 1
supersedes: null
---

# US-071 Acceptance Criteria to Test Matrix

Status: **RED** fails until IMPLEMENTATION; **GUARD** green by design before and after (the reason is in the test generation report); **MANUAL** the Owner's walk, recorded in the implementation report; **MEASURE** run by the suite, its numbers copied to the implementation report.

Classes: `GetSearchResultsQueryTests` in `Application.UseCases`; `StudentSearchSourceTests` in `Infrastructure.Persistence`; `SearchResultsPageTests`, `HeaderSearchFieldTests`, `ErrorPageHeaderTests`, `MarkupTextSurvivesActionsTests`, `PageSearchHintTests` in `Web.Pages`; `SearchAccessTests`, `PermissionMatrixTests` in `Web.Security`; `SearchTranslationTests`, `PageSearchHintTranslationTests` in `Web.Localization`; `DisabledButtonRuleTests`, `PickerNarrowScriptTests` in `Architecture`; `SearchMeasurementTests` in `Web.UseCases`.

| Acceptance Criterion | Scenario | Test Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Teaching pages, both roles | HTTP | `HeaderSearchFieldTests` | every-teaching-page theory | field in the header, once, empty | RED |
| AC-001 | Admin's and Dean's other pages | HTTP | `HeaderSearchFieldTests` | Admin and Dean page facts | field | RED |
| AC-001 | Label and button, both languages | HTTP | `HeaderSearchFieldTests` | label theory | translated texts | RED |
| AC-001 | Form and input attributes, `autocomplete="off"` | HTTP | `HeaderSearchFieldTests` | attributes fact | GET, action, `q`, 100, off, no token | RED |
| AC-001 | Empty on the three lists under their own search | HTTP | `HeaderSearchFieldTests` | lists theory | own field holds it, header none | RED |
| AC-001 | Forced change, pages before sign-in, error page, Control Plane | HTTP | `HeaderSearchFieldTests` | absence tests | no field | GUARD |
| AC-001 | Error page under a protected endpoint | HTTP | `ErrorPageHeaderTests` | `AMalformedCode_OnTheCourseChoice_LeavesTheMarkerNowhere`, `TheReadOnlyRefusal_HasNoHeaderControls`, `TheAntiforgeryRefusal_HasNoHeaderControls` | no field, no switcher | RED |
| AC-001 | A not-found page under a protected endpoint | HTTP | `ErrorPageHeaderTests` | `ANotFoundCard_HasNoHeaderControls` | no field, no switcher | GUARD |
| AC-002 | Four groups in order with headings, both languages | HTTP | `SearchResultsPageTests` | `AllFourGroups_AppearInTheFixedOrder_WithTheirHeadings` | order, `<h2>`, text | RED |
| AC-002 | A group with no match hidden | HTTP | `SearchResultsPageTests` | `AGroupWithNoMatch_IsHidden` | one group | RED |
| AC-002 | A person in both roles | HTTP, Integration | `SearchResultsPageTests`, `StudentSearchSourceTests` | `APersonInBothRoles_IsInBothGroups_AndTheStudentsRowOmitsTheCourseTheyTeach`, `OnlyAStudentMembership_Qualifies` | both groups; only student courses | RED |
| AC-002 | Teacher, course and Meet rows | HTTP | `SearchResultsPageTests` | `ATeacherRow_LinksToTheirCoursesAtStatusAll_WithNoText`, `TheTeachersGroup_IsTheListAtStatusAll`, `ACourseRow_ShowsSectionAndStatus_AndLinksToTheCard`, `AMeetRow_LinksToItsListWithTheSearch_AndNamesTheList` | fields, links | RED |
| AC-002 | Match rules at source | Integration | `StudentSearchSourceTests` | `AStudent_IsFound_BySurnameGivenNameOrEmail`, `EveryWord_MustBeFound`, `ApostropheVariants_CountAsOneCharacter`, `PatternCharacters_MatchOnlyThemselves`, `TheFullName_IsNotSearched`, `AMeetCode_IsMatched_ByEveryFieldOfTheScreen` | found / none | RED |
| AC-002 | Mapping, status "all" | Unit | `GetSearchResultsQueryTests` | `TeachersAndCourses_AreReadAtStatusAll_WithTheLimit`, `ATeacherRow_HasTheLabelAndEmail`, `ACourseRow_HasNameSectionAndState` | rows | RED |
| AC-003 | Both courses, only the left marked, Kyiv date, both languages | HTTP | `SearchResultsPageTests` | `AStudentOnOneRoster_WhoLeftAnother_ShowsBothCourses_AndOnlyTheLeftOneIsMarked` | links, dated mark on one | RED |
| AC-003 | Equal sightings: mark, no date | HTTP, Unit | `SearchResultsPageTests`, `GetSearchResultsQueryTests` | `AMembershipWithEqualSightings_CarriesTheMarkWithNoDate`, `TheLeftMark_FollowsTheJournalsRule_InTheSchoolsZone` | undated | RED |
| AC-003 | Back on the roster | HTTP | `SearchResultsPageTests` | `BackOnTheRoster_ThereIsNoMark` | no mark | RED |
| AC-003 | Departed found; no name or email not found | Integration | `StudentSearchSourceTests` | `ADepartedStudent_IsFound`, `AStudentWithNoNameOrEmail_IsNotFound_ByAnyText` | found / none | RED |
| AC-003 | Every student course, in order, with sightings | Integration, Unit | `StudentSearchSourceTests`, `GetSearchResultsQueryTests` | `TheCourses_AreEveryStudentMembership_InTheCourseListsOrder`, `AStudentRow_HasTheLabelEmailAndCourses_InTheGivenOrder` | records | RED |
| AC-003 | The email once when it is the label (D-8) | HTTP | `SearchResultsPageTests` | `AnEmailThatIsTheLabel_IsNotRepeated` | once | RED |
| AC-004 | 21: 20 alphabetically with the line | HTTP | `SearchResultsPageTests` | `With21Matches_TwentyShowAlphabetically_WithTheCountLine` | 20, line | RED |
| AC-004 | Exactly 20: no line, no link | HTTP | `SearchResultsPageTests` | `WithExactly20_ThereIsNeitherLineNorLink` | none | RED |
| AC-004 | Students: no link | HTTP | `SearchResultsPageTests` | `TheStudentsGroup_HasNoWholeListLink` | none | RED |
| AC-004 | The link opens the list with the search and total | HTTP | `SearchResultsPageTests` | `TheTeachersLink_OpensTheWholeList_WithTheSameTotal`, `TheCoursesLink_OpensTheWholeList_WithTheSameTotal` | URL, total | RED |
| AC-004 | Meet: sum of counters, first list with a match | HTTP, Unit | `SearchResultsPageTests`, `GetSearchResultsQueryTests` | `TheMeetLink_OpensTheFirstListWithAMatch_AndTheTotalIsTheSumOfTheCounters`, `TheMeetLink_SkipsAListWithNoMatch`, `TheMeetGroup_SumsTheCounters_AndNamesTheFirstListWithAMatch` | line, URL, counters | RED |
| AC-004 | Order and limit at source | Integration, Unit | `StudentSearchSourceTests`, `GetSearchResultsQueryTests` | `Rows_AreAlphabetical_NamesakesApart_UpToTheLimit_AndTheCountIsAll`, `TheFirstMeetMatches_AreMergedAcrossTheLists_ByTheCode`, `TheMerge_TakesTheLimit_InOrderAcrossTheLists`, `TheStudentsGroup_ShowsTheLimit_WithTheTotal_AndReadsCoursesOfTheShownOnly` | rows | RED |
| AC-005 | 1 character and no text: message, no read | HTTP, Unit | `SearchResultsPageTests`, `GetSearchResultsQueryTests` | `NoTextOrOneCharacter_ShowsTheMessage_AndNoGroup`, `OneCharacter_StaysInTheField`, `NoText_IsThePrompt_AndReadsNothing`, `OneCharacter_IsThePrompt_AndTheTextStays` | prompt | RED |
| AC-005 | From 2 characters, two one-letter words | HTTP, Unit | `SearchResultsPageTests`, `GetSearchResultsQueryTests` | `TwoOneLetterWords_AreSearched`, `FromTwoCharacters_TheTextIsSearched_Whole` | searched | RED |
| AC-005 | Refused: 400, no read, no echo | HTTP, Unit | `SearchResultsPageTests`, `GetSearchResultsQueryTests` | `ARefusedText_Answers400_WithTheMessage_AndIsNotEchoed`, `ARefusedText_ReadsNothing_AndKeepsNoText` | message, empty field and path | RED |
| AC-005 | Nothing found: text encoded, reminder | HTTP, Unit | `SearchResultsPageTests`, `GetSearchResultsQueryTests` | `NothingFound_RepeatsTheText_AndShowsTheReminder`, `NothingFound_IsResultsWithFourZeroTotals_AndReadsNoRows` | both texts | RED |
| AC-005 | Encoding, unknown parameters, language switch | HTTP, Unit | `SearchResultsPageTests`, `GetSearchResultsQueryTests` | `TheText_IsEncoded_InTheFieldAndTheReturnPath`, `AnUnknownParameter_IsNeverEchoed`, `SwitchingLanguage_ReturnsToTheSameSearch`, `TheUrl_EncodesTheText` | encoded, absent | RED |
| AC-006 | Erased person at source, with a control | Integration | `StudentSearchSourceTests` | `AnErasedPerson_IsNotFound_ByNameOrEmail` | none, survivor found | RED |
| AC-006 | Excluded person | Integration | `StudentSearchSourceTests` | `AnExcludedPerson_IsNotFound_ByTheirEmail` | none, control found | RED |
| AC-006 | Former surname and email | Integration | `StudentSearchSourceTests` | `AFormerSurname_IsNotFound` | none, current found | RED |
| AC-006 | Erased organizer | Integration | `StudentSearchSourceTests` | `AnErasedOrganizer_IsNotFound_AndASurvivorStillFinds` | none, survivor found | RED |
| AC-006 | Erasure end to end | HTTP | `SearchResultsPageTests` | `AnErasedPerson_AndAMeetingWithThemAsOrganizer_AreNotFound` | groups before, nothing after | RED |
| AC-007 | Admin and Dean get results; read-only mode | Security | `SearchAccessTests` | allowed theory, read-only fact | `200`, four groups | RED |
| AC-007 | Anonymous | Security | `SearchAccessTests` | `Anonymous_IsSentToSignIn` | redirect | RED |
| AC-007 | Restricted session | Security | `SearchAccessTests` | `ARestrictedDean_IsSentToTheForcedChange` | redirect | GUARD |
| AC-007 | Policy of the endpoint | Security | `PermissionMatrixTests` | existing matrix, new row | `ViewCourses` | RED |
| AC-007 | No Google, no write, no audit | Security, Integration | `SearchAccessTests`, `StudentSearchSourceTests` | no-Google fact, `Searching_WritesNothing` | 0, unchanged | RED |
| AC-007 | The text in no log line, every state | Security | `SearchAccessTests` | log fact | none | RED |
| AC-007 | `no-store`, `Referrer-Policy` | Security | `SearchAccessTests` | header facts | present | RED |
| AC-007 | Bound parameter, fixed commands | Integration | `StudentSearchSourceTests` | `TheWord_IsABoundParameter`, `TheNumberOfWords_DoesNotChangeTheNumberOfCommands`, `TheCoursesOfTheShownStudents_TakeOneCommand_AndNoneForNoStudent`, `TheFirstMatches_TakeThreeCommands_WithTheWordsBound` | counts | RED |
| AC-008 | In every state, small text; in English | HTTP | `SearchResultsPageTests` | `TheWarning_ShowsInEveryState_InSmallText`, `TheWarning_IsInTheUsersLanguage` | text, class | RED |
| AC-008 | Exact texts | Source scan | `SearchTranslationTests` | exact-text theory | values | RED |
| AC-009 | Hint text of each kind, both languages, every state | HTTP | `PageSearchHintTests` | adapted existing theories | new texts | RED |
| AC-009 | Same label as the header field | HTTP | `PageSearchHintTests` | `TheHint_NamesTheSameLabelAsTheHeaderField` | equal | RED |
| AC-009 | No final period | Source scan | `PageSearchHintTests`, `SearchTranslationTests` | `TheHint_HasNoFinalPeriod`, `NoNewText_EndsWithAPeriod` | none | RED |
| AC-009 | Templates and no literal in views | Source scan | `PageSearchHintTranslationTests`, `SearchTranslationTests` | template facts, no-literal facts | values | RED |
| AC-009 | The results page has no hint (I-3) | HTTP | `SearchResultsPageTests` | `TheResultsPage_HasNoHint` | none | RED |
| AC-010 | Data and texts of section 9 | Measurement | `SearchMeasurementTests` | `TheSearch_OnTheExpectedSize_IsMeasured` | counts; times recorded | MEASURE |
| AC-011 | The rule in both hosts' shared part, same values | Source scan | `DisabledButtonRuleTests` | `TheRule_IsInTheSharedPart`, `BothHosts_HaveTheSameValues` | present, equal | RED |
| AC-011 | Grey, readable | Source scan | `DisabledButtonRuleTests` | `TheBackgroundAndBorder_AreGrey`, `TheText_IsReadable` | values | RED |
| AC-011 | Enabled buttons unchanged | Source scan | `DisabledButtonRuleTests` | `NoEnabledButton_GetsTheseVariables` | none | GUARD |
| AC-011 | A person sees it, computer and phone; the header field too | Manual | implementation report | Owner's walk | as FR-012, FR-001 | MANUAL |
| AC-012 | Malformed code with a marker: nowhere; text and back link stay; both roles | HTTP | `ErrorPageHeaderTests` | `AMalformedCode_OnTheCourseChoice_LeavesTheMarkerNowhere` | no marker | RED (the defect shown) |
| AC-012 | The user's language kept | HTTP | `ErrorPageHeaderTests` | `TheErrorPage_StaysInTheUsersLanguage` | English, no switcher | RED |
| AC-012 | Other pages' switchers unchanged | HTTP | `ErrorPageHeaderTests` | `AnOrdinaryPage_KeepsItsSwitcher_AndItsFallback` | switcher, fallback | GUARD |
| AC-013 | Markup text through the list, `MarkForm`, confirm and choice forms | HTTP | `MarkupTextSurvivesActionsTests` | methods (a), (b), (c) | encoded, equal | GUARD |
| AC-013 | `GET` course-choice, unknown code, valid `returnQ` | HTTP | `MarkupTextSurvivesActionsTests` | method (d) | `404`, kept | GUARD |
| AC-013 | Teacher list: invalid `status` or `page` with a valid `q` | HTTP | `MarkupTextSurvivesActionsTests` | method (e) | `400`, kept | GUARD |
| AC-013 | Script tests fail if the behaviour goes | Source scan | `PickerNarrowScriptTests` | `TheScript_HidesAnEmptyBlock`, `TheScript_DisablesTheSubmit`, `WithoutTheLine_TheEmptyBlockCheckFails`, `WithoutTheLine_TheSubmitCheckFails` | code only | GUARD |
| AC-013 | Summaries cite the right FR-009 items | Review | `PickerNarrowScriptTests` | the class's summaries | corrected | GUARD |
