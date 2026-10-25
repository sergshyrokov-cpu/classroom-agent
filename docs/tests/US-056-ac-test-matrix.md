---
artifact_type: ac_test_matrix
story: US-056
version: 1
status: DRAFT
created_at: 2026-10-25T19:45:00Z
updated_at: 2026-10-25T19:45:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-056-spec.md
    version: 3
  - path: docs/designs/database/US-056-db-design.md
    version: 1
  - path: docs/designs/database/US-056-entity-model.md
    version: 1
  - path: docs/tests/US-056-test-strategy.md
    version: 1
supersedes: null
---

# US-056 Acceptance Criteria → Test Matrix

Status before implementation: **red** = fails for the missing behaviour; **green (design)**
= passes today because the current start already gives the expected result (strategy §6).

Class prefixes: `UC` = `Application/UseCases`, `IP` = `Infrastructure/Persistence`,
`WP` = `Web/Pages`.

| AC | Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | First-load roster student, no submissions, journal 01.09–31.12.2025 | Host | WP `MembershipStartJournalTests` | `AStudentOfTheFirstLoad_IsListedForAPeriodBeforeTheFirstSynchronization` | S listed, T not a row | red |
| AC-001 | Rule (a) | Unit | UC `MembershipStartTests` | `AFirstLoadMembership_StartsAtTheCourseCreationTime` | creationTime | red |
| AC-001, AC-005 | Journal student read carries the start | Integration | IP `MembershipStartReadersTests` | `TheJournalStudentRead_CarriesTheStart` | StartsAt = creationTime / first seen | red |
| AC-002 | Teacher line on screen | Host | WP `MembershipStartReportTests` | `TheReportBeforeTheFirstRun_NamesTheFirstLoadsTeacher_OnScreen` | T named | red |
| AC-002 | Teacher line, table export | Host | WP `MembershipStartReportTests` | `…_InTheTableExport` | T named | red |
| AC-002 | Teachers cell, shipped blank form | Host | WP `MembershipStartReportTests` | `…_InTheBlankForm` | T named | red |
| AC-002 | Journal field / report member read | Integration | IP `MembershipStartReadersTests` | `TheJournalFieldMemberRead_CarriesTheStart` | StartsAt, teachers included | red |
| AC-003 | Meeting 2025-10-15: organizer + course teacher, course student | Host | WP `MembershipStartMeetTests` | `AMeetingBeforeTheFirstRun_MarksTheFirstLoadsTeacherAndStudent` | T organizer/teacher, S student | red |
| AC-003 | Report 3 lists S | Host | WP `MembershipStartMeetTests` | `TheReportBeforeTheFirstRun_ListsTheFirstLoadsStudent` | S listed | red |
| AC-003 | Meet report member read | Integration | IP `MembershipStartReadersTests` | `TheMeetReportMemberRead_CarriesTheStart` | StartsAt | red |
| AC-004 ¶1 | Code of pre-run meetings linked automatically | Host | WP `MembershipStartMeetTests` | `ACodeOfMeetingsBeforeTheFirstRun_IsLinkedAutomatically` | one automatic link to the course | red |
| AC-004 ¶1 | Scoring read | Integration | IP `MembershipStartReadersTests` | `TheMeetCodeScoringRead_CarriesTheStart` | StartsAt per email | red |
| AC-004 ¶2 | Unassigned list: T's code before a no-candidate code | Host | WP `MembershipStartMeetTests` | `AnUnassignedCodeOfTheFirstLoadsTeacher_ComesBeforeACodeWithNoCandidate` | candidate group first | red |
| AC-004 ¶2 | Raw-SQL order agrees with `MembershipStart` + `RosterOnDate` | Integration | IP `MembershipStartReadersTests` | `TheUnassignedCodesOrder_AgreesWithTheStartRule` | same order as the C# rule | red |
| AC-005 | Submissions-only first-load member, Nov 2025 | Host | WP `MembershipStartJournalTests` | `ASubmitterFoundByTheFirstLoad_IsListedForAPeriodWithoutTheirSubmission` | L listed (control: Oct 2025); off roster, last seen 2026-10-22 | red |
| AC-006 | No creationTime: not in 2025, in Oct 2026 | Host | WP `MembershipStartJournalTests` | `WithoutACreationTime_AStudentOfTheFirstLoad_CountsFromTheFirstRunsDay` | as stated | green (design) |
| AC-006 | Rule (b) | Unit | UC `MembershipStartTests` | `AFirstLoadMembership_WithoutCreationTime_StartsAtTheFirstLoadingRun` | first-loading run's instant | red |
| AC-007 | Later arrival: journal 2025 and Oct 2026 | Host | WP `MembershipStartJournalTests` | `ALaterArrival_CountsFromTheDayTheyWereSeen` | S listed both, N neither; N first seen 2026-11-05 | red |
| AC-007 | Later arrival: Meet meeting 2026-10-30 | Host | WP `MembershipStartMeetTests` | `AMeetingBeforeALaterArrivalWasSeen_DoesNotCountThemAsACourseStudent` | N not on course list, S course student | green (design) |
| AC-007 | Rule (c), boundaries | Unit | UC `MembershipStartTests` | `ALaterArrival_StartsAtItsFirstSeenInstant`, `ALaterArrival_WithoutCreationTime_…`, `FirstSeenAtTheCourseSaveInstant_CountsAsTheFirstLoad`, `FirstSeenOneTickAfterTheCourseSaveInstant_IsALaterArrival`, `TheComparison_IsByInstant_NotByOffset` | as named | red |
| AC-008 | Course first loaded by the 2026-11-05 run, journal 20.10–31.10.2026 | Host | WP `MembershipStartJournalTests` | `ACourseFirstLoadedAfterTheFirstSynchronization_CountsItsStudentsFromItsCreation` | S2 listed | red |
| AC-009 ¶1 | Pre-upgrade rows: first-load members listed, later arrival not | Host (seeded) | WP `MembershipStartPreUpgradeTests` | `StoredFirstLoadMembers_CountFromTheCourseCreation_AndALaterArrivalDoesNot` | as stated | red |
| AC-009 ¶2 | Pre-upgrade course without stored first-load member: M not listed | Host (seeded) | WP `MembershipStartPreUpgradeTests` | `ALaterArrival_InACourseWithNoStoredFirstLoadMember_DoesNotCountFromTheCreation` | M absent, submitter control listed | green (design) |
| AC-010 | Observed dates after another run; leaver expiry from last seen | Host | WP `MembershipStartJournalTests` | `ObservedDatesAndTheLeaverExpiry_AreUnchanged` | dates as observed; leaver kept by purge | green (design) |
| AC-011 | Erasure of S1 + another run: S2 from creation, N from 2026-11-05 | Host | WP `MembershipStartJournalTests` | `SeenInTheFirstLoad_SurvivesTheErasureOfAnotherFirstLoadMember` | S2 listed 2025 and Oct 2026; S1, N not | red |

Every Acceptance Criterion AC-001 … AC-011 has at least one mapped test. No endpoint is
added or changed (API design NOT_APPLICABLE), so no contract test applies (TC-3).
