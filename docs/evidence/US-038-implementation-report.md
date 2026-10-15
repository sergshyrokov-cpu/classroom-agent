---
artifact_type: implementation_report
story: US-038
version: 1
status: DRAFT
created_at: 2026-10-15T10:30:00Z
updated_at: 2026-10-15T10:30:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-038-erase-participant.md
    version: null
  - path: docs/specifications/US-038-spec.md
    version: 1
  - path: docs/decisions/US-038-open-decisions.md
    version: 3
  - path: docs/designs/api/US-038-api-design.md
    version: 1
  - path: docs/designs/api/US-038-openapi.yaml
    version: 1
  - path: docs/designs/database/US-038-db-design.md
    version: 1
  - path: docs/designs/database/US-038-entity-model.md
    version: 1
  - path: docs/tests/US-038-test-strategy.md
    version: 1
  - path: docs/tests/US-038-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-038-test-generation-report.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-038 Implementation Report — Erase all data of one participant on request

## 1. Summary

Implemented: the Admin-only erasure page under `/settings/erasure` (search by POST,
confirmation, erasure, lifting of an exclusion), the erasure transaction behind the
exclusive write fence, the exclusion (`erased_participant`) with an HMAC-SHA-256
email fingerprint and the installation salt (`erasure_salt`, created lazily), the
exclusion check under the shared fence in synchronization (per course) and in the
Meet load (per page), audit rows and the `ParticipantErased` log line, the
read-only refusal with its audit row, translations in `uk` and `en`, and migration
`AddParticipantErasure`. The compile-only skeleton of OD-002 (a) is completed; no
`NotImplementedException` remains.

Validation: build 0 warnings; full suite green (§5); `dotnet format
--verify-no-changes` clean.

Limitations and deviations: §7 — notably D-1 (the erasure deletes through the change
tracker, not set-based statements) and D-2 (a pre-existing US-015 defect fixed
because AC-011 exposed it).

## 2. Source Artifacts

