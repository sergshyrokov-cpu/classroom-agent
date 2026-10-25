---
artifact_type: implementation_report
story: US-056
version: 1
status: DRAFT
created_at: 2026-10-25T21:30:00Z
updated_at: 2026-10-25T21:30:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-056-membership-starts-at-course-creation.md
    version: null
  - path: docs/decisions/US-056-open-decisions.md
    version: 1
  - path: docs/specifications/US-056-spec.md
    version: 3
  - path: docs/designs/api/US-056-api-design.md
    version: 1
  - path: docs/designs/database/US-056-db-design.md
    version: 1
  - path: docs/designs/database/US-056-entity-model.md
    version: 1
  - path: docs/tests/US-056-test-strategy.md
    version: 1
  - path: docs/tests/US-056-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-056-test-generation-report.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
attempt: 1
---

# US-056 Implementation Report

## 1. Summary

A membership's start now follows trebovaniya.md v98. If the course's first load recorded the
membership, the start is the course's Google `creationTime`, or that run's instant when Google gave
none. Every other membership starts at its first-seen instant. The journal, the template report,
Meet reports, Meet code scoring and the unassigned Meet codes order all use the start.

Implementation follows db-design §1–§3:

- The start is **derived**: `first_seen_at <= course.created_at`.
- The rule lives in **one Domain place**, `MembershipStart.Of`.
- Four readers project the three source values and apply the rule in memory.
- The raw SQL candidate test is the SQL mirror of the same rule.

The Story needed no schema change, migration, endpoint, text, audit or log change.

**Status:**

- All 29 story tests pass.
- The build has 0 warnings.
- `dotnet format --verify-no-changes` is clean.
- The full suite passed 6167 of 6168 tests. The one failure is a timing-flaky test unrelated to
  US-056 (§5, §7 N-2).

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-056-membership-starts-at-course-creation.md` | — |
| Specification | `docs/specifications/US-056-spec.md` | 3 (APPROVED) |
| Open Decisions | `docs/decisions/US-056-open-decisions.md` | 1 (none open) |
| API design | `docs/designs/api/US-056-api-design.md` | 1 (NOT_APPLICABLE) |
| DB design / entity model | `docs/designs/database/US-056-db-design.md`, `US-056-entity-model.md` | 1 / 1 |
| Test strategy / matrix / report | `docs/tests/US-056-test-strategy.md`, `US-056-ac-test-matrix.md`, `docs/evidence/US-056-test-generation-report.md` | 1 / 1 / 1 |

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (`docs/tests/US-056-ac-test-matrix.md`) | Status |
|---|---|---|---|
| AC-001 | `MembershipStart.Of`, `JournalSource.GetStudentMembersAsync` → `GetJournalQuery` | `MembershipStartJournalTests.AStudentOfTheFirstLoad_…`, `MembershipStartTests`, `MembershipStartReadersTests.TheJournalStudentRead_…` | pass |
| AC-002 | `JournalFieldSource.GetMembersAsync` → `ReportBuilder` (teacher line, students) | `MembershipStartReportTests` (3), `…TheJournalFieldMemberRead_…` | pass |
| AC-003 | `MeetReportSource.GetCourseMembersAsync` → `MeetReportRules`, `GetMeetReportsQuery` | `MembershipStartMeetTests` (marks, report 3), `…TheMeetReportMemberRead_…` | pass |
| AC-004 ¶1 | `MeetCodeScoringSource.GetScoringInputsAsync` → `RosterOnDate` | `…ACodeOfMeetingsBeforeTheFirstRun_IsLinkedAutomatically`, `…TheMeetCodeScoringRead_…` | pass |
| AC-004 ¶2 | `MeetCodesReadSource.GetUnassignedPageAsync` (`has_candidate` SQL) | `…AnUnassignedCodeOfTheFirstLoadsTeacher_…`, `…TheUnassignedCodesOrder_AgreesWithTheStartRule` | pass |
| AC-005 | rule (a) on the submissions-only membership (same readers) | `…ASubmitterFoundByTheFirstLoad_…` | pass |
| AC-006 | rule (b) | `…WithoutACreationTime_…`, unit (b) | pass |
| AC-007 | rule (c) | `…ALaterArrival_CountsFromTheDayTheyWereSeen`, `…AMeetingBeforeALaterArrivalWasSeen_…`, unit (c) and boundaries | pass |
| AC-008 | rule (a) with the course's own `created_at` | `…ACourseFirstLoadedAfterTheFirstSynchronization_…` | pass |
| AC-009 | derived rule over pre-existing columns (no backfill) | `MembershipStartPreUpgradeTests` (2) | pass |
| AC-010 | sync and purge untouched | `…ObservedDatesAndTheLeaverExpiry_AreUnchanged` | pass |
| AC-011 | inputs never rewritten (FR-002) | `…SeenInTheFirstLoad_SurvivesTheErasureOfAnotherFirstLoadMember` | pass |

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Domain/Rules/MembershipStart.cs` (new) | the rule | FR-001, entity model §2 |
| `src/ClassroomAgent.Application/Models/JournalMemberRecord.cs`, `JournalCourseMemberRecord.cs`, `MeetReportMemberRecord.cs`, `MeetLinking/RosterMembership.cs` | `FirstSeenAt` → `StartsAt` | entity model §3 |
| `src/ClassroomAgent.Application/UseCases/JournalCellRule.cs` | parameter `startsAt` | entity model §4 |
| `src/ClassroomAgent.Application/UseCases/GetJournalQuery.cs`, `GetMeetReportsQuery.cs`, `ReportBuilder.cs`, `MeetReportRules.cs`, `MeetLinking/RosterOnDate.cs` | pass `StartsAt` | entity model §4, FR-003.1–4 |
| `src/ClassroomAgent.Infrastructure/Persistence/Repositories/JournalSource.cs`, `Repositories/JournalFieldSource.cs`, `MeetReportSource.cs`, `MeetCodeScoringSource.cs` | project `FirstSeenAt`, `Course.CreatedAt`, `Course.CreationTime`; map through `MembershipStart.Of` | db-design §3, FR-003.1–4 |
| `src/ClassroomAgent.Infrastructure/Persistence/MeetCodesReadSource.cs` | `JOIN course`, CASE start in `has_candidate` | db-design §3, FR-003.5 |
| `tests/…` story test classes and `MembershipStartHost.cs` (new) | TEST_WRITING output | `ac_test_matrix` |
| `tests/…/CourseRows.cs`, `FakeClassroomReader.cs`, `SyncHostExtensions.cs`, three `*SourceTests.cs` | TEST_WRITING output | test generation report §2 |
| `tests/…/TestInfrastructure/MeetLinkingTestData.cs` | optional `firstLoadedAt` on `SeedCoursesAsync` / `SeedClearRostersAsync` | db-design §6 (fixture says which case it means) |
| `tests/…/Web/BackgroundServices/MeetLinkingHostTests.cs` | `MeetingsBeforeEveryFirstSeenDate_…` passes `firstLoadedAt: FirstMeeting` | db-design §6, §7 D-1 |

