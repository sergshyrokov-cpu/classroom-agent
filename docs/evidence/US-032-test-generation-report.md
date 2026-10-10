---
artifact_type: test_generation_report
story: US-032
version: 1
status: DRAFT
created_at: 2026-10-10T16:14:25Z
updated_at: 2026-10-10T16:14:25Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-032-spec.md
    version: 1
  - path: docs/designs/api/US-032-openapi.yaml
    version: 1
  - path: docs/designs/database/US-032-db-design.md
    version: 1
  - path: docs/designs/database/US-032-entity-model.md
    version: 1
  - path: docs/tests/US-032-test-strategy.md
    version: 1
  - path: docs/tests/US-032-ac-test-matrix.md
    version: 1
  - path: docs/decisions/US-032-open-decisions.md
    version: 2
supersedes: null
---

# US-032 Test Generation Report

## 1. Result

**PASS — red phase verified.** `dotnet build ClassroomAgent.sln`: 0 warnings,
0 errors. Full suite (`dotnet test ClassroomAgent.sln --no-build`, Docker
running): **4345 total, 3986 passed, 359 failed, 0 skipped**, 6 m 19 s.

- **359 new tests**: 358 fail, 1 passes (§4).
- **3986 pre-existing tests** (the US-031 run closed at 3986/3986): 3985 pass;
  1 fails deliberately — `AppUserMigrationTests`, updated to expect 15
  migrations, the last `_AddMeetingCodeLinks`. No regression.
- Every failure is the missing feature (§4).

## 2. Production skeleton (OD-009 a, Owner 2026-10-10)

Compile-only; every new behaviour throws `NotImplementedException`; no EF
mapping, no migration, no DI registration, no view, no translation, no route.
IMPLEMENTATION owns and completes it.

- **Domain:** `Entities/MeetingCodeLink` (properties with private setters;
  `State`, `CanBeConfirmed`, factories and transitions throw; not in the
  `DbContext`); `Enums/MeetingCodeLinkState`; `SyncStep.Linking`; six
  `AuditAction` values; `AuditEvent`: get-only throwing `MeetCode`,
  `MeetPreviousCourseId`, `PurgedMeetCodeLinks` (get-only so EF does not map them
  before the migration) and throwing factories `MeetCodeAutoLinked`,
  `MeetCodeChanged`, `MeetCodeChangeRefused`; `RetentionPurgeCounts` gains
  optional last `MeetCodeLinks = 0`.
- **Application:** `MeetLinking/` — `RosterOnDate`, `MeetCodeScorer` (throw),
  records `RosterMembership`, `MeetCodeMeeting`, `MeetCodeScoringInput`,
  `CandidateShare` (`PercentRoundedDown` throws), `MeetCodeScore`,
  `MeetCodeDecision`; `Models/MeetLinkingThresholds` (data and `Default` 60/30 in
  full), `MeetCodeList`, `MeetCodeChangeOutcome`, `MeetCodeChangeResult`,
  `Requests/MeetCodeFormInput`; `Ports/IMeetingCodeLinkRepository`;
  `Exceptions/MeetingCodeLinkConflictException`; use cases
  `SetMeetCodeCourseUseCase`, `ConfirmMeetCodeLinkUseCase`,
  `MarkMeetCodeNotACourseUseCase` (constructors; `ExecuteAsync` throws).
- **Web:** `InstallationSettingsReader.MeetLinkingMinSharePercentKey`,
  `MeetLinkingMinGapPointsKey` (constants only).

**Binding notes for IMPLEMENTATION**

1. Turn the throwing get-only `AuditEvent` properties into mapped
   `{ get; private set; }` with the configuration and migration
   `AddMeetingCodeLinks` (db-design §8); map `MeetingCodeLink`; register
   `IMeetingCodeLinkRepository`, the read and scoring sources, the use cases and
   `MeetLinkingThresholds` (DI singleton read by `InstallationSettingsReader`).
