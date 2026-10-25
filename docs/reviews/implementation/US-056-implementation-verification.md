---
artifact_type: implementation_verification
story: US-056
version: 1
status: APPROVED
created_at: 2026-10-25T22:15:00Z
updated_at: 2026-10-25T22:15:00Z
produced_by: implementation-verifier
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
  - path: docs/evidence/US-056-implementation-report.md
    version: 1
supersedes: null
verdict: PASS
---

# US-056 Implementation Verification

## 1. Summary

**PASS.** 0 Critical, 0 Major, 3 Minor.

The code does what spec v3 and db-design v1 say:

- The start of a membership is derived by one Domain rule, `MembershipStart.Of`.
- All four EF readers that feed a member-in-period or member-on-date decision apply it.
- The raw SQL `has_candidate` test of the unassigned Meet codes list mirrors it.

A ripgrep of `src` finds no other reader of `FirstSeenAt` / `first_seen_at` outside the entity, its
EF configuration and these five places. Each AC has a test that would fail without the behaviour. The
one disclosed deviation (D-1) is sound. The 29 story tests and `MeetLinkingHostTests` pass.

The review ran in a fresh context, from files and the working tree only.

## 2. Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-056-membership-starts-at-course-creation.md` | — |
| Specification | `docs/specifications/US-056-spec.md` | 3 (APPROVED) |
| Open Decisions | `docs/decisions/US-056-open-decisions.md` | 1 |
| API design | `docs/designs/api/US-056-api-design.md` | 1 (NOT_APPLICABLE) |
| DB design / entity model | `docs/designs/database/US-056-db-design.md`, `US-056-entity-model.md` | 1 / 1 |
| Test strategy / AC matrix | `docs/tests/US-056-test-strategy.md`, `US-056-ac-test-matrix.md` | 1 / 1 |
| Test generation report | `docs/evidence/US-056-test-generation-report.md` | 1 |
| Implementation report | `docs/evidence/US-056-implementation-report.md` | 1 |

The implementation report names the current version of every input, so none is stale.

## 3. AC Verification

| AC | Tests read | Proves the AC? |
|---|---|---|
| AC-001 | `MembershipStartJournalTests.AStudentOfTheFirstLoad_…`, `MembershipStartTests.AFirstLoadMembership_StartsAtTheCourseCreationTime`, `MembershipStartReadersTests.TheJournalStudentRead_CarriesTheStart` | Yes. S has no submission and is listed for 2025 only through the start. Before the change their first-seen date of 2026-10-22 excluded them. |
| AC-002 | `MembershipStartReportTests` (3), `…TheJournalFieldMemberRead_CarriesTheStart` | Yes. The reader asserts the teacher's start is `creationTime`. The host tests cover screen, table export and blank form. |
| AC-003 | `MembershipStartMeetTests` (marks, report 3), `…TheMeetReportMemberRead_CarriesTheStart` | Yes. |
| AC-004 ¶1 | `…ACodeOfMeetingsBeforeTheFirstRun_IsLinkedAutomatically`, `…TheMeetCodeScoringRead_CarriesTheStart` | Yes. The scoring read asserts the start for each of rules a, b and c. |
| AC-004 ¶2 | `…AnUnassignedCodeOfTheFirstLoadsTeacher_…`, `…TheUnassignedCodesOrder_AgreesWithTheStartRule` | Yes. The expected order is computed from `MembershipStart` + `RosterOnDate`, and pinned to a literal so it cannot be vacuous. Code 1 (a 2025 meeting by a first-load teacher) has no candidate under the old rule. |
| AC-005 | `…ASubmitterFoundByTheFirstLoad_…` | Yes. October is the control: L is named there through a submission. November has no submission by L, so only the start lists them. |
| AC-006 | `…WithoutACreationTime_…` (green by design), `MembershipStartTests` rule b | Yes. The host test is green by design (rule b equals the old first-seen value). The unit test pins rule b itself. |
| AC-007 | `…ALaterArrival_CountsFromTheDayTheyWereSeen`, `…AMeetingBeforeALaterArrivalWasSeen_…` (green by design), unit rule c + boundaries | Yes. The journal test lists S in 2025, which is red before the change, so the test is not vacuous. |
| AC-008 | `…ACourseFirstLoadedAfterTheFirstSynchronization_…` | Yes. The course is first loaded by a later run, which restarts the host on that day. |
| AC-009 | `MembershipStartPreUpgradeTests` (2) | Yes. Rows are seeded directly, as before the upgrade. Paragraph 2 is green by design and has a submitter control. |
| AC-010 | `…ObservedDatesAndTheLeaverExpiry_AreUnchanged` (green by design) | Yes, as a guard. It asserts the stored tuples and that the purge keeps the leaver, so a course created in 2020 does not drive the expiry. |
| AC-011 | `…SeenInTheFirstLoad_SurvivesTheErasureOfAnotherFirstLoadMember` | Yes. S2 is listed for 2025 after S1's erasure and another run. |

The four green-by-design tests match matrix §6 and the test strategy. Each one is paired with a red
test or a unit test of the same rule.

## 4. Specification Coverage