The other changed files are workflow artifacts and state, which the closed list allows. No secret,
database file or IDE config is in the set.

## 5. Validation Evidence

All runs were made from the repository root by delegated `cheap-worker` agents.

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0, no diagnostics |
| `dotnet test --filter FullyQualifiedName~<Class>` × 6 story classes | 29/29 pass |
| same for `MeetLinkingHostTests` / `MeetLinkingLoggingTests` / `FailedRunRecordedTests` | 11/11, 2/2, 8/8 |
| same for `JournalSourceTests` / `JournalFieldSourceTests` / `MeetReportSourceTests` | 12/12, 6/6, 12/12 |
| `dotnet test ClassroomAgent.sln` (final, free RAM 4.2 GB) | 6167 passed, 1 failed, 0 skipped of 6168; 8 min 06 s |
| `dotnet test --filter FullyQualifiedName~PushCheckCoordinationTests` | 9/9 pass |

**Earlier full runs, in order:**

1. 161 failures, all `Npgsql … The operation has timed out` in
   `PostgreSqlFixture.CreateDatabaseAsync`. This was the known Docker/WSL hang: the container
   was ready, then silent for 20 minutes.
2. The retry had 1 failure, `MeetLinkingHostTests.MeetingsBeforeEveryFirstSeenDate_…`. It is fixed
   in §7 D-1. `PushCheckCoordinationTests` passed in that run.

Also fixed along the way: `dotnet format` flagged whitespace because the scripted edits had
rewritten CRLF files as LF. The line endings were restored, and the index is LF either way.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

- **D-1 Existing fixture changed meaning (expected, db-design §6).**
  `MeetLinkingHostTests.MeetingsBeforeEveryFirstSeenDate_FindNobodyOnTheRoster_AndLinkNothing`
  seeded the courses at the host clock and memberships one day earlier. Under FR-001 those are
  first-load memberships, starting at `creation_time` 10 days earlier, so the meeting found the
  roster and was linked (expected 0, actual 1).
  - **Intent (US-032 I-1):** "meetings older than the start of every membership".
  - **Fix:** the fixture now seeds the courses' first load before the memberships
    (`firstLoadedAt: FirstMeeting`), making them later arrivals. The assertions are unchanged.
  - **No other existing test needed a change.**
- **N-1 Full-suite count.** The suite grew from 6139 to 6168 tests (29 new).
- **N-2 Flaky unrelated test.**
  `PushCheckCoordinationTests.ManyPushesWithinAMinute_LeaveOnlyOnePendingCheck` failed once in the
  final full run with "Timed out waiting for 3 Control Plane call(s); 2 made"
  (`FakeControlPlaneClient.cs:113`).
  - It passed in the previous full run and 9/9 when its class ran alone.
  - It tests legitimacy push coordination, which US-056 does not touch (no file in §4).
  - It is recorded here, not fixed, because a fix would be out of scope.
- **N-3 Raw SQL agreement.** The `has_candidate` SQL is pinned to `MembershipStart.Of` plus
  `RosterOnDate` by `TheUnassignedCodesOrder_AgreesWithTheStartRule`, which covers cases a, b, c and
  a non-member. The `<=` boundary at exactly `created_at` is pinned only in C# (unit test).

## 8. Open Decisions

None touched or required.
