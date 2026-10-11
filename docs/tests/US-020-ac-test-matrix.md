---
artifact_type: ac_test_matrix
story: US-020
version: 1
status: DRAFT
created_at: 2026-10-11T16:00:00Z
updated_at: 2026-10-11T16:00:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-020-spec.md
    version: 1
  - path: docs/designs/api/US-020-openapi.yaml
    version: 1
  - path: docs/designs/database/US-020-db-design.md
    version: 1
  - path: docs/tests/US-020-test-strategy.md
    version: 1
supersedes: null
---

# US-020 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests`): **RV** `Application.UseCases.CourseListRequestValidationTests`,
**Q** `Application.UseCases.CourseListQueryTests`, **PN** `Application.UseCases.CourseListPersonNameTests`,
**U** `Application.UseCases.CourseListUrlTests`, **S** `Infrastructure.Persistence.CourseListSourceTests`,
**SC** `Infrastructure.Persistence.CourseListSchemaTests`, **CM** `Web.Persistence.CourseMembershipSchemaTests`,
**P** `Web.Pages.CourseListPageTests`, **A** `Web.Security.CourseListAuthorizationTests`,
**RO** `Web.UseCases.CourseListReadOnlyTests`, **T** `Web.Localization.CourseListTranslationTests`,
**L** `Web.Logging.CourseListLoggingTests`.

Status: all **RED (expected)** before IMPLEMENTATION — see the test generation report.