2. **Deviations from the entity model the tests fix** (entity model allowed
   TEST_WRITING to adjust signatures):
   - one raw-pairs form type `MeetCodeFormInput` for the three writes, instead of
     three typed forms — a repeated field must be detectable (as US-027);
   - `CourseExistsAsync` is on `IMeetingCodeLinkRepository`, not on the read
     source, so the write use cases depend on one port;
   - use cases expose `ExecuteAsync(actorId, actorRole, meetingCode, form,
     requestId, ct)` → `MeetCodeChangeResult(Outcome, List, ReturnPage)`;
     read-only mode throws `ReadOnlyModeException` after committing the refused row
     (as `ReportTemplateRefusalAudit`); the unit of work throws
     `MeetingCodeLinkConflictException` for a concurrency-stamp mismatch and for
     `uq_meeting_code_link_meeting_code`, answered as `StateChanged`.
3. **Names the tests fix:**
   - translation keys `MeetCodes.NavigationEntry`, `MeetCodes.Title`,
     `MeetCodes.List.{Unassigned,Linked,NotACourse}`,
     `MeetCodes.Empty.{Unassigned,Linked,NotACourse}`, `MeetCodes.NoCandidates`,
     `MeetCodes.Automatically`, `MeetCodes.DeletedAccount`,
     `MeetCodes.NotConfirmed`,
     `MeetCodes.Action.{PickCourse,Confirm,Relink,MarkNotACourse}`,
     `MeetCodes.Choice.{Title,Candidates,OtherCourses,Submit}`,
     `MeetCodes.Message.{CoursePicked,Relinked,MarkRemoved,Confirmed,Marked,CodeNotFound,StateChanged,QueryInvalid}`,
     `MeetCodes.FieldError.{SameCourse,CourseNotFound}`, `LastSync.Step.Linking`;
   - log event `SyncLinkingStepCompleted` (Information; `RunId`, `CodesScored`,
     `LinksCreated`); no code, email or course name in any log line;
   - a **blank** `MeetLinking:*` value is invalid (spec FR-005 "empty"), unlike
     `Sync:IntervalMinutes`.
4. **Refused-row action** (api-design §2.5): from `expectedState` when it parses
   (`unassigned` → `MeetCodeCoursePicked`, `linked` → `MeetCodeRelinked`,
   `marked` → `MeetCodeMarkRemoved` on the link path), else
   `MeetCodeCoursePicked`; the confirmation and mark paths always use their own
   action; the code only when it passes its shape.
5. **Watermark after a linking failure** is not asserted (test strategy §7): the
   host test checks failed step `linking` and that the stored meetings remain.

## 3. Files

Created (tests), under `tests/ClassroomAgent.Tests/`:
- `Application/MeetLinking/RosterOnDateTests.cs`, `MeetCodeScorerTests.cs`
- `Application/UseCases/MeetingCodeLinkInvariantTests.cs`,
  `MeetCodeAuditEventTests.cs`, `MeetCodeChangeTests.cs`,
  `RetentionPurgeMeetLinkTests.cs`
- `Infrastructure/Persistence/MeetingCodeLinkSchemaTests.cs`,
  `MeetingCodeLinkRepositoryTests.cs`
- `Web/BackgroundServices/MeetLinkingHostTests.cs`
- `Web/Configuration/MeetLinkingConfigurationTests.cs`
- `Web/Logging/MeetLinkingLoggingTests.cs`
- `Web/Pages/MeetCodesPageTests.cs`, `MeetCodeActionsHttpTests.cs`
- `Web/Security/MeetCodesAuthorizationTests.cs`
- `Web/Localization/MeetCodesTranslationTests.cs`
- `TestInfrastructure/MeetCodeWorld.cs`, `MeetLinkingTestData.cs`,
  `MeetCodesHostExtensions.cs`, `MeetLinkRows.cs`

Modified (tests): `Infrastructure/Persistence/AppUserMigrationTests.cs`
(15 migrations, the last `_AddMeetingCodeLinks`).