As in the front matter: Story; spec v1; open decisions v3 (OD-001 a, OD-002 a); API
design v1 and OpenAPI v1; DB design v1 and entity model v1; test strategy v1, AC
matrix v1, test generation report v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (class) | Status |
|---|---|---|---|
| AC-001 | `GetErasureConfirmationQuery`, `ParticipantErasureReadSource.GetConfirmationAsync`, `Views/Erasure/Confirmation.cshtml` | `ErasurePageTests`, `ErasureSearchTests`, `ErasureQueryTests` | pass |
| AC-002 | `EraseParticipantUseCase`, `ParticipantErasureStore`, `MeetSession.ClearOrganizer`, `Course.ClearOwner`, `ErasureController.Erase` | `ParticipantErasureTests`, `EraseParticipantUseCaseTests`, `ErasurePageTests` | pass |
| AC-003 | `EraseParticipantUseCase` (mark before any read), `ErasureMark.IsSet`, confirmation re-render with `MarkError` | `EraseParticipantUseCaseTests`, `ErasurePageTests`, `ErasureInvariantTests` | pass |
| AC-004 | `RunSynchronizationUseCase.ImportCourseAsync` (fence, `GetExcludedGoogleUserIdsAsync`, owner cleared) | `ErasureExclusionSyncTests` | pass |
| AC-005 | `RunSynchronizationUseCase.StoreMeetPageAsync`, `ExcludedEmailsAsync` | `ErasureExclusionMeetTests` | pass |
| AC-006 | `ErasedParticipant`, `ErasureSalt`, `EmailFingerprint`, `ErasureSaltStore` | `ParticipantErasureTests`, `ErasureInvariantTests`, `ParticipantErasureSchemaTests` | pass |
| AC-007 | `LiftErasureExclusionUseCase`, `ErasedParticipantRepository.GetByFingerprintAsync` | `LiftErasureExclusionUseCaseTests`, `ErasureExclusionSyncTests`, `ErasurePageTests` | pass |
| AC-008 | policy `EraseParticipantData`; `[Authorize]` on `ErasureController`; read-only guard first in both use cases; home link in `nav#admin-settings` | `ErasureAuthorizationTests`, `ErasureReadOnlyHttpTests`, unit read-only tests | pass |
| AC-009 | `AuditEvent.ParticipantErased` / `ErasureExclusionLifted`, `ErasureLog.ParticipantErased` | `ParticipantErasureTests`, `ErasureLoggingTests`, `ParticipantErasureSchemaTests` | pass |
| AC-010 | `ErasureFence` (advisory lock), shared entry in both writers, exclusive in erasure | `ErasureExclusionSyncTests`, `ErasureExclusionMeetTests` | pass |
| AC-011 | submitter filter in `ImportCourseAsync`; D-2 | `ErasureExclusionSyncTests.AnExcludedSubmitterOffTheRoster_IsNotCreated` | pass |
| AC-012 | `lower(email)` matching in store and read source; `NormalizeEmail` in the Meet check | `ParticipantErasureTests`, `ErasureExclusionMeetTests` | pass |
| AC-013 | `SearchParticipantsForErasureQuery`, `ParticipantErasureReadSource.SearchAsync` (EF `Contains`, literal), POST search | `ErasureSearchTests`, `ErasurePageTests`, `ErasureQueryTests` | pass |
| AC-014 | limit 51 → 50 + `Truncated` | `ErasureQueryTests`, `ErasureSearchTests`, `ErasurePageTests` | pass |
| AC-015 | `ParticipantNotFound` (rollback inside the transaction); route constraint; `TryParseId` | `EraseParticipantUseCaseTests`, `ParticipantErasureTests`, `ErasureAuthorizationTests` | pass |
| AC-016 | lifting removes every matching exclusion, one audit row each | `LiftErasureExclusionUseCaseTests`, `ParticipantErasureSchemaTests` | pass |
| AC-017 | `ReadOnlyNotice` on page and confirmation; buttons disabled; refusal audited | `ErasureReadOnlyHttpTests`, unit tests | pass |
| AC-018 | purge untouched (no change to `RetentionPurgeStore`) | `ParticipantErasureTests.TheRetentionPurge_LeavesExclusionsUntouched` | pass |
| AC-019 | null fingerprint path; `NoEmailNotice` | `ParticipantErasureTests`, `EraseParticipantUseCaseTests`, `ErasureExclusionSyncTests`, `ErasureExclusionMeetTests` | pass |
| AC-020 | global antiforgery filter covers the three POSTs | `ErasureAuthorizationTests`, `InstallationCookieTests` | pass |
| AC-021 | 37 keys in `SharedResource.uk.resx` / `.en.resx`, `ErasureTextKeys` | `ErasureTranslationTests` | pass |

## 4. Change Set

### 4.1 Production — created

| File | Trace |
|---|---|
| Domain `Entities/ErasedParticipant.cs`, `Entities/ErasureSalt.cs`, `Rules/EmailFingerprint.cs`, `Rules/ErasureCounts.cs` | entity model §1–§4 (skeleton completed) |
| Application `Ports/IErasureFence.cs`, `IErasureSaltStore.cs`, `IErasedParticipantRepository.cs`, `IParticipantErasureStore.cs`, `IParticipantErasureReadSource.cs` | entity model §7 |
| Application `UseCases/EraseParticipantUseCase.cs`, `LiftErasureExclusionUseCase.cs`, `SearchParticipantsForErasureQuery.cs`, `GetErasureConfirmationQuery.cs`; `Validation/ErasureMark.cs` | entity model §8; FR-002 … FR-009, VR-001 … VR-003 |
| Application `Models/*` (9 read records/outcomes) and `Models/Dtos/*` (8 skeleton DTOs) | entity model §7; openapi schemas |
| Application `Models/Dtos/ErasurePageModel.cs`, `LiftBlock.cs`, `ErasureMessageKey.cs` | openapi `ErasurePageModel`, `LiftBlock`, `ErasureMessageKey` |
| Infrastructure `Persistence/Configurations/ErasedParticipantConfiguration.cs`, `ErasureSaltConfiguration.cs` | db-design §2, §3 |
| Infrastructure `Persistence/ErasureFence.cs`, `ParticipantErasureStore.cs`, `ParticipantErasureReadSource.cs`, `Repositories/ErasedParticipantRepository.cs`, `Repositories/ErasureSaltStore.cs` | entity model §9; db-design §7, §8, §10 |
| Infrastructure `Migrations/20261015091505_AddParticipantErasure.cs` (+ Designer) | db-design §11 (PC-2) |
| Web `Controllers/ErasureController.cs`, `Views/Erasure/Index.cshtml`, `Views/Erasure/Confirmation.cshtml` | openapi five operations; FR-001 … FR-009 |
| Web `Security/ErasureTextKeys.cs`, `Security/ErasureLog.cs` | FR-018; FR-013 log line |

