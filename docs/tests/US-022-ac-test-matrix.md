---
artifact_type: ac_test_matrix
story: US-022
version: 1
status: DRAFT
created_at: 2026-10-12T09:40:00Z
updated_at: 2026-10-12T09:40:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-022-spec.md
    version: 1
  - path: docs/designs/api/US-022-openapi.yaml
    version: 1
  - path: docs/designs/database/US-022-db-design.md
    version: 1
  - path: docs/tests/US-022-test-strategy.md
    version: 1
supersedes: null
---

# US-022 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests`): **Q** `Application.UseCases.TeacherListQueryTests`,
**U** `Application.UseCases.TeacherListUrlTests`, **S** `Infrastructure.Persistence.TeacherListSourceTests`,
**P** `Web.Pages.TeacherListPageTests`, **A** `Web.Security.TeacherListAuthorizationTests`,
**RO** `Web.UseCases.TeacherListReadOnlyTests`, **T** `Web.Localization.TeacherListTranslationTests`,
**L** `Web.Logging.TeacherListLoggingTests`.

Status: all **RED (expected)** before IMPLEMENTATION — see the test generation report.

| AC | Scenario | Level | Class | Test method | Expected result |
|---|---|---|---|---|---|
| AC-001 | default asks the port for ACTIVE | Unit | Q | `ByDefault_ThePortIsAskedForActive` | Active, one count + one page read |
| AC-001 | empty status/page are defaults | Unit | Q | `AnEmptyStatusAndPage_AreTheDefaults` | Shown, Active, page 1 |
| AC-001 | who is listed per status, counts | Integration | S | `EachStatus_ListsItsCurrentTeachers_WithTheirCountsOfThatStatus` | ACTIVE [Українка 1, Шевченко 2]; no archived-only, former, student |
| AC-001 | over HTTP | Integration | P | `TheDefaultList_ShowsActiveTeachers_WithNameEmailAndCount` | 2 rows; Коваль, Франко, Мельник absent |
| AC-002 | each token → state, any case | Unit | Q | `EachStatusToken_IsHandedToThePort` | state / null for all |
| AC-002 | ARCHIVED, all, counts | Integration | S | `EachStatus_ListsItsCurrentTeachers_WithTheirCountsOfThatStatus` | ARCHIVED [Коваль 1, Українка 1]; all [1, 2, 2] |
| AC-002 | "all" = sum of states | Integration | S | `UnderAll_TheCount_IsTheSumOverTheStates` | 6 / 2 / 1 / 1 |
| AC-002 | over HTTP | Integration | P | `AChosenStatus_OrAll_ListsThoseTeachers_WithCountsOfThatStatus` | rows and counts per status |
| AC-003 | row: name, email, count | Unit | Q | `TheRow_CarriesTheNameTheEmailAndTheCount` | profile name, email as stored, 2 |
| AC-003 | name rule fallbacks | Unit | Q | `TheNameRule_FallsBackToTheEmail_ThenToNoName` | one part, email, NoName; empty = absent |
| AC-003 | stored values projected | Integration | S | `TheRecord_CarriesTheStoredValues` | exact record |
| AC-003 | email / placeholder / case kept | Integration | P | `ATeacherWithoutAName_IsShownByEmail_OrThePlaceholder` | email, placeholder, mixed case unchanged |
| AC-004 | link per status | Unit | Q | `TheRowLink_IsTheCourseListOfThatTeacherAndStatus` | `/workspace/courses?[status=…&]teacherId=id` |
| AC-004 | link followed | Integration | P | `EachRow_LinksToTheCourseListOfThatTeacher_AndTheListShowsTheCountedCourses` | 200, exactly the counted courses |
| AC-005 | 50 per page | Unit | Q | `Paging_Is50PerPage_FromThePort` | 51–100, 101–120, 3 pages |
| AC-005 | past the last | Unit | Q | `APagePastTheLast_ShowsTheLast` | page 3 |
| AC-005 | database paging by label | Integration | S | `Paging_HappensInTheDatabase_InLabelOrder` | label order ≠ id order |
| AC-005 | over HTTP, status kept | Integration | P | `Paging_Shows50PerPage_SortedByName_AndKeepsTheStatus` | rows, total 120, links |
| AC-005 | single page | Integration | P | `OnePage_HasNoPagingLinks` | no links |
| AC-006 | URL rule | Unit | U | `Build_WritesStatusThenPage_OmittingDefaults`, `ThePath_IsTheTeacherListRoute` | canonical URLs |
| AC-006 | return path validated | Unit | Q | `TheReturnPath_IsTheValidatedUrl` | status/page of the shown page |
| AC-006 | address and every URL on the page | Integration | P | `TheAddress_CarriesStatusAndPageOnly_AndTheSwitcherReturnsToTheValidatedUrl` | only status/page, status/teacherId; no `@`, name |
| AC-007 | home page second link | Integration | P | `Teachers_IsTheSecondLinkOfTheWorkspaceSection` | Courses, Teachers, …Journal; Admin, Dean, read-only |
| AC-008 | empty state, no page read | Unit | Q | `NobodyListed_IsTheEmptyState_WithoutAPageRead` | IsEmpty, page 1, 1 read |
| AC-008 | empty school | Integration | S | `AnEmptySchool_ListsNobody` | 0, [] |
| AC-008 | over HTTP, empty school | Integration | P | `NobodyListed_ShowsTheMessage_WithoutATable` | message, no table |
| AC-008 | archived-only school, default ACTIVE | Integration | P | `OnlyArchivedCourses_ShowTheMessage_UnderTheDefaultStatus` | same message |
| AC-009 | use case, 3 causes + normal | Security | RO | `TheUseCase_ReturnsTheSeededTeachers_WritesNothing_AndCallsNoGoogle` | Shown, no write, 0 Google calls |
| AC-009 | page, 3 causes + normal | Security | RO | `ThePage_ShowsTheTeachers_WritesNothing_AndCallsNoGoogle` | 200, no write, 0 Google calls |
| AC-009 | source writes nothing | Integration | S | `Reading_WritesNothing` | fingerprint unchanged |
| AC-010 | Admin and Dean allowed | Security | A | `AnAdminAndADean_CanOpenTheTeacherList` | 200 with data |
| AC-010 | anonymous refused | Security | A | `Anonymous_IsSentToSignIn` | 302 /sign-in, nothing written |
| AC-010 | forced password change | Security | A | `ADeanWithATemporaryPassword_IsSentToTheForcedChange` | 302 change-password |
| AC-010 | endpoint declares ViewCourses | Security | A | `ThePath_IsAProtectedEndpoint_WithTheViewCoursesPolicy` | one protected endpoint |
| AC-011 | keys in uk and en | Localization | T | `EveryKey_ExistsInBothLanguages_NonEmpty_AndTheTextsDiffer` | new + reused keys |
| AC-011 | page in both languages, data unchanged | Localization | T | `ThePage_RendersInTheChosenLanguage_AndShowsGoogleDataAsStored` | uk, en |
| AC-011 | status choice translated, in order | Localization | T | `TheStatusChoice_OffersTheStatesInTheUiLanguage_InOrder` | six options |
| AC-011 | empty message translated | Localization | T | `TheEmptyMessage_IsShownInEnglish` | en text |
| AC-012 | no Google call | Security | RO, P | (all RO tests), `TheDefaultList_ShowsActiveTeachers_WithNameEmailAndCount` | `GoogleCallCount() == 0` |
| AC-012 | synthetic data only | — | all | fixtures `TeacherListHostExtensions`, `FakeTeacherListSource` | invented names, `school.example` |
| AC-013 | malformed status | Unit | Q | `AMalformedStatus_IsRefused_BeforeAnyRead` | Invalid, no read |
| AC-013 | malformed page | Unit | Q | `AMalformedPage_IsRefused_BeforeAnyRead` | Invalid, no read |
| AC-013 | both malformed | Unit | Q | `BothMalformed_GiveBothMessages_InOrder` | two keys |
| AC-013 | rejected value dropped | Unit | Q | `AfterARefusal_TheReturnPathDropsTheRejectedValue` | default return path |
| AC-013 | 400 page, no echo | Integration | P | `AMalformedParameter_Is400_WithItsMessage_AndNoEcho` | 400, message, no list |
| AC-013 | both over HTTP | Integration | P | `BothMalformed_ShowBothMessages_InOrder` | two messages |
| AC-013 | switcher and form after refusal | Integration | P | `AfterARefusal_TheRejectedValueIsDropped_FromTheSwitcherAndTheForm` | validated return path |
| AC-013 | ignored parameters not echoed | Integration | P | `TheAddress_CarriesStatusAndPageOnly_AndTheSwitcherReturnsToTheValidatedUrl` | no echo |
| AC-013 | Warning without the value | Logging | L | `AMalformedParameter_LogsAWarningNamingIt_ButNotItsValue` | Warning, no value |
| AC-013 | ignored parameters not logged | Logging | L | `IgnoredParameters_AreNeverLogged` | no value in log |
| AC-013 | Information without personal data | Logging | L | `AListedPage_LogsTheTotal_AndNoTeacherData` | total; no name, email |
| AC-014 | port order kept | Unit | Q | `TheRows_KeepThePortsOrder` | spec order |
| AC-014 | Ukrainian label order | Integration | S | `TheOrder_IsTheLabelKey_InTheUkrainianAlphabet` | Андрієнко, Гнатюк, Ґудзь, Іваненко, zoe@, nameless |
| AC-014 | same as course list's choice | Integration | S | `TheOrder_EqualsTheCourseListsTeacherChoice` | equal sequences |
| AC-015 | two reads per shown page | Unit | Q | `AShownPage_ReadsTwice` | 1 count + 1 page, any size/page |
| AC-015 | one command per call | Integration | S | `EveryRead_IsOneDatabaseCommand` | [1, 1, 1, 1] |
| AC-015 | pages 2, 3 | Unit, Integration | Q, S, P | `Paging_Is50PerPage_FromThePort`, `Paging_HappensInTheDatabase_InLabelOrder`, `Paging_Shows50PerPage_SortedByName_AndKeepsTheStatus` | 51–100, 101–120 |
| AC-016 | count = course list's filter | Integration | S | `TheCount_AgreesWithTheCourseListsTeacherFilter` | 2; equal for every listed teacher/status; all in the choice |
| AC-016 | link followed, not refused | Integration | P | `EachRow_LinksToTheCourseListOfThatTeacher_AndTheListShowsTheCountedCourses` | 200, [Фізика, Хімія] |
| S-09 | HTML encoding | Security | P | `NamesAndEmails_AreHtmlEncoded` | encoded |

Every Acceptance Criterion has at least one mapped test. No criterion is
untestable.