| AC | Scenario | Level | Class | Test method | Expected result |
|---|---|---|---|---|---|
| AC-001 | row columns from the port | Unit | Q | `TheRow_CarriesEveryColumnOfTheCourse` | name, section, state, ordered teachers, 3 students, local time |
| AC-001 | empty teacher/student/section/time | Unit | Q | `ACourseWithoutTeachersStudentsSectionOrUpdateTime_HasEmptyValues` | [], 0, null, null |
| AC-001 | current students only | Unit | Q | `TheStudentCount_IsTheCurrentStudentsOnly` | 2 |
| AC-001 | record as stored | Integration | S | `ThePageRecord_CarriesTheStoredValues` | stored values |
| AC-001 | Q5 current teachers | Integration | S | `CurrentTeachers_AreOfTheGivenCoursesOnly_InOrder` | given courses, by label, no former |
| AC-001 | Q6 current students | Integration | S | `StudentCounts_AreTheCurrentStudentsOfTheGivenCourses` | 3, 1, absent |
| AC-001 | page row rendered | Host | P | `TheRow_ShowsEveryColumn` | every column, Kyiv 01:30 |
| AC-002 | default ACTIVE | Unit | RV | `NoParameters_GiveTheDefaults` | filter ACTIVE |
| AC-002 | default ACTIVE | Unit | Q | `NoStatus_FiltersActive` | archived absent |
| AC-002 | default ACTIVE over HTTP | Host | P | `ByDefault_OnlyActiveCoursesAreListed` | Фізика, Хімія |
| AC-003 | each token | Unit | RV | `AStatusToken_IsAccepted_CaseInsensitively` | effective status |
| AC-003 | state reaches port | Unit | Q | `AStateToken_FiltersThatState`, `All_FiltersNoState` | state / null |
| AC-003 | state filter in SQL | Integration | S | `TheStateFilter_KeepsThatState_AndNullKeepsAll` | counts 2/1/1/0/4 |
| AC-003 | over HTTP | Host | P | `AChosenStatus_OrAll_ListsThoseCourses` | archived / provisioned / all |
| AC-004 | owner + co-teacher, former excluded | Unit | Q | `OwnerAndCoTeacher_AreBothOffered_AndBothFindTheCourse`, `AChosenTeacher_ReachesThePort` | both offered, course found |
| AC-004 | former teacher / student unknown | Unit | RV | `AFormerTeacher_AndAStudent_AreUnknownTeachers` | 404 outcome |
| AC-004 | Q1 choice | Integration | S | `TheTeacherChoice_IsTheCurrentTeachersOfAnyCourse_EachOnce`, `TheTeacherChoice_IsOrderedByTheLabelKey` | current teachers once, label order |
| AC-004 | teacher filter in SQL | Integration | S | `TheTeacherFilter_KeepsTheCoursesTheTeacherTeachesNow` | current only |
| AC-004 | over HTTP | Host | P | `TheTeacherChoice_AndFilter_UseTheCurrentTeachers`, `AFormerTeachersId_Is404` | options, filter, 404 |
| AC-005 | name rule | Unit | PN | `TheName_FollowsTheRule_InTheRowAndTheChoice` (10 cases) | profile / part / email / no name, unchanged |
| AC-005 | I-3 emails | Unit | PN | `SameLabelledChoices_CarryTheirEmails`, `ASameLabelledChoiceWithoutAnEmail_CarriesNone`, `Rows_ListNamesOnly_EvenForSameLabelledTeachers`, `TheChoice_ListsEachTeacherOnce_InThePortsOrder` | emails on duplicates only |
| AC-005 | over HTTP | Host | P | `ATeacher_IsShownByProfileName_OrByEmail`, `SameNamedTeachers_AreToldApartByEmail_InTheChoice` | surname first, email fallback |
| AC-006 | trimmed search to port | Unit | Q / RV | `TheSearchText_ReachesThePortTrimmed`, `TheSearchText_IsTrimmed_AndWhiteSpaceOnlyIsNoSearch` | trimmed / none |
| AC-006 | case-insensitive substring in SQL | Integration | S | `TheSearch_IsACaseInsensitiveSubstringOfTheNameOrTheSection` | name or section, Ukrainian folding only |
| AC-006 | over HTTP | Host | P | `TheSearch_FindsTheNameOrTheSection_InAnyCase` | ІСТ, 9-б |
| AC-007 | combined | Unit | Q | `AllThreeFilters_ReachThePortTogether` | one course |
| AC-007 | combined in SQL | Integration | S | `TheFilters_Combine` | one course |
| AC-007 | over HTTP | Host | P | `TheFilters_Combine` | Фізика; NoMatch |
| AC-008 | port order kept | Unit | Q | `TheRows_KeepThePortsOrder` | Ukrainian order |
| AC-008 | paging | Unit | Q | `Page2_Of120Courses_IsCourses51To100`, `Page3_Of120Courses_IsCourses101To120`, `APagePastTheLast_ShowsTheLastPage`, `ThePageCount_IsTheCeilingOfTotalOver50` | offsets, counts |
| AC-008 | URL rule | Unit | U | all 9 methods | canonical URLs |
| AC-008 | database paging | Integration | S | `ThePage_SkipsAndTakesInTheDatabase` | 51–100, 101–120 |
| AC-008 | over HTTP | Host | P | `Paging_Shows50PerPage_AndKeepsTheFilters`, `OnePage_HasNoPagingLinks` | 50 rows, links keep filters |
| AC-009 | links, no Classroom link | Host | P | `EachRow_LinksToTheJournalAndTheMeetReports_AndNotToClassroom` | both links, no alternate link |
| AC-010 | return path on 200 | Unit | Q | `OnAListedPage_TheReturnPath_IsTheCanonicalUrl` | canonical |
| AC-010 | return path after refusal | Unit | RV | `AfterARefusal_TheReturnPath_OmitsTheRejectedParameters_AndThePage` | rejected and page dropped |
| AC-010 | over HTTP | Host | P | `TheAddress_CarriesTheFiltersOnly_AndTheSwitcherReturnsToTheValidatedUrl`, `AfterARefusal_TheSwitcherDropsTheRejectedValue` | four parameters, id only, no echo |
| AC-011 | empty states | Unit | Q | `NoCourseStored_IsTheNoCoursesStoredState`, `NothingMatching_IsTheNoMatchState`, `AListWithRows_HasNoEmptyState` | NoCoursesStored / NoMatch |
| AC-011 | Q3 | Integration | S | `AnyCourseStored_IsFalseOnAnEmptyDatabase_AndTrueWithOneCourse` | false / true |
| AC-011 | over HTTP | Host | P | `NoCourseSynchronized_ShowsItsOwnMessage`, `NothingMatching_ShowsTheOtherMessage` | own message, no table |
| AC-012 | home page first link | Host | P | `Courses_IsTheFirstLinkOfTheWorkspaceSection` (Admin, Dean, read-only) | first link /workspace/courses |
| AC-013 | each VR rule | Unit | RV | `AMalformedStatus_IsRefused`, `ARepeatedStatus_IsRefused`, `AMalformedTeacherId_IsRefused`, `ARepeatedTeacherId_IsRefused`, `TheLargestId_IsWellFormed_AndUnknown`, `AnUnknownTeacherId_IsRefusedWith404`, `AMalformedStatus_WithAnUnknownTeacher_Is400WithTheStatusKeyOnly`, `OneHundredCharacters_AreAccepted_AndOneHundredAndOneAreTooLong`, `AControlCharacter_IsRefused`, `OtherCharacters_AreAccepted`, `ARepeatedSearchText_IsSearchRepeated_BeforeAnyOtherRule`, `ATooLongSearchText_WithAControlCharacter_IsSearchTooLongOnly`, `APageNumberInRange_IsAccepted`, `AMalformedPage_IsRefused`, `ARepeatedPage_IsRefused`, `EveryMalformedParameter_IsReported_InParameterOrder`, `OnARefusal_ValidFieldsKeepTheirValues`, `TheTeacherChoice_IsInTheModel_OnEveryAnswer`, `EmptyValues_AreAbsent` | keys, outcome, no course read |
| AC-013 | no echo in model | Unit | RV | `ARejectedValue_AppearsNowhereInTheModel` | absent |
| AC-013 | over HTTP | Host | P | `AMalformedParameter_Is400_WithItsMessage_AndNoEcho`, `ARepeatedOrControlCharacterSearch_HasItsOwnMessage`, `SeveralMalformedParameters_ShowEveryMessage_InOrder`, `AnUnknownTeacherId_Is404` | 400/404 page, messages, no echo |
| AC-013 | not logged | Host | L | `AMalformedParameter_LogsAWarningNamingIt_ButNotItsValue`, `AnUnknownTeacher_LogsAWarningNamingTheParameter_ButNotItsValue`, `TheSearchText_IsNeverLogged`, `AListedPage_LogsTheTotal_AndNoCourseOrPersonData` | parameter named, value absent |
| AC-014 | query is a read | Unit | Q | `TheQuery_DependsOnTheReadPortAndTheTimeZoneOnly` | port + time zone only |
| AC-014 | all BR-025 causes | Integration | RO | `TheUseCase_ReturnsTheSeededCourses_WritesNothing_AndCallsNoGoogle`, `ThePage_ShowsTheCourses_WritesNothing_AndCallsNoGoogle` | list shown, fingerprint unchanged, 0 Google calls |
| AC-014 | source writes nothing | Integration | S | `Reading_WritesNothing` | fingerprint unchanged |
| AC-015 | roles | Security | A | all 5 methods | Admin/Dean 200; anonymous and temporary-password 302; ViewCourses policy |
| AC-016 | bilingual | Host | T | all 4 methods | keys in uk/en; page language; Google data unchanged |
| AC-016 | Google data unchanged | Unit | PN | `TheName_FollowsTheRule_InTheRowAndTheChoice` (re-casing cases) | as stored |
| AC-017 | no Google call | Host | P / RO | `TheRow_ShowsEveryColumn`, RO methods | 0 calls; synthetic data only |
| AC-018 | Kyiv time | Unit | Q | `TheUpdateTime_IsShownInTheSchoolsTimeZone_OnBothSidesOfSummerTime` | 01:30 on 1 April; 23:30 on 31 October |
| AC-018 | over HTTP | Host | P | `TheRow_ShowsEveryColumn` | 01.04.2026 01:30 |
| AC-019 | Ukrainian order | Integration | S | `ThePage_IsInUkrainianOrder_ThenSection_ThenId` | Алгебра, Географія, Ґрунтознавство, Інформатика, Історія; Cyrillic before Latin |
| AC-019 | ІСТ and % | Integration | S | `TheSearch_IsACaseInsensitiveSubstringOfTheNameOrTheSection`, `TheSearch_IsLiteral` | Історія; only names containing % |
| AC-019 | collation | Schema | SC | `TheCollation_FoldsAndOrdersUkrainian`, `TheCollation_IsADeterministicUkrainianIcuCollation`, `NoColumn_UsesTheCollation` | ICU uk-UA, deterministic |
| AC-020 | fixed calls | Unit | Q | `TheNumberOfReads_IsFixed_AndTheSameForPage1AndPage3` | 5 and 5 |
| AC-020 | one command per read | Integration | S | `EveryRead_IsOneDatabaseCommand` | 1 each |
| AC-020 | index (FR-016) | Schema | SC / CM | `TheMigration_ExistsUnderTheDesignedName`, `TheCurrentTeacherIndex_IsPartialOnCurrentTeachers`, `TheRequiredIndexes_Exist` (modified) | migration, partial index |

Every Acceptance Criterion AC-001 … AC-020 has at least one mapped test. The
endpoint `GET /workspace/courses` has tests for 200, 302 (both), 400 and 404
(TC-3); `GET /` for its additive link (AC-012).