### 4.2 Production — modified

| File | Trace |
|---|---|
| Domain `Entities/AuditEvent.cs`, `Enums/AuditAction.cs`, `Enums/AuditTargetType.cs` | entity model §5; db-design §9 (skeleton factories completed; misplaced doc comment of `DeanSignInRefusedUnknownLogin` restored) |
| Domain `Entities/MeetSession.cs` | entity model §5 (`OrganizerEmail` nullable), FR-007 (`ClearOrganizer`) |
| Domain `Entities/Course.cs` | FR-007 (`ClearOwner`) — supporting |
| Application `Authorization/InstallationPolicies.cs` | api-design §2.7 |
| Application `UseCases/RunSynchronizationUseCase.cs` | entity model §8 (fence, exclusions, salt), FR-011, FR-012; D-2 |
| Application `MeetLinking/MeetCodeMeeting.cs`, `MeetLinking/MeetCodeScorer.cs`, `Models/MeetReportMeetingRecord.cs`, `UseCases/MeetReportRules.cs` | I-9 readers of the organizer email (§7 D-3) |
| Application `Localization/SharedResource.uk.resx`, `.en.resx` | FR-018 (37 keys each) |
| Infrastructure `ClassroomAgentDbContext.cs`, `Configurations/AuditEventConfiguration.cs`, `CourseConfiguration.cs`, `MeetSessionConfiguration.cs`, `Migrations/ClassroomAgentDbContextModelSnapshot.cs` | db-design §4, §6, §9; skeleton `Ignore` lines removed |
| Infrastructure `MeetCodeScoringSource.cs`, `MeetCodesReadSource.cs` | I-9 readers |
| Web `Configuration/InstallationServices.cs` | DI registration |
| Web `Security/InstallationSecurityServices.cs` | policy registration |
| Web `Security/SignInRoutes.cs` | the five routes, TempData key |
| Web `Views/Home/Index.cshtml` | FR-001 link in `nav#admin-settings` |

### 4.3 Tests — modified by IMPLEMENTATION (supporting)

| File | Trace |
|---|---|
| `TestInfrastructure/SyncWorld.cs` | test strategy §9: the new constructor ports (`NoErasures`) |
| `Application/UseCases/CourseWorkFailureTests.cs`, `CourseWorkImportTests.cs`, `SubmissionImportTests.cs`, `SubmitterWithoutRosterTests.cs` | the same constructor arguments in their positional `SecondRun` |
| `Infrastructure/Persistence/DeanAccountSchemaTests.cs`, `Web/UseCases/AdminLoginCheckEveryTimeTests.cs`, `Web/Persistence/AccessCheckAuditSchemaTests.cs` | table-set snapshots gain the two db-design tables; the audit column snapshot gains the five count columns (db-design §9.2) — the TEST_WRITING stage amended five such snapshots and missed these |
| `TestInfrastructure/HostEndpoint.cs` | the antiforgery enumeration needs a digit sample for `{participantId}`, as US-012 added for `{deanId}` |

Test files created and amended at TEST_WRITING are listed in the test generation
report §1, §2; none of their assertions was changed here.

No secret, generated database file, `.xlsx` or IDE-local file is in the change set.

