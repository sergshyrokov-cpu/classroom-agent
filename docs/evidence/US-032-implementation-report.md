---
artifact_type: implementation_report
story: US-032
version: 1
status: DRAFT
created_at: 2026-10-10T18:42:20Z
updated_at: 2026-10-10T18:42:20Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-032-meet-code-linking.md
    version: null
  - path: docs/specifications/US-032-spec.md
    version: 1
  - path: docs/decisions/US-032-open-decisions.md
    version: 2
  - path: docs/designs/api/US-032-openapi.yaml
    version: 1
  - path: docs/designs/api/US-032-api-design.md
    version: 1
  - path: docs/designs/database/US-032-db-design.md
    version: 1
  - path: docs/designs/database/US-032-entity-model.md
    version: 1
  - path: docs/tests/US-032-test-strategy.md
    version: 1
  - path: docs/tests/US-032-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-032-test-generation-report.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-032 Implementation Report — Link meeting codes to courses

## 1. Summary

Implemented: the `MeetingCodeLink` entity and table (link to a course or "not a
course" mark), the scoring and decision rules, the automatic linking step after
the Meet step of every run, the thresholds configuration, the "Meet meetings"
page with its three lists and the course-choice form, the three writes (set the
course, confirm, mark "not a course") with the expected-state check, read-only
refusal and audit, and the purge extension (last activity, course deletion,
leaver expiry, orphaned marks, link count). Validation: build, full suite and
format check — §5.

Security-sensitive: it shows personal data (organizer and participant emails) to
Dean and Admin, adds two policies, three state-changing endpoints and audit rows.

## 2. Source Artifacts

As the front matter `inputs`. Story `docs/stories/US-032-meet-code-linking.md`;
Specification v1 (APPROVED); Open Decisions v2 (OD-001 … OD-009 resolved);
OpenAPI v1 and API design v1; DB design v1 and entity model v1; test strategy v1,
AC-test matrix v1, test-generation report v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (ac_test_matrix) | Status |
|---|---|---|---|
| AC-001 | `LinkMeetCodesStep.ExecuteAsync`, `MeetCodeScorer`, `MeetingCodeLink.LinkAutomatically`, `AuditEvent.MeetCodeAutoLinked`; step 3 of `RunSynchronizationUseCase` | `MeetLinkingHostTests.AnUnambiguousCode_IsLinkedAutomatically_WithOneAuditRow`, `MeetCodeScorerTests`, `MeetCodeAuditEventTests` | green |
| AC-002 | `RosterOnDate.Covers`, `MeetCodeScorer.Score`; SQL candidate test in `MeetCodesReadSource.GetUnassignedPageAsync` | `RosterOnDateTests`, `MeetCodeScorerTests`, `MeetCodesPageTests.AtTheKyivDayBoundary_TheTeacherIsStillACandidate` | green |
| AC-003 | `MeetCodeScorer.Decide` (exact fractions) | `MeetCodeScorerTests`, `MeetLinkingHostTests.AnAmbiguousCode_StaysUnassigned_AndTheRunCompletes`, `MeetCodesPageTests.Unassigned_…` | green |
| AC-004 | re-scoring every run (`IMeetCodeScoringSource.GetUnassignedCodesAsync`) | `MeetLinkingHostTests.AnAmbiguousCode_IsLinkedByALaterRun_…` | green |
| AC-005 | only codes without a row are read | `MeetLinkingHostTests.AnAutomaticLink_StaysWithA_…`, `APersonsLink_IsNeverRevised_…` | green |
| AC-006 | `InstallationSettingsReader` (keys, 1 … 100, defaults), `MeetLinkingThresholds` singleton | `MeetLinkingConfigurationTests`, `MeetLinkingHostTests.ConfiguredThresholds_…` | green |
| AC-007 | step reached only after the Meet step; `SyncStep.Linking` on its own failure | `MeetLinkingHostTests.AFailedMeetStep_…`, `AFailureOfTheLinkingStep_…`; `MeetCodesPageTests.AFailedLinkingStep_…` | green |
| AC-008 | `SetMeetCodeCourseUseCase` (unassigned), `GetMeetCodeCourseChoiceQuery`, `MeetCodesController` | `MeetCodeChangeTests`, `MeetCodeActionsHttpTests`, `MeetCodesPageTests.TheChoiceForm_…` | green |
| AC-009 | `ConfirmMeetCodeLinkUseCase`, `MeetingCodeLink.Confirm` | `MeetCodeChangeTests.Confirming…`, `MeetCodeActionsHttpTests.Confirming_…` | green |
| AC-010 | `SetMeetCodeCourseUseCase` (linked), `MeetingCodeLink.Relink` | `MeetCodeChangeTests.Relinking_…`, `MeetCodeActionsHttpTests.Relinking_…` | green |
| AC-011 | `MarkMeetCodeNotACourseUseCase`, `MeetingCodeLink.Mark` / `MarkNotACourse` | `MeetCodeChangeTests.Marking…`, `MeetCodeActionsHttpTests.Marking…`, `MeetLinkingHostTests.AMarkedCode_…` | green |
| AC-012 | `SetMeetCodeCourseUseCase` (marked), `MeetingCodeLink.LinkFromMark` | `MeetCodeChangeTests.PickingACourseForAMarkedCode_…`, `MeetCodeActionsHttpTests.PickingACourse_ForAMarkedCode_…` | green |
| AC-013 | `MeetCodesReadSource` ordering; `MeetCodes.NoCandidates` | `MeetCodesPageTests.ACodeWithNoCandidates_…` | green |
| AC-014 | policies `ViewMeetCodes`, `LinkMeetCodes` (Admin, Dean) on every action | `MeetCodesAuthorizationTests`, `InstallationEndpointTests` | green |
| AC-015 | `MeetCodeWrite.EnsureAllowedAsync` (guard first, refused row) | `MeetCodeChangeTests.InReadOnlyMode_…`, `MeetCodeActionsHttpTests.InReadOnlyMode_…`, `MeetCodesPageTests.InReadOnlyMode_…` | green |
| AC-016 | `RunRetentionPurgeUseCase`, `RetentionPurgeStore` | `RetentionPurgeMeetLinkTests`, `MeetCodeAuditEventTests` | green |
| AC-017 | `SharedResource.uk/en.resx` (`MeetCodes.*`, `LastSync.Step.Linking`) | `MeetCodesTranslationTests` | green |
| AC-018 | no Google port in any new type | all of the above use substituted ports | green |

## 4. Change Set

**Domain**
- `Entities/MeetingCodeLink.cs` (new) — FR-001, entity model §1, db-design §2.
- `Enums/MeetingCodeLinkState.cs` (new) — entity model §1.
- `Entities/AuditEvent.cs` — FR-014: three properties, `MeetCodeAutoLinked`, `MeetCodeChanged`, `MeetCodeChangeRefused`; purge count (FR-016).
- `Enums/AuditAction.cs` — six actions (db-design §3.1). `Enums/SyncStep.cs` — `Linking` (FR-006). `Rules/RetentionPurgeCounts.cs` — `MeetCodeLinks` (FR-016).

**Application**
- `MeetLinking/RosterOnDate.cs`, `MeetCodeScorer.cs`, `CandidateShare.cs`, `MeetCodeScore.cs`, `MeetCodeDecision.cs`, `MeetCodeMeeting.cs`, `MeetCodeScoringInput.cs`, `RosterMembership.cs` (new) — FR-002 … FR-004.
- `UseCases/LinkMeetCodesStep.cs` (new) — FR-006. `UseCases/RunSynchronizationUseCase.cs` — the third step (FR-006).
- `UseCases/SetMeetCodeCourseUseCase.cs`, `ConfirmMeetCodeLinkUseCase.cs`, `MarkMeetCodeNotACourseUseCase.cs`, `MeetCodeWrite.cs` (new) — FR-008 … FR-015.
- `UseCases/GetMeetCodesQuery.cs`, `GetMeetCodeCourseChoiceQuery.cs` (new) — FR-007, VR-004.
- `UseCases/RunRetentionPurgeUseCase.cs`, `Ports/IRetentionPurgeStore.cs`, `Models/CourseActivityDates.cs`, `Models/RetentionPurgeStep.cs`, `Models/CourseDeletionCounts.cs`, `Models/LeaverDeletionCounts.cs` — FR-016.
- `Ports/IMeetingCodeLinkRepository.cs`, `IMeetCodeScoringSource.cs`, `IMeetCodesReadSource.cs` (new) — entity model §5.
- `Exceptions/MeetingCodeLinkConflictException.cs` (new) — FR-013, db-design §7.
- `Models/` new rows and results (`AccountRef`, `UnassignedCodeRow`, `LinkedCodeRow`, `MarkedCodeRow`, `PagedRows`, `MeetCodeListCounts`, `MeetCodeList`, `MeetCodeChangeOutcome`, `MeetCodeChangeResult`, `MeetCodesPageResult`, `MeetCodeChoiceResult`, `MeetLinkingCounts`, `MeetLinkingThresholds`); `Models/SynchronizationRunOutcome.cs` (`Linking`) — FR-006, FR-007.
- `Models/Dtos/` page models (`MeetCodesPageModel`, `UnassignedCodeItem`, `LinkedCodeItem`, `NotACourseCodeItem`, `AccountLabel`, `CandidateCourse`, `MeetCodeCourseChoicePageModel`, `MeetCodeExpectedState`, `MeetCodeFieldError`, `MeetCodesMessageKey`) — OpenAPI schemas (AD-8).
- `Models/Requests/MeetCodeFormInput.cs`, `MeetCodesRequest.cs` (new) — VR-001 … VR-004.
- `Authorization/InstallationPolicies.cs` — two policies (spec §7). `Localization/SharedResource.uk.resx`, `.en.resx` — FR-017.

**Infrastructure**
- `Persistence/Configurations/MeetingCodeLinkConfiguration.cs` (new), `ClassroomAgentDbContext.cs` — db-design §2.
- `Configurations/AuditEventConfiguration.cs` — db-design §3; `SyncStateConfiguration.cs` — §4; `ClassroomParticipantConfiguration.cs` — comment on the expression index (§5.2).
- `Persistence/Repositories/MeetingCodeLinkRepository.cs`, `Persistence/MeetCodeScoringSource.cs`, `Persistence/MeetCodesReadSource.cs` (new) — entity model §5, db-design §5.
- `Persistence/RetentionPurgeStore.cs` — db-design §6. `Persistence/UnitOfWork.cs` — db-design §7 (conflict translation, detaching failed link entries). `Persistence/TimestampInterceptor.cs` — PC-6 for the new table.
- `Migrations/20261010162545_AddMeetingCodeLinks.cs`, `.Designer.cs`, `ClassroomAgentDbContextModelSnapshot.cs` — db-design §8 (PC-2).

**Web**
- `Controllers/MeetCodesController.cs` (new), `Views/MeetCodes/Index.cshtml`, `CourseChoice.cshtml` (new), `Security/MeetCodesTextKeys.cs` (new), `Security/SignInRoutes.cs`, `Security/InstallationSecurityServices.cs` — OpenAPI operations, policies.
- `Views/Home/Index.cshtml` — navigation entry (OpenAPI `getLanding`).
- `Configuration/InstallationSettings.cs`, `InstallationSettingsReader.cs`, `InstallationServices.cs` — FR-005, DI.
- `BackgroundServices/SynchronizationBackgroundService.cs` — `SyncLinkingStepCompleted` log line (spec §9).

**Docs**
- `docs/architecture/deployment-conventions.md` — DC-3 gains the two optional keys (spec FR-005, Story Notes).

**Tests** (supporting changes; see §7)
- `Infrastructure/Persistence/AppUserMigrationTests.cs` — the table list gains `meeting_code_link`.
- `Web/BackgroundServices/MeetLinkingHostTests.cs` — `AMarkedCode_IsNeverLinked_…` starts its host without the purge service.
- `TestInfrastructure/RetentionPurgeHost.cs` — `RemovePurgeService` made public for that test.
- Schema snapshot tests of earlier Stories updated for the US-032 additions: `DeanAccountSchemaTests`, `AdminLoginCheckEveryTimeTests`, `AccessCheckAuditSchemaTests`, `InstallationAuditEventSchemaTests`, `DeanAccountAuditSchemaTests`.
- Whitespace-only formatting of US-032 test files reported by `dotnet format`.

## 5. Validation Evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | exit 0 — 0 warnings, 0 errors |
| Tests | `dotnet test ClassroomAgent.sln --no-build` (Docker running) | exit 0 — **4345 total, 4345 passed, 0 failed, 0 skipped**, 4 m 40 s |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0 (after a whitespace-only fix of three US-032 files) |

Red → green: the 359 new US-032 tests of TEST_WRITING and the updated
`AppUserMigrationTests` now pass; the 3985 earlier tests still pass. Incremental
runs during the work: the 151 Application/Domain unit tests green before any
Infrastructure existed; then the HTTP, host, schema and purge classes, which
exposed the defects of §7 items 2 … 5.

**Delegation.** Inline: the Domain entity and audit factories, the scorer and
roster rule, the three write use cases, the two queries, the linking step and its
place in the run, the purge use case, the ports and models (contracts), diagnosing
every failing test and the fixes of §7. Delegated to `cheap-worker`: (1) the
Infrastructure persistence — configurations, migration, repository, scoring and
read sources, purge store, unit-of-work translation; (2) the Web layer —
controller, views, routes, policies, translations, configuration reader, DI, log
line; (3) the schema-snapshot test updates (that agent stopped at the session
limit after editing all five files; their run and the format fix were done here).
Full-suite and format runs were executed here as background commands.

## 6. Configuration Changes

- New optional settings `MeetLinking:MinSharePercent` (default 60) and
  `MeetLinking:MinGapPoints` (default 30), whole numbers 1 … 100; a present value
  that is blank or invalid stops the start, naming the key, not the value
  (spec FR-005, OD-006; DC-3 updated). No appsettings file changed.

## 7. Deviations and Discovered Problems

1. **`LinkMeetCodesStep` takes the unit of work as an argument**, not in its
   constructor. The architecture rule (US-007 AC-007, `WritePathRule`) flags any
   Application use case whose constructor takes `IUnitOfWork` without
   `IReadOnlyModeGuard`; spec FR-006 forbids a second guard call inside the run.
   The step is called only by `RunSynchronizationUseCase` after its guard, which
   passes its own unit of work. For the security review.
2. **Failed link saves are detached** from the change tracker (`UnitOfWork`), for
   the conflict case (db-design §7) and for any other save failure with pending
   link entries — otherwise the run's own failure save (`FailRun`) would retry the
   failed insert and the run would never be recorded as failed.
3. **Timestamp interceptor** did not know the new entity (created_at stayed
   `0001-01-01`); added (PC-6).
4. **Two Infrastructure bugs found by the HTTP tests and fixed:** a 1-based page
   offset (`(page − 1) · size`, API-8 is 0-based) and raw-SQL column aliases in
   PascalCase where the snake_case naming convention expects snake_case; a
   `SELECT DISTINCT … ORDER BY … COLLATE` that PostgreSQL rejects was rewritten
   with `GROUP BY`.
5. **Test fixture fixes** (no assertion weakened): `AppUserMigrationTests` table
   list; `MeetLinkingHostTests.AMarkedCode_…` — the purge at host start correctly
   deletes a mark whose code has no meeting yet (spec FR-016 rule 4), so that test
   now starts its host without the purge service; earlier schema snapshot tests
   gain the US-032 names.
6. **Page model has no `readOnly` flag** (OpenAPI lists one as a convenience): the
   page keeps every control enabled and enforcement is in `Application` (AD-6);
   a refused action answers `409` with the reason.
7. **Unique code** is a unique constraint (`HasAlternateKey`) backed by an index
   of the same name, because the schema test asserts a constraint
   (`contype = 'u'`); db-design §2.2 names it as a unique index — same name, same
   effect.

## 8. Open Decisions

None touched or newly required. OD-001 … OD-009 resolved.