| Requirement | Where | Verdict |
|---|---|---|
| FR-001 a/b/c | `Domain/Rules/MembershipStart.cs`: `firstSeenAt <= courseFirstLoadedAt ? creationTime ?? firstSeenAt : firstSeenAt` | Met. Rule b gives first-seen, which equals the first-loading run's instant (spec FR-001, db-design §1). |
| FR-002 | Derived from `first_seen_at` and `course.created_at`, which are never rewritten (PC-6). No write path changed. | Met. |
| FR-003.1 | `JournalSource.GetStudentMembersAsync`, `JournalFieldSource.GetMembersAsync` → `GetJournalQuery` | Met. |
| FR-003.2 | `JournalFieldSource.GetMembersAsync` → `ReportBuilder` (teachers and students) | Met. |
| FR-003.3 | `MeetReportSource.GetCourseMembersAsync` → `MeetReportRules.OnRoster`, `GetMeetReportsQuery` | Met. |
| FR-003.4 | `MeetCodeScoringSource.GetScoringInputsAsync` → `RosterOnDate.Covers` | Met. |
| FR-003.5 | `MeetCodesReadSource.GetUnassignedPageAsync`, CASE expression in `has_candidate` | Met. The SQL `<=` and `COALESCE` match the C# rule. The date comparison is unchanged. |
| FR-004 | Sync, purge and course card untouched (no file in the diff) | Met. |
| FR-005 | The derived rule over existing columns; no backfill needed | Met. |
| FR-006 | No endpoint, text, resx, audit, log or Google change in the diff | Met. |
| VR-001, VR-002 | No new validation; status not rewritten | Met. |

The diff has no behaviour that no artifact asks for.

## 5. Design Conformance

- **db-design §1–§3:** derived start, one Domain rule, three values projected and the rule applied in
  memory, plus the SQL mirror. All conform. No schema change and no migration, as designed.
- **Entity model §3–§4:** `FirstSeenAt` is renamed to `StartsAt` in `JournalMemberRecord`,
  `JournalCourseMemberRecord`, `MeetReportMemberRecord` and `RosterMembership`, and the
  `JournalCellRule.IsOfPeriod` parameter is renamed. Conforms.
- **AD-3:** `Domain` gains a dependency-free static rule. `Infrastructure` references `Domain.Rules`.
  `Application` has no new reference.
- **AD-4, AD-6, AD-8:** not affected. No port, read-only path or DTO boundary changed.
- **OpenAPI:** not applicable (API design NOT_APPLICABLE).

## 6. Deviations

| Id | Departs from | Reason | Verdict |
|---|---|---|---|
| D-1 | Existing test `MeetLinkingHostTests.MeetingsBeforeEveryFirstSeenDate_…` (US-032 I-1 fixture) | db-design §6 predicted it. The fixture seeded memberships before `created_at`, which now makes them first-load members. `firstLoadedAt: FirstMeeting` makes them later arrivals again, which is the test's intent. Assertions unchanged. | **Accepted.** Disclosed, matches db-design §6, intent preserved. |

No undisclosed deviation was found. The three `*SourceTests` edits only rename `FirstSeenAt` to
`StartsAt`. Their fixtures have no `creation_time`, or memberships after `created_at`, so the expected
values stay right.

## 7. Report Accuracy

- The change set in report §4 equals `git status`: 15 modified `src` files plus the new
  `MembershipStart.cs`, 8 modified test files and 7 new test files. Every file is traced.
- The AC-to-test locations in report §3 exist with the names given.
- Clean change:
  - No `TODO`, `TBD`, `FIXME` or `NotImplementedException` in the new or changed files.
  - No skipped or commented-out test.
  - The skeleton `MembershipStart.Of` is fully implemented.
- Workflow artifacts and state files are on the closed list.

## 8. Story Test Run

Delegated to `cheap-worker`, which touched no files and was asked to report facts verbatim:

- Docker: `docker info` exit code 0.
- `dotnet build ClassroomAgent.sln`: exit 0, 0 warnings, 0 errors.

| Filter (`FullyQualifiedName~`) | Exit | Total | Passed | Failed | Skipped |
|---|---|---|---|---|---|
| `MembershipStartTests` | 0 | 7 | 7 | 0 | 0 |
| `MembershipStartReadersTests` | 0 | 5 | 5 | 0 | 0 |
| `MembershipStartJournalTests` | 0 | 7 | 7 | 0 | 0 |
| `MembershipStartMeetTests` | 0 | 5 | 5 | 0 | 0 |
| `MembershipStartPreUpgradeTests` | 0 | 2 | 2 | 0 | 0 |
| `MembershipStartReportTests` | 0 | 3 | 3 | 0 | 0 |
| `MeetLinkingHostTests` (D-1) | 0 | 11 | 11 | 0 | 0 |

That is 29/29 story tests, matching the 29 in the test generation report. Two classes ran concurrently,
and both passed. The full suite was not rerun here; `security-reviewer` runs it.

## 9. Findings

- **N-1 (Minor) — SQL boundary pinned only in C#.**
  - Where: `MeetCodesReadSource.GetUnassignedPageAsync`.
  - What: the `<=` at exactly `course.created_at` is tested only in `MembershipStartTests`. No SQL row
    sits on the boundary. The report discloses this as N-3.
  - Risk: low. A real run's memberships are seconds before `created_at`, never equal to it.
  - Loop-back if acted on: `test_gap`. Advisory.
- **N-2 (Minor) — full suite not green in one run.**
  - The final full run was 6167/6168. The failure was in `PushCheckCoordinationTests`, which no US-056
    file touches, and the class passed 9/9 alone.
  - `security-reviewer` reruns the full suite.
- **N-3 (Minor) — line endings.**
  - `git` warns that `MeetLinkingTestData.cs` and `MeetLinkingHostTests.cs` are LF in the working copy
    and will be converted to CRLF. The index is normalised, so the commit is unaffected, and
    `dotnet format` passed per the report.
  - Cosmetic only.

## 10. Verdict

**PASS.** Next stage: SECURITY_REVIEW.