## 5. Validation Evidence

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` (incremental, after each group) | 0 errors, 0 warnings |
| `dotnet test --filter` (17 classes: new non-HTTP + amended schema + architecture) | 272 / 271 passed, 1 failed (AC-011 → D-2); after the fix the sync classes 27 / 27 |
| `dotnet test ClassroomAgent.sln` (full, before the snapshot updates) | 5917 / 5912 passed, 5 failed — the four snapshot tests and the antiforgery enumeration of §4.3 |
| `dotnet test --filter` (the 5 plus every US-038 class) | 269 / 269 passed |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0 (one whitespace finding in `MeetCodesReadSource.cs` fixed with `dotnet format --include`) |
| `dotnet test ClassroomAgent.sln` (final, full) | see §5.1 |

### 5.1 Final full run

- `dotnet test ClassroomAgent.sln` (full, after every change): **5917 total, 5917
  passed, 0 failed, 0 skipped**, 7 min 14 s, exit code 0.
- One full run before it ended 5904 / 5917: thirteen tests in eleven classes failed
  during a stall of the test PostgreSQL container — Npgsql read timeouts in
  `CreateDatabaseAsync` / `MigrateAsync` and HTTP 500 at sign-in in the same minutes,
  tests taking 7–8 min each. Those eleven classes rerun alone: 329 / 329 passed; then
  the clean full run above. An environment failure (TC-2), not a code one.

Delegation: every full test run and the format check ran on `cheap-worker`
subagents; the filtered reruns and the incremental builds used to drive the nullable
fallout of `OrganizerEmail` were run inline, because each result decided the next
edit. One temporary diagnostic test was written, run and deleted to read the
host's log for the AC-011 failure. Production code, the translations and the report
were written inline.

## 6. Configuration Changes

None. No `appsettings` key, no package, no hosted service. DI registrations and one
policy only (§4.2).

## 7. Deviations and Discovered Problems

- **D-1 — the erasure deletes through the change tracker, not set-based.** db-design
  §7.1 names `ExecuteDeleteAsync` / `ExecuteUpdateAsync` "as RetentionPurgeStore".
  The existing architecture test `AuditDeletionConfinementTests` (US-037) allows set-
  based deletes only in `RetentionPurgeStore` and `ExecuteUpdate` nowhere else,
  because such statements bypass the read-only commit backstop. `ParticipantErasureStore`
  therefore loads the rows and removes / changes them through EF Core; the counts are
  the rows removed; everything commits in the use case's one `SaveChanges`, which
  passes `ReadOnlyModeUnitOfWork`. Every functional requirement holds; spec §9
  "set-based" is met as one query per kind plus batched commands — round trips grow
  only with EF's batch size, not per row. The salt insert (`INSERT … ON CONFLICT DO
  NOTHING`, db-design §3.3) is raw SQL, as designed. Concurrent erasures are
  serialized by the exclusive fence, so FR-016 still holds. **For the security review
  and the human gate.**
- **D-2 — pre-existing US-015 defect fixed.** A submitter not on the roster and not
  yet stored was given a submission with `participant_id = 0`: the participant had
  been staged but not saved. The foreign key failed and the run stayed `running`. No
  earlier host-level test exercised it; AC-011 does. `ResolveSubmitterAsync` now saves
  inside the course transaction right after staging such a person. A second effect
  observed — a run whose course transaction fails can stay `running` — was not
  investigated further; it is outside this Story.
- **D-3 — readers of an unknown organizer (spec I-9), as checked.** `MeetCodeScorer`:
  an unknown organizer makes no course a candidate and is not an "organizer" to
  exclude from the participants. `MeetCodeScoringSource`, `MeetCodesReadSource`: an
  unknown organizer is not listed. `MeetReportRules`: matches no connection, and
  the meeting is **not** flagged "organizer is not a course teacher" (nothing is known
  about them); `Person` renders it as an unnamed person. `MeetReportSource`,
  `GetMeetReportsQuery`, `GetMeetReportMeetingQuery`: pass it through. The retention
  purge does not read it.
- **D-4 — a Meet conference whose only connections are excluded is not stored**
  (FR-012 forbids storing their participations, and a meeting needs at least one
  connection). With another allowed connection the meeting is stored, without the
  organizer when the organizer is excluded.
- **D-5 — the salt read by the Meet load** is cached for the run only once found; while
  none exists it is re-read on every page, so a first erasure during a run is still
  honoured (db-design §7.4 says "once per run is enough", which holds only once the
  salt exists).
- **D-6 — the lifting button is disabled in read-only mode** as the erasure button is
  (spec I-8 covers both actions); a forced request still gets `409`.

## 8. Open Decisions

OD-001 (a) and OD-002 (a) applied as resolved. None newly required; no
security-sensitive decision is missing. D-1 is an implementation deviation from a
design mechanism under an existing architecture rule, recorded here for review
rather than as an Open Decision.
