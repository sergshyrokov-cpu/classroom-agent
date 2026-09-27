---
artifact_type: ac_test_matrix
story: US-014
version: 1
status: DRAFT
created_at: 2026-09-27T19:14:37Z
updated_at: 2026-09-27T19:14:37Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-014-sync-courses-and-rosters.md
    version: null
  - path: docs/specifications/US-014-spec.md
    version: 2
  - path: docs/designs/api/US-014-api-design.md
    version: 2
  - path: docs/designs/database/US-014-db-design.md
    version: 1
  - path: docs/designs/database/US-014-entity-model.md
    version: 2
  - path: docs/decisions/US-014-open-decisions.md
    version: 2
supersedes: null
---

# US-014 Acceptance Criteria → Test Matrix

This workflow variant has no separate verification stage: **these tests are the
verification**. A green suite must be sufficient evidence that the nine Acceptance
Criteria hold.

Levels: **U** unit / Application-layer (ports substituted, TC-1, TC-4), **I**
integration against real PostgreSQL via Testcontainers (TC-2), **A** adapter with a
scripted HTTP transport (TC-4), **S** security-relevant.

## AC-001 A run imports the school's courses

| Scenario | Level | Test class | Test method | Expected |
|---|---|---|---|---|
| Every course Classroom returns is imported | U | `CourseImportTests` | `ARun_ImportsEveryCourseClassroomReturns` | both courses stored, state as reported |
| Optional fields arrive as Google gave them, in UTC | U | `CourseImportTests` | `ACourse_KeepsTheFieldsClassroomGave` | owner, both instants, section preserved |
| A course with no owner and no instants is imported | U | `CourseImportTests` | `ACourseWithNoOwnerAndNoInstants_IsStillImported` | stored with nulls, not refused |
| An empty course list is a success, not an error | U | `CourseImportTests` | `AnEmptyCourseList_IsASuccessfulRunWithAZeroCounter` | completed, counter 0 (OD-003) |
| An unrecognised state skips that course only | U | `CourseImportTests` | `ACourseWithAnUnrecognisedState_IsSkippedAndTheRunCompletes` | other two stored, run completed (OD-010) |
| The course list is paged to the end | A | `GoogleClassroomReaderTests` | `TheCourseList_IsPagedToTheEnd` | two requests, second carries the token (VR-005) |
| The state reaches the caller as Google's string | A | `GoogleClassroomReaderTests` | `ACourseState_IsHandedOnAsTheStringGoogleSent` | unparsed, so the use case can apply OD-010 |
| The table exists with the designed columns | I | `CourseSchemaTests` | `TheMigration_CreatesTheTable` | 17 columns, bounds and nullability |
| Each of the five states is storable | I | `CourseSchemaTests` | `EveryStateOfTheVocabulary_IsAccepted` | five rows accepted |
| Nullable columns really are nullable | I | `CourseSchemaTests` | `ACourseWithNoOwnerAndNoGoogleInstants_IsStorable` | stored (db-design §3.3) |

## AC-002 A second run changes nothing it should not

| Scenario | Level | Test class | Test method | Expected |
|---|---|---|---|---|
| A second run adds no row and keeps the identity | U | `CourseImportTests` | `ASecondRun_AddsNoCourseAndKeepsTheIdentity` | one insert, same surrogate id |
| A renamed course is updated in place | U | `CourseImportTests` | `ARenamedCourse_IsUpdatedInPlace` | name and state replaced |
| A duplicate Google course id is rejected by the database | I | `CourseSchemaTests` | `ADuplicateGoogleId_IsRejected` | `23505` on `uq_course_google_id` |
| A duplicate Google user id is rejected | I | `ClassroomParticipantSchemaTests` | `ADuplicateGoogleUserId_IsRejected` | `23505` on the unique index |

## AC-003 Each course's roster is imported with the role on the membership

