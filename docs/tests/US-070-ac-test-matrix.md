---
artifact_type: ac_test_matrix
story: US-070
version: 1
status: DRAFT
created_at: 2026-11-03T07:00:13Z
updated_at: 2026-11-03T07:00:13Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-070-search-in-lists-and-pickers.md
    version: null
  - path: docs/specifications/US-070-spec.md
    version: 4
  - path: docs/tests/US-070-test-strategy.md
    version: 1
supersedes: null
---

# US-070 Acceptance Criteria to Test Matrix

Status: **RED** fails until IMPLEMENTATION; **GUARD** green by design before and after (the reason is in the test generation report); **MANUAL** the Owner's walk, recorded in the implementation report.

Classes: `SearchMatchingSourceTests` in `Infrastructure.Persistence`; `TeacherListSearchPageTests`, `MeetCodesSearchPageTests`, `MeetCodeSearchSurvivesActionsTests`, `CourseListSearchPageTests`, `PickerNarrowMarkupTests`, `PageSearchHintTests` in `Web.Pages`; `ListSearchAccessTests` in `Web.Security`; `ListSearchTranslationTests`, `PageSearchHintTranslationTests` in `Web.Localization`; `PickerNarrowScriptTests` in `Architecture`.

| Acceptance Criterion | Scenario | Test Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Words in any order, parts of words, any case | Integration | `SearchMatchingSourceTests` | `ATeacher_IsFound_ByWordsInAnyOrder_AsPartsOfWords_InAnyCase` | only that teacher | RED |
| AC-001 | Words across fields; every word must be found; a typo finds nothing | Integration | `SearchMatchingSourceTests` | `Words_LandInDifferentFields_AndEveryWordMustBeFound` | found / none | RED |
| AC-001 | Six apostrophe pairs on three sources | Integration | `SearchMatchingSourceTests` | `ApostropheVariants_CountAsOneCharacter` | found | RED |
| AC-001 | Other apostrophe-like characters literal (I-2) | Integration | `SearchMatchingSourceTests` | `OtherApostropheLikeCharacters_StayLiteral` | none | RED |
| AC-001 | `%`, `_`, `\` literal | Integration | `SearchMatchingSourceTests` | `PatternCharacters_MatchOnlyThemselves` | only the holder | RED |
| AC-001 | Hyphens ignored in a code | Integration | `SearchMatchingSourceTests` | `ACode_IsFound_WithItsHyphensIgnored` | that code | RED |
| AC-001 | Hyphen-only word finds no code, may match another field | Integration | `SearchMatchingSourceTests` | `AWordOfHyphensOnly_FindsNoCode_ButMayMatchAnotherField` | linked code by section only | RED |
| AC-001 | A hyphen elsewhere is ordinary | Integration | `SearchMatchingSourceTests` | `OutsideACodeField_AHyphenIsOrdinary` | `10-а` yes, `10а` no | GUARD |
| AC-002 | Name and section, words across both | Integration | `SearchMatchingSourceTests` | `ACourse_IsFound_ByNameAndSection_WithWordsAcrossBoth` | counts | RED |
| AC-002 | Status and teacher AND the search | Integration | `SearchMatchingSourceTests` | `OnTheCourseList_StatusAndTeacher_CombineWithTheSearch` | counts | GUARD |
| AC-002 | Word order and case over HTTP | HTTP | `CourseListSearchPageTests` | `TheWords_AreMatchedInAnyOrderAndCase_OverNameAndSection` | only the course | RED (one row GUARD) |
| AC-002 | Apostrophes over HTTP | HTTP | `CourseListSearchPageTests` | `ApostropheForms_AreOne` | found | RED |
| AC-002 | No match shows the existing message | HTTP | `CourseListSearchPageTests` | `AWordThatMatchesNothing_ListsNoCourse_AndShowsTheNoMatchMessage` | message | GUARD |
| AC-002 | Filters AND the search over HTTP | HTTP | `CourseListSearchPageTests` | `TheStatusAndTeacherFilters_AreAndedWithTheSearch` | rows | GUARD |
| AC-003 | Teacher on a later page by surname, given name, email; total counts matches | HTTP | `TeacherListSearchPageTests` | `ATeacherOnALaterPage_IsFound_AndTheTotalCountsTheMatchesOnly` | row, total 1 | RED |
| AC-003 | Status filter AND | HTTP | `TeacherListSearchPageTests` | `TheStatusFilter_AppliesWithTheSearch` | rows | RED |
| AC-003 | Who is listed does not change | HTTP | `TeacherListSearchPageTests` | `WhoIsListed_DoesNotChange_ForASearch` | no student, no former teacher | RED |
| AC-003 | Status AND and count at source | Integration | `SearchMatchingSourceTests` | `TheSearch_CombinesWithTheStatus_AndCountsMatchesOnly`, `AnAbsentValue_IsNotSearched_AndTheOtherFieldsStillFind` | rows | RED |
| AC-003 | No match message (FR-006) | HTTP | `TeacherListSearchPageTests` | `EmptyMessages_DifferBetweenASearchAndNone` | new text / existing text | RED |
| AC-004 | Counters and rows on each list | HTTP | `MeetCodesSearchPageTests` | `UnderASearch_EveryListCounts_AndShowsItsOwnMatchesOnly`, `TheCounters_FollowTheSearch` | counters, rows | RED |
| AC-004 | By code, course name and section, organizer | HTTP | `MeetCodesSearchPageTests` | `ACode_IsFound_ByEveryFieldOfTheFeature` | that code | RED |
| AC-004 | What each list searches, counts agree | Integration | `SearchMatchingSourceTests` | `EachList_HoldsItsMatches_AndTheCountsAgree`, `ThePagedReads_TotalTheMatchesOnly`, `AnyStoredSessionOfACode_LendsItsOrganizer` | counts, rows | RED |
| AC-004 | Candidates and acting accounts not searched | Integration | `SearchMatchingSourceTests` | `ACandidateCourse_IsNotSearched`, `TheAccountsThatActedOnACode_AreNotSearched` | none | RED |
| AC-004 | Organizers column (OD-001 a) | HTTP | `MeetCodesSearchPageTests` | `LinkedAndNotACourse_ShowTheOrganizers_RightAfterTheCode`, `ACodeWithNoOrganizerLeft_HasAnEmptyCell` | column 2, emails, empty cell | RED |
| AC-004 | Organizers on rows at source | Integration | `SearchMatchingSourceTests` | `LinkedAndMarkedRows_CarryDistinctOrganizers_InOrdinalOrder`, `UnderASearch_ARow_StillListsAllItsOrganizers` | distinct, ordinal, all | RED |
| AC-004 | No match message on each list | HTTP | `MeetCodesSearchPageTests` | `EmptyMessages_DifferBetweenASearchAndNone` | new text / existing text | RED |
| AC-005 | Erased organizer not found, with a before-erasure control | Integration | `SearchMatchingSourceTests` | `AnErasedOrganizer_FindsNothing_OnAnyList_AndASurvivorStillFinds` | counts 0, survivor 1 | RED |
| AC-005 | Not found and not shown, on each list | HTTP | `MeetCodesSearchPageTests` | `AnErasedOrganizer_IsNotFound_AndNotShown` | no row, email absent | RED |
| AC-006 | Teacher paging links, return path, clamp | HTTP | `TeacherListSearchPageTests` | `PagingLinks_CarryTheSearch_InTheStatedOrder`, `APagePastTheLast_ShowsTheLast_UnderASearch` | URLs | RED |
| AC-006 | Teacher language switch | HTTP | `TeacherListSearchPageTests` | `SwitchingLanguage_ReturnsToTheSameSearch` | same search, English | RED |
| AC-006 | Teacher row link drops it (I-12) | HTTP | `TeacherListSearchPageTests` | `ARowsCourseLink_DoesNotCarryTheSearch` | no `q` | RED |
| AC-006 | Encoding in URL and markup | HTTP | `TeacherListSearchPageTests` | `TheText_IsEncoded_InTheAddressAndInTheField` | round trip, no raw markup | RED |
| AC-006 | Meet list and paging links, clamp (I-6), language | HTTP | `MeetCodesSearchPageTests` | `ListLinks_CarryTheSearch_AndResetThePage`, `PagingLinks_CarryTheSearch_AndWalkTheMatches`, `APagePastTheLast_ShowsTheLast`, `APagePastTheEnd_OfNoMatches_IsPageZero`, `SwitchingLanguage_ReturnsToTheSameSearch` | URLs, rows | RED |
| AC-006 | Row forms and links carry it | HTTP | `MeetCodeSearchSurvivesActionsTests` | `AnUnassignedRow_CarriesTheSearch`, `ALinkedRow_CarriesTheSearch_InEveryAction`, `AMarkedRow_CarriesTheSearch` | `returnQ` | RED |
| AC-006 | With no search nothing is emitted | HTTP | `MeetCodeSearchSurvivesActionsTests` | `WithNoSearch_NoReturnQIsEmitted` | no field | GUARD |
| AC-006 | Success redirects with the search | HTTP | `MeetCodeSearchSurvivesActionsTests` | `PickingACourse_RedirectsToTheSearch`, `Confirming_RedirectsToTheSearch`, `Marking_RedirectsToTheSearch` | `list`, `q`, `page` | RED |
| AC-006 | A page left empty shows the last | HTTP | `MeetCodeSearchSurvivesActionsTests` | `APageLeftEmptyByTheAction_ShowsTheLastPage` | 20 rows, field | RED |
| AC-006 | Stale, unknown code, same course, unknown course | HTTP | `MeetCodeSearchSurvivesActionsTests` | `AStaleState_KeepsTheSearch`, `AnUnknownCode_KeepsTheSearch`, `TheSameCourse_KeepsTheSearch_OnTheChoicePage`, `AnUnknownCourse_KeepsTheSearch_OnTheChoicePage` | status, search kept | RED |
| AC-006 | Malformed form: error page, no search | HTTP | `MeetCodeSearchSurvivesActionsTests` | `AMalformedForm_AnswersTheErrorPage_NotTheSearch` | `400`, no field | GUARD |
| AC-006 | Choice page, Cancel (I-7), language | HTTP | `MeetCodeSearchSurvivesActionsTests` | `TheChoicePage_CarriesTheSearch_AndCancelReturnsToTheCodesList`, `WithNoSearch_CancelReturnsToTheCodesList`, `SwitchingLanguage_OnTheChoicePage_KeepsTheSearch` | URLs | RED |
| AC-006 | A bad `returnQ` is no search (FR-005.5) | HTTP | `MeetCodeSearchSurvivesActionsTests` | `ARefusedReturnQ_IsNoSearch_AndTheActionGoesOn`, `ARepeatedReturnQ_IsNoSearch_WhereARepeatedKnownFieldIsMalformed` | action done, no text | GUARD |
| AC-006 | A bad `returnQ` on the choice page, with a control | HTTP | `MeetCodeSearchSurvivesActionsTests` | `OnTheChoicePage_ARefusedReturnQ_IsNoSearch` | `200`, no text | RED |
| AC-006 | No row code or email in a query string | HTTP | `MeetCodesSearchPageTests` | `NoLink_PutsARowsCodeOrEmail_IntoAQueryString` | none | RED |
| AC-007 | Refusals on the course list | HTTP | `CourseListSearchPageTests` | `ARefusedText_Answers400_WithTheMessage_AndIsNotEchoed` | `400`, message, no text | GUARD |
| AC-007 | Refusals on the teacher list | HTTP | `TeacherListSearchPageTests` | `ARefusedText_Answers400_WithTheMessage_AndIsNotEchoed`, `EveryFailingParameter_IsReported` | `400`, messages, no text | RED |
| AC-007 | Refusals on the Meet codes (VR-003, VR-004) | HTTP | `MeetCodesSearchPageTests` | `ARefusedText_Answers400_ShowsNoList_AndIsNotEchoed`, `AnInvalidOtherParameter_KeepsTheFallback_WithTheSearchApplied`, `BothInvalid_ShowTheSearchRefusalOnly` | statuses, messages | RED |
| AC-007 | Nothing read with the rejected text, with a control | HTTP | `TeacherListSearchPageTests`, `MeetCodesSearchPageTests` | `ARefusedText_ReachesNoRead_AndAnAcceptedTextReachesThem` | no read / reads with the text | RED |
| AC-007 | Trim, white space, 100 characters | HTTP | `TeacherListSearchPageTests` | `WhiteSpaceIsNoSearch_AndAHundredCharactersAreAllowed` | no search; allowed | RED |
| AC-007, AC-008 | Not logged, accepted and refused | Security | `ListSearchAccessTests` | `TheSearchedText_IsInNoLogLine` | no line holds it | RED (courses GUARD) |
| AC-008 | Admin and Dean get results on each list | Security | `ListSearchAccessTests` | `AnAdminAndADean_CanSearchEveryList` | `200`, field, row | RED (courses GUARD) |
| AC-008 | Anonymous refused on each list | Security | `ListSearchAccessTests` | `Anonymous_IsSentToSignIn` | redirect | GUARD |
| AC-008 | Restricted session refused on each list | Security | `ListSearchAccessTests` | `ARestrictedDean_IsSentToTheForcedChange` | redirect | GUARD |
| AC-008 | Read-only mode allows the search | Security | `ListSearchAccessTests` | `InReadOnlyMode_TheSearchStillWorks` | `200`, field | RED (courses GUARD) |
| AC-008 | Read-only mode still refuses a row action; no text audited | Security | `MeetCodeSearchSurvivesActionsTests` | `InReadOnlyMode_ARowActionIsRefused_AndNoTextIsAudited` | `409`, no text | GUARD |
| AC-008 | No Google, no write, no audit, with a control | Security | `ListSearchAccessTests` | `ASearch_CallsNoGoogleApi_WritesNothing_AndAuditsNothing` | 0 calls, unchanged | RED (courses GUARD) |
| AC-008 | Writes nothing at source | Integration | `SearchMatchingSourceTests` | `Searching_WritesNothing` | unchanged | RED |
| Spec §7 | Bound parameter in every read | Integration | `SearchMatchingSourceTests` | `TheWord_IsABoundParameter_InEveryRead` | not in command text | RED |
| Spec §9 | Commands do not depend on words | Integration | `SearchMatchingSourceTests` | `TheNumberOfWords_DoesNotChangeTheNumberOfCommands` | equal | RED |
| AC-009 | Hooks on the four selects, both languages | HTTP | `PickerNarrowMarkupTests` | `ASelectPicker_CarriesTheHooks_AndLoadsTheScriptOnce` | attributes, one script | RED |
| AC-009 | Hooks on the course-choice page, plain row text | HTTP | `PickerNarrowMarkupTests` | `TheCourseChoice_CarriesTheRadiosHooks_WithThePlainLabelsAsRowText` | attributes, no mark, no percent | RED |
| AC-009 | No field where nothing narrows | HTTP | `PickerNarrowMarkupTests` | `WithNoCourseStored_ThePageHasNoHookAndNoScript`, `ACourseListWithNoTeacherToChoose_HasNoHookAndNoScript`, `ACourseChoiceWithNothingToChoose_HasNoHookAndNoScript` | no hook, no script | GUARD |
| AC-009 | The choice submits as before | HTTP | `PickerNarrowMarkupTests` | `TheJournalSelect_KeepsTheSelectedOption_AndTheEmptyOne` | names, selected | GUARD |
| AC-009 | The script's text: rules, removal, no name, nothing sent or stored, no style, polite announcement, disabled, hidden | Source scan | `PickerNarrowScriptTests` | every method but `NoView_HoldsInlineNarrowingCode` | present / absent | RED |
| AC-009 | No inline code | Source scan | `PickerNarrowScriptTests` | `NoView_HoldsInlineNarrowingCode` | none | GUARD |
| AC-009 | Narrowing on a computer and a phone, scripts off | Manual | implementation report | Owner's walk | as FR-009 | MANUAL |
| AC-010 | Fields: label, type, length, `autocomplete` | HTTP | `TeacherListSearchPageTests`, `MeetCodesSearchPageTests`, `CourseListSearchPageTests` | `TheField_HasItsLabelAndAttributes_AndShowsTheValidatedText`, `TheForm_HasItsFieldsAndKeepsListAndSize`, `TheField_HasItsLabelAndAttributes` | attributes | RED |
| AC-010 | Hint variant and text on each page, each state | HTTP | `PageSearchHintTests` | adapted: `EveryTeachingDataPage_ShowsItsHintOnce_DirectlyUnderTheTitle`, `InEnglish_EveryTeachingDataPage_ShowsTheEnglishText`, `InReadOnlyMode_TheHintIsUnchanged`, `WithNoRowsAtAll_TheHintIsShown`, `ThePagedListHint_IsTheSame_OnOnePage_AndOnEveryPageOfMany` | new texts | RED |
| AC-010 | Translations, template, retired keys, no final period | Source scan | `ListSearchTranslationTests`, `PageSearchHintTranslationTests` | `ANewText_IsAnEntry_WithTheSpecifiedWording`, `NoNewText_EndsWithAPeriod`, `TheListFieldTemplate_IsAnEntry_AndTheRetiredHintKeysAreGone` | exact values | RED |
