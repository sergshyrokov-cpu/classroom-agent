---
artifact_type: ac_test_matrix
story: US-021
version: 1
status: DRAFT
created_at: 2026-10-11T16:30:00Z
updated_at: 2026-10-11T16:30:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-021-spec.md
    version: 1
  - path: docs/designs/api/US-021-openapi.yaml
    version: 1
  - path: docs/designs/database/US-021-db-design.md
    version: 1
  - path: docs/tests/US-021-test-strategy.md
    version: 1
supersedes: null
---

# US-021 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests`): **Q** `Application.UseCases.CourseCardQueryTests`,
**S** `Infrastructure.Persistence.CourseCardSourceTests`, **P** `Web.Pages.CourseCardPageTests`,
**A** `Web.Security.CourseCardAuthorizationTests`, **RO** `Web.UseCases.CourseCardReadOnlyTests`,
**T** `Web.Localization.CourseCardTranslationTests`, **L** `Web.Logging.CourseCardLoggingTests`.

Status: **RED (expected)** before IMPLEMENTATION unless marked otherwise — see the
test generation report for the run and for the tests green by design.

| AC | Scenario | Level | Class | Test method | Expected result |
|---|---|---|---|---|---|
| AC-001 | details mapped unchanged, Kyiv times to the minute | Unit | Q | `TheDetails_AreTheStoredValues_WithTheTimesInTheSchoolsZone` | values as stored, 09:15 / 01:30 |
| AC-001 | blank value is not given (I-1) | Unit | Q | `ABlankOptionalValue_IsNotGiven` (4 cases) | nulls |
| AC-001 | Q1 values | Integration | S | `TheCourse_IsReadWithItsDisplayableValues`, `ACourseWithoutOptionalValues_HasNulls` | stored values / nulls |
| AC-001 | rendered details, no Classroom link, folder, calendar, Google id | Host | P | `TheCard_ShowsTheDetails` | every field and label; markers absent |
| AC-001 | omitted fields and labels | Host | P | `AbsentDetails_AreOmitted_WithTheirLabels` | no element, no label |
| AC-002 | split into sections | Unit | Q | `TheMembers_AreSplitIntoTheThreeSections_InThePortsOrder` | teachers, students, leavers |
| AC-002 | every membership of the course only | Integration | S | `TheMembers_AreEveryMembershipOfTheCourse_AndNoneOfAnother` | 12 rows; 2/7/3; none of Хімія |
| AC-002 | sections and counts rendered | Host | P | `TheSections_ShowTheCurrentTeachersAndStudents_WithTheirCounts` | 2 / 7 / 3 |
| AC-003 | owner mark on a current teacher only | Unit | Q | `OnlyACurrentTeacher_CarriesTheOwnerMark`, `AnOwnerWhoIsNotACurrentTeacher_MarksNoOne` (3 cases) | owner true once / none |
| AC-003 | `is_owner` in SQL | Integration | S | `TheOwner_IsTheParticipantWithTheCoursesOwnerId`, `ACourseWhoseOwnerIsNotInTheRoster_MarksNoOne` (null, unknown) | Шевченко only / none |
| AC-003 | mark rendered | Host | P | `TheOwner_IsMarkedOnTheOwnersRowOnly`, `ACourseWithNoOwner_MarksNoOne`, `ACourseWhoseOwnerIsNotACurrentTeacher_MarksNoOne` | mark on Шевченко / no mark |
| AC-004 | leavers with role and date | Unit | Q | `TheMembers_AreSplitIntoTheThreeSections_InThePortsOrder`, `ASubmitterOnlyStudent_IsALeaverWithNoName` | leavers both roles; submitter NoName, 15 Mar |
| AC-004 | submitter-only student stored off roster | Integration | S | `ASubmitterOnlyStudent_ComesBackOffTheRoster_WithItsLastSeen` | off roster, nameless, instant |
| AC-004 | leavers rendered, not current | Host | P | `TheLeavers_ShowRoleAndLastSeenDate_AndAreNotCurrent` | Мельник, Франко, no name; roles; dates |
| AC-005 | name rule and email | Unit | Q | `TheName_FollowsTheRule_InEverySection` (10 cases), `APersonWithoutAnEmail_HasNoEmail` | profile / part / email / no name |
| AC-005 | rendered | Host | P | `APerson_IsShownByProfileName_OrEmail_WithTheEmail` | label + email |
| AC-006 | port order kept | Unit | Q | `TheMembers_AreSplitIntoTheThreeSections_InThePortsOrder` | port order per section |
| AC-006 | order in SQL, tie-breaks | Integration | S | `TheMembers_AreInTheUkrainianOrder_NamelessLast`, `EqualNames_AreOrderedByEmail_NoneFirst_ThenById` | spec FR-007 order |
| AC-006 | rendered, one page | Host | P | `EachSection_IsInNameOrder_OnOnePage` | order; no paging |
| AC-007 | card links | Host | P | `TheCard_LinksToTheJournal_TheMeetReports_AndTheCourseList` | journal, Meet reports with courseId; list |
| AC-007 | list name links to card | Host | P | `TheCourseList_LinksEachCourseNameToItsCard` | `<a href="/workspace/courses/{id}">name</a>` |
| AC-007 | address and switcher (FR-010, S-06) | Unit / Host | Q / P | `TheReturnPath_IsTheCardUrl`, `LeadingZeros_OpenTheCourse_AndTheReturnPathIsCanonical`, `TheAddress_CarriesTheIdOnly_AndTheSwitcherReturnsToTheCard` | canonical card URL; no echo; no personal data in URLs |
| AC-008 | malformed → no read | Unit | Q | `AMalformedId_IsCourseMalformed_AndNothingIsRead` (17 cases) | CourseMalformed, 0 calls |
| AC-008 | unknown → Q1 only | Unit | Q | `AnUnknownId_IsCourseUnknown_AndTheMembersAreNotRead`, `TheLargestId_IsWellFormed_AndUnknown` | CourseUnknown, Q2 not run |
| AC-008 | Q1 null | Integration | S | `NoCourse_IsNull` | null |
| AC-008 | 404 page, no echo | Host | P | `AMalformedOrUnknownId_IsTheOrdinaryNotFoundPage` (12 cases) | control card 200; then 404, `Error.NotFound`, value absent |
| AC-008 | not logged | Host | L | `AMalformedOrUnknownId_LogsAWarningNamingTheParameter_ButNotItsValue` (2), `AValueTheRouteDoesNotMatch_IsNeverLogged` | Warning names `courseId` + outcome; value absent |
| AC-009 | empty sections | Unit | Q | `ACourseWithNobody_HasThreeEmptySections` | three empty lists |
| AC-009 | messages rendered | Host | P | `EmptySections_ShowTheirOwnMessages`, `OnlyTheEmptySection_ShowsItsMessage` | own message, no table, count 0 |
| AC-010 | no guard, no Google, no unit of work | Unit | Q | `TheQuery_DependsOnTheReadPortAndTheTimeZoneOnly` | port + time zone only |
| AC-010 | every BR-025 cause | Host | RO | `TheUseCase_ReturnsTheCard_WritesNothing_AndCallsNoGoogle` (4), `ThePage_ShowsTheCard_WritesNothing_AndCallsNoGoogle` (4) | Shown / 200, fingerprint unchanged, 0 Google calls |
| AC-010 | Q1/Q2 write nothing | Integration | S | `Reading_WritesNothing` | fingerprint unchanged |
| AC-011 | allowed roles | Security | A | `AnAdminAndADean_CanOpenTheCourseCard` (2) | 200 with data |
| AC-011 | forbidden: anonymous, restricted Dean | Security | A | `Anonymous_IsSentToSignIn`, `ADeanWithATemporaryPassword_IsSentToTheForcedChange` | 302 sign-in / change-password |
| AC-011 | endpoint protected by `ViewCourses`, digit constraint | Security | A | `ThePath_IsAProtectedEndpoint_WithTheViewCoursesPolicy` | single endpoint, policy, `1,19` |
| AC-012 | keys in both languages | Host | T | `EveryKey_ExistsInBothLanguages_NonEmpty_AndTheTextsDiffer` | 26 keys |
| AC-012 | page in uk and en, data unchanged | Host | T | `TheCard_RendersInTheChosenLanguage_AndShowsGoogleDataAsStored` (uk, en) | labels translated; names as stored |
| AC-013 | no Google call, synthetic data | Host | P / RO | `TheCard_ShowsTheDetails`, RO tests | 0 Google calls; TC-4 fixtures |
| AC-014 | Kyiv time and date | Unit / Host | Q / P | `TheDetails_AreTheStoredValues_WithTheTimesInTheSchoolsZone`, `ALeaversLastSeen_IsTheSchoolsLocalDate`, `TheCard_ShowsTheDetails`, `TheLeavers_ShowRoleAndLastSeenDate_AndAreNotCurrent` | 1 April 01:30; 1 April |
| AC-015 | Ukrainian order, nameless last | Integration / Host | S / P | `TheMembers_AreInTheUkrainianOrder_NamelessLast`, `EachSection_IsInNameOrder_OnOnePage` | Андрієнко, Гнатюк, Ґудзь, Іваненко, Ігнатенко, …, no name |
| AC-016 | description as text with line break | Host | P | `TheDescription_IsText_WithItsLineBreak`, `GoogleData_IsHtmlEncoded` | encoded; `\n` kept; no `<br>` |
| AC-017 | bounded reads | Unit / Integration | Q / S | `ACard_IsTwoReads_WhateverTheRosterSize` (3; 300+50), `EachRead_IsOneDatabaseCommand_WhateverTheRosterSize` | 2 calls; 1 command each |
| AC-008 / FR-013 | shown card log line | Host | L | `AShownCard_LogsTheCounts_AndNoCourseOrPersonData` | counts; no sensitive value |
| — | HTML encoding of Google data (S-08) | Host | P | `GoogleData_IsHtmlEncoded` | no raw markup |