| Scenario | Level | Test class | Test method | Expected |
|---|---|---|---|---|
| Both rosters imported, role on the membership | U | `RosterImportTests` | `BothRosters_AreImportedWithTheRoleOnTheMembership` | 3 people, 1 teacher, 2 students |
| Co-teachers are imported, not only the owner | U | `RosterImportTests` | `CoTeachers_AreImported` | two teacher memberships |
| One person on two courses: one person, two memberships | U | `RosterImportTests` | `OnePersonOnTwoCourses_IsOneParticipantAndTwoMemberships` | BR-050 |
| Both rosters are read and paged | A | `GoogleClassroomReaderTests` | `ARoster_ReadsBothListsPagedToTheEnd` | 2 teachers over two pages, 1 student |
| A person on both rosters of one course → one teacher row | U | `RosterImportTests` | `APersonOnBothRostersOfOneCourse_IsOneTeacherMembership` | spec v2 I-9 tie-break |
| A role change keeps one membership | U | `RosterImportTests` | `ARoleChange_KeepsOneMembershipAndItsFirstSeenDate` | role replaced, first-seen kept |
| A second membership of the same pair is rejected | I | `CourseMembershipSchemaTests` | `ASecondMembershipOfTheSamePersonInTheSameCourse_IsRejected` | `23505` on `uq_course_membership_course_participant` (PC-8) |
| One person may hold two courses with different roles | I | `CourseMembershipSchemaTests` | `OnePerson_MayTeachOneCourseAndStudyAnother` | both rows stored |
| A role outside the vocabulary is rejected | I | `CourseMembershipSchemaTests` | `ARoleOutsideTheVocabulary_IsRejected` | `23514` on `ck_course_membership_role` |
| The person carries no role column | I | `ClassroomParticipantSchemaTests` | `TheTable_HasNoRoleColumn` | BR-050 enforced by absence |

## AC-004 A membership records what synchronization observed

| Scenario | Level | Test class | Test method | Expected |
|---|---|---|---|---|
| A first sighting sets both instants and the flag | U | `RosterImportTests` | `AFirstSighting_SetsBothInstantsAndTheFlag` | equal instants, on roster |
| One run, one instant everywhere | U | `RosterImportTests` | `EveryObservationOfOneRun_CarriesTheSameInstant` | I-8 |
| Leaving clears the flag and keeps last-seen | U | `RosterImportTests` | `LeavingTheRoster_ClearsTheFlagAndKeepsTheLastSeenDate` | row never deleted (BR-051) |
| Returning reuses the row and keeps first-seen | U | `RosterImportTests` | `ReturningToTheRoster_ReusesTheMembershipAndKeepsTheFirstSeenDate` | FR-009 |
| An empty roster does mark memberships off | U | `RosterImportTests` | `AnEmptyRoster_MarksTheMembershipsOffTheRoster` | I-7 |
| A **failed** roster read marks nothing off | U | `RosterImportTests` | `AFailedRosterRead_MarksNoMembershipOffTheRoster` | I-6 — the distinction from the row above |
| Out-of-order instants are rejected | I | `CourseMembershipSchemaTests` | `ALastSeenEarlierThanFirstSeen_IsRejected` | `23514` on `ck_course_membership_seen_order` |
| Equal instants are accepted | I | `CourseMembershipSchemaTests` | `EqualObservationInstants_AreAccepted` | the legitimate same-run shape |
| The roster-on-a-date index exists and leads with the course | I | `CourseMembershipSchemaTests` | `TheRequiredIndexes_Exist`, `TheRosterOnADateIndex_LeadsWithTheCourse` | PC-7 |

## AC-005 A course Google no longer returns is left alone

| Scenario | Level | Test class | Test method | Expected |
|---|---|---|---|---|
| Such a course is untouched | U | `CourseImportTests` | `ACourseClassroomStoppedReturning_IsLeftUntouched` | name, state and `updated_at` unchanged (FR-011, I-4) |
| Deleting a course with memberships is refused | I | `CourseMembershipSchemaTests` | `DeletingACourseThatHasMemberships_IsRefused` | `23503`, so only the purge deletes, in order |
| Deleting a participant with memberships is refused | I | `CourseMembershipSchemaTests` | `DeletingAParticipantThatHasMemberships_IsRefused` | `23503` (§5.4) |

## AC-006 Read-only mode never reaches the new port

| Scenario | Level | Test class | Test method | Expected |
|---|---|---|---|---|
| No Classroom call in read-only mode | U, S | `CourseImportRefusalTests` | `InReadOnlyMode_TheClassroomPort_IsNeverCalled` | 0 reads, **plus the allowed control** |
| Nothing is written in read-only mode | U, S | `CourseImportRefusalTests` | `InReadOnlyMode_NothingIsWritten` | no row, no commit, no transaction |
| The guard runs before the port and the repositories | U, S | `CourseImportRefusalTests` | `TheGuard_IsConsultedBeforeTheClassroomPort` | order, plus the impersonation control |
| Leaving read-only mode resumes importing | U | `CourseImportRefusalTests` | `LeavingReadOnlyMode_LetsTheNextRunImport` | refusal is a state, not a latch |
| No connection → no call, no write | U, S | `CourseImportRefusalTests` | `WithNoSavedConnection_TheClassroomPortIsNeverCalledAndNothingIsWritten` | US-013 AC-005 shape |
| The run stays a protected write path | U, S | `CourseImportRefusalTests` | `TheRun_StaysAProtectedWritePath` | guard first **and** import happened |

