---
artifact_type: test_generation_report
story: US-056
version: 1
status: DRAFT
created_at: 2026-10-25T20:00:00Z
updated_at: 2026-10-25T20:00:00Z
produced_by: test-writer
inputs:
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
supersedes: null
---

# US-056 Test Generation Report

**Result: PASS.** 29 new tests in 6 classes: 25 red for the missing behaviour, 4 green by
design. Build 0 warnings / 0 errors. The three existing reader classes touched by the rename
stay green (30/30).

## 1. Test files created

| File | Tests |
|---|---|
| `tests/ClassroomAgent.Tests/Application/UseCases/MembershipStartTests.cs` | 7 |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/MembershipStartReadersTests.cs` | 5 |
| `tests/ClassroomAgent.Tests/Web/Pages/MembershipStartJournalTests.cs` | 7 |
| `tests/ClassroomAgent.Tests/Web/Pages/MembershipStartPreUpgradeTests.cs` | 2 |
| `tests/ClassroomAgent.Tests/Web/Pages/MembershipStartReportTests.cs` | 3 |
| `tests/ClassroomAgent.Tests/Web/Pages/MembershipStartMeetTests.cs` | 5 |
| `tests/ClassroomAgent.Tests/TestInfrastructure/MembershipStartHost.cs` | helper |

The unit class sits in `Application/UseCases` next to `CourseInvariantTests` (the precedent for
Domain tests): a `ClassroomAgent.Tests.Domain` namespace hides `ClassroomAgent.Domain` in other
test files (CS0234).

## 2. Test files modified

| File | Change | Trace |
|---|---|---|
| `TestInfrastructure/CourseRows.cs` | `InsertCourseAsync(..., createdAt)` | db-design §1, §5 (seeded `created_at`) |
| `TestInfrastructure/FakeClassroomReader.cs` | `WithCourse(..., creationTime)`, `WithRoster` | FR-001 a/b, AC-007 |
| `TestInfrastructure/SyncHostExtensions.cs` | `StartAsync(..., now)` | AC dates (spec §5 preamble) |
| `Infrastructure/Persistence/JournalSourceTests.cs:138` | `.FirstSeenAt` → `.StartsAt` | entity model §3, db-design §6 |
| `Infrastructure/Persistence/JournalFieldSourceTests.cs:181` | same | same |
| `Infrastructure/Persistence/MeetReportSourceTests.cs:184` | same | same |

## 3. Compile-only skeleton

| Declaration | File |
|---|---|
| `MembershipStart.Of(DateTimeOffset, DateTimeOffset, DateTimeOffset?)` — throws `NotImplementedException` | `src/ClassroomAgent.Domain/Rules/MembershipStart.cs` (new) |
| `JournalMemberRecord.StartsAt` (was `FirstSeenAt`) | `src/ClassroomAgent.Application/Models/JournalMemberRecord.cs` |
| `JournalCourseMemberRecord.StartsAt` | `src/ClassroomAgent.Application/Models/JournalCourseMemberRecord.cs` |
| `MeetReportMemberRecord.StartsAt` | `src/ClassroomAgent.Application/Models/MeetReportMemberRecord.cs` |
| `RosterMembership.StartsAt` | `src/ClassroomAgent.Application/MeetLinking/RosterMembership.cs` |
| `JournalCellRule.IsOfPeriod(startsAt, …)` parameter rename | `src/ClassroomAgent.Application/UseCases/JournalCellRule.cs` |
| callers pass `m.StartsAt` / `membership.StartsAt` | `GetJournalQuery.cs`, `GetMeetReportsQuery.cs`, `ReportBuilder.cs` (2), `MeetReportRules.cs`, `RosterOnDate.cs` |

The renames are signature names from entity model §3–§4, with no behaviour change: the readers
still fill the positional record field with `m.FirstSeenAt`, and nothing calls `MembershipStart`.
IMPLEMENTATION completes the rule and the four readers plus the raw SQL (db-design §3).

## 4. Commands

All runs were made from the repository root by delegated `cheap-worker` agents:
`dotnet build ClassroomAgent.sln`, then one
`dotnet test --filter FullyQualifiedName~<Class>` per class. Docker was running, and no retry
was needed. Free RAM before the runs: 7.4 GB.

## 5. Results

| Class | Passed | Failed | Total |
|---|---|---|---|
| MembershipStartTests | 0 | 7 | 7 |
| MembershipStartReadersTests | 0 | 5 | 5 |
| MembershipStartJournalTests | 2 | 5 | 7 |
| MembershipStartPreUpgradeTests | 1 | 1 | 2 |
| MembershipStartReportTests | 0 | 3 | 3 |
| MembershipStartMeetTests | 1 | 4 | 5 |
| JournalSourceTests (existing) | 12 | 0 | 12 |
| JournalFieldSourceTests (existing) | 6 | 0 | 6 |
| MeetReportSourceTests (existing) | 12 | 0 | 12 |

The full suite was not run at this stage. IMPLEMENTATION runs it.

### 5.1 Expected failures (red phase verified)

- **Unit, 7 tests.** Each throws `NotImplementedException` at `MembershipStart.cs:13`.
- **Readers, 4 tests.** Each fails with `Expected: 2025-09-01T06:00:00+00:00, Actual:
  2026-09-10T06:59:55+00:00`: the record carries the first-seen instant, not the start.
- **Readers, unassigned order.** Throws `NotImplementedException` from the C# rule that computes
  the expected order. After implementation the same test pins the raw SQL to that rule.
- **Journal, 5 tests (AC-001, 005, 007, 008, 011).** Each fails with `Assert.Contains … Not found:
  "Test Person n"`: the first-load member is missing from a period before the first run. In
  AC-005 the October 2025 control passed, so L is named and the missing November row is the
  start.
- **Pre-upgrade ¶1.** "Test Person 2" is not found.
- **Report, 3 tests.** The teacher line, the table export and the blank form do not name T.
  The item-title control passed.
- **Meet, 4 tests.** "не викладач цього курсу" is found; «Немає студентів за цей період.» is
  found; no automatic link was made; the candidate code is not first.

### 5.2 Green by design

| Test | Why it passes today |
|---|---|
| `WithoutACreationTime_AStudentOfTheFirstLoad_CountsFromTheFirstRunsDay` (AC-006) | Rule (b) equals today's first-seen start. |
| `ObservedDatesAndTheLeaverExpiry_AreUnchanged` (AC-010) | The Story changes neither sync nor the purge. The test is a guard. |
| `ALaterArrival_InACourseWithNoStoredFirstLoadMember_DoesNotCountFromTheCreation` (AC-009 ¶2) | M is excluded by first seen today. The test fails an "earliest surviving first-seen" recognition. |
| `AMeetingBeforeALaterArrivalWasSeen_DoesNotCountThemAsACourseStudent` (AC-007 Meet) | S was first seen on 2026-10-22, which is before the 2026-10-30 meeting. The test guards against counting N from creation. |

Each of these has a positive control, so it cannot pass on an empty page.

### 5.3 Unexpected failures

None.

## 6. Untested Acceptance Criteria

None. Every AC-001 … AC-011 is mapped in `docs/tests/US-056-ac-test-matrix.md`.

## 7. Open Decisions

None.

## 8. Notes for IMPLEMENTATION

- Existing fixtures that seed `first_seen_at` ≤ the course's `created_at` together with a
  `creation_time` may change meaning (db-design §6). One example is `MeetLinkingHostTests`
  near line 282 (`firstSeenAt: LaterMeeting`, `creation_time` 10 days earlier). List each
  adjusted test in the implementation report.
- `MembershipStartHost.RunAgainAtAsync` restarts the host on a later day. A scheduled run
  minutes later would land on the same calendar day.

## 9. Delegation

- **Kept inline:** test design, the strategy and matrix, the skeleton, the unit, reader,
  journal and pre-upgrade tests, and the red-phase verdict.
- **Fork (strong model):** the report and Meet host tests (`MembershipStartReportTests`,
  `MembershipStartMeetTests`), written against the scenarios fixed in the brief.
- **`cheap-worker`:** every build and test run.