Production files: the skeleton of §2 (20 new files; edits to `SyncStep`,
`AuditAction`, `AuditEvent`, `RetentionPurgeCounts`,
`InstallationSettingsReader`). Artifact: OD-009 added to
`docs/decisions/US-032-open-decisions.md` (v2).

Commands: `dotnet build ClassroomAgent.sln`; `dotnet test ClassroomAgent.sln
--no-build`; per-class `dotnet test ClassroomAgent.sln --filter
FullyQualifiedName~<Class>`.

**Delegation.** Test design, the skeleton, `MeetCodeWorld` and the Application/
Domain tests (`RosterOnDate`, `MeetCodeScorer`, `MeetingCodeLink`, `AuditEvent`,
the three use cases) and the red-phase verdict were done inline. Three
`cheap-worker` agents wrote, from fully specified scenarios, (1) the host
linking, configuration and logging tests, (2) the page, action, authorization and
translation tests, (3) the schema, repository and purge tests, and ran their own
classes. Agent (2) stalled after writing its files; its classes were built and run
here (89/89 red, reasons checked) and it was stopped.

## 4. Red phase — why each class fails

| Reason | Classes (failing count) |
|---|---|
| `NotImplementedException` from the skeleton | `MeetCodeChangeTests` 65, `MeetCodeScorerTests` 35, `MeetCodeAuditEventTests` 22, `MeetingCodeLinkInvariantTests` 20 (guard tests: `Assert.Throws<ArgumentException/InvalidOperationException>` got `NotImplementedException`), `RosterOnDateTests` 9, part of `MeetingCodeLinkRepositoryTests` |
| `42P01 relation "meeting_code_link" does not exist` / `42703 column "meet_code"` (no migration) | `MeetingCodeLinkSchemaTests` 64, `MeetLinkingHostTests` 11, `RetentionPurgeMeetLinkTests` 7, `MeetCodesAuthorizationTests` 20 (they seed a link row first), most of `MeetCodeActionsHttpTests` and `MeetCodesPageTests` |
| new paths answer `404` from the catch-all (no controller) | the rest of `MeetCodeActionsHttpTests` 41 and `MeetCodesPageTests` 25 |
| service not registered (`MeetLinkingThresholds`, `IMeetingCodeLinkRepository`) | `MeetLinkingConfigurationTests` (valid cases), `MeetingCodeLinkRepositoryTests` |
| host starts with an invalid `MeetLinking:*` value | `MeetLinkingConfigurationTests` 16 invalid cases |
| keys missing in the resx files | `MeetCodesTranslationTests` 3 |
| no `SyncLinkingStepCompleted` event | `MeetLinkingLoggingTests` 2 |
| 14 migrations, not 15 | `AppUserMigrationTests` 1 |

**Tests that pass now.** One:
`MeetingCodeLinkSchemaTests.ACompletedRow_WithTheLinkingStep_IsRejected` — the
current `ck_sync_state_failed_step` already rejects `linking` on any row. It is
meaningful only together with its red partner
`AFailedRow_WithTheLinkingStep_IsAccepted`.

No failure is caused by a test-code error: each class's failures were checked by
reason; the three agents fixed their own compile and SQL errors against existing
tables before reporting.

## 5. Untested Acceptance Criteria

None. Every AC-001 … AC-018 maps to at least one test (`ac_test_matrix`).
AC-018 holds by construction: the Google ports are the substituted fakes, and the
write use cases take no Google port.

## 6. Open Decisions

OD-001 … OD-008 resolved before activation; OD-009 (skeleton) resolved (a) by the
Owner on 2026-10-10. None open.

## 7. Findings for later stages

- `MeetingCodeLinkSchemaTests.TheLowerEmailIndex_IsOnLowerOfEmail` asserts the
  index definition contains `lower(` and `email` and no `UNIQUE`; the exact
  definition text is unverified until the index exists.
- The concurrency tests depend on `ConcurrencyStamp` being an EF concurrency
  token and on the unit of work translating both conflicts (db-design §7).