Every one of these six pairs the refusal with the allowed case, because "the port was
not called" is also true of a pipeline with no step — see the test-generation report.

## AC-007 A failure part-way leaves a consistent database

| Scenario | Level | Test class | Test method | Expected |
|---|---|---|---|---|
| Committed courses survive a later failure | U | `CourseImportTests` | `ARunThatFailsPartWay_KeepsTheCoursesAlreadyCommitted` | 2 courses, 2 transactions, run failed (OD-009) |
| The stored diagnosis carries no payload | U, S | `CourseImportTests` | `AFailedImport_StoresADiagnosisWithNoPayload` | no message text, category present (SC-10) |
| A skipped course is not counted | U | `CourseImportTests` | `ASkippedCourse_IsNotCounted` | I-5 |

## AC-008 No personal data in a log; nothing leaves the installation; no audit row

| Scenario | Level | Test class | Test method | Expected |
|---|---|---|---|---|
| Every adapter request goes to Google only | A, S | `GoogleClassroomReaderTests` | `EveryRequest_GoesToGoogle` | SC-13 |
| The token impersonates the technical account | A, S | `GoogleClassroomReaderTests` | `TheTokenRequest_ImpersonatesTheTechnicalAccount` | BR-015, never a person |
| A profile with no address is carried through, not faked | A | `GoogleClassroomReaderTests` | `AProfileWithNoAddress_IsCarriedThroughWithTheAddressAbsent` | OD-006; no "ID: x" placeholder |
| The stored failure text holds no personal data | U, S | `CourseImportTests` | `AFailedImport_StoresADiagnosisWithNoPayload` | SC-10 |
| No audit row and no audit member is added | I, S | `AppUserMigrationTests` (existing), `AccessCheckAuditSchemaTests` (existing) | table set and audit assertions unchanged | FR-019 |

## AC-009 Tests never reach Google; the schema is tested for real

| Scenario | Level | Test class | Test method | Expected |
|---|---|---|---|---|
| Every Application test substitutes the port | U | `SyncWorld.ClassroomReader` (fixture) | used by all `*ImportTests` | TC-4; fixtures synthetic |
| Every adapter test runs offline | A | `GoogleClassroomReaderTests` | scripted transport throughout | TC-4 |
| Schema tested on real PostgreSQL | I | the three `*SchemaTests` | Testcontainers, no InMemory | TC-2 |
| Paging covered by a multi-page fixture | A | `GoogleClassroomReaderTests` | `TheCourseList_IsPagedToTheEnd`, `ARoster_ReadsBothListsPagedToTheEnd` | a single-page implementation fails |
| Two people may share an address | I | `ClassroomParticipantSchemaTests` | `TwoParticipantsSharingAnEmailAddress_AreBothStored` | OD-011's positive assertion |
| The address is indexed and not unique | I | `ClassroomParticipantSchemaTests` | `TheAddress_IsIndexedButNotUnique` | fails if `IsUnique()` is added |
| Migration and table counts grew as designed | I | `AppUserMigrationTests` (existing, modified) | `TheMigrations_CreateLegitimacyStateThenAppUserAndAuditEvent` | six → seven migrations, three tables added |

## Coverage summary

All nine Acceptance Criteria have at least one mapped scenario, and every one has at
least one scenario that fails before implementation. No Acceptance Criterion is left
to a manual check.

Deliberately **not** covered here, with the reason:

- **the age rule of PC-11 / §5 v36** — deferred by OD-001, so there is nothing to
  test; the nullability test of `CourseSchemaTests` records the constraint the future
  rule inherits (db-design §3.5);
- **retry, backoff and permission-failure classification** — US-017 (OD-008). This
  Story's failure test asserts only that the run is recorded as failed;
- **incremental behaviour** — US-018 (OD-007);
- **the purge's own queries** — US-037; the `Restrict` tests record the order it must
  delete in;
- **the off-roster membership of a student with submissions but no sighting** —
  needs submissions, so US-015 (BR-051, §10).