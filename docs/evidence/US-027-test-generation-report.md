---
artifact_type: test_generation_report
story: US-027
version: 1
status: DRAFT
created_at: 2026-10-04T20:40:00Z
updated_at: 2026-10-04T20:40:00Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-027-test-strategy.md
    version: 1
  - path: docs/tests/US-027-ac-test-matrix.md
    version: 1
  - path: docs/specifications/US-027-spec.md
    version: 2
  - path: docs/designs/api/US-027-openapi.yaml
    version: 1
  - path: docs/designs/database/US-027-db-design.md
    version: 1
  - path: docs/designs/database/US-027-entity-model.md
    version: 1
  - path: docs/decisions/US-027-open-decisions.md
    version: 3
supersedes: null
---

# US-027 Test Generation Report

## 1. Result

**PASS — red phase verified.** `dotnet build ClassroomAgent.sln`: 0 warnings,
0 errors. Full suite (`dotnet test ClassroomAgent.sln`, Docker running):
**3404 total, 3020 passed, 384 failed, 0 skipped**, 2 m 51 s. All 384 failures
are new US-027 tests failing for missing production behaviour; the 2 other new
tests are green by design (§4). **No existing test regressed** — every failing
test belongs to a class listed in §3.

## 2. Production skeleton (OD-005 a, Owner 2026-10-04)

Compile-only; members throw `NotImplementedException`; nothing registered in DI;
no migration; no existing behaviour changed. IMPLEMENTATION owns and completes it.

- **Domain:** enums `ReportView`, `ReportScaleMode`, `ReportMarkKind`,
  `ReportLateMarkKind`, `ReportCellState`; values `ReportMark`, `ReportLateMark`,
  `ReportScaleRow`, `ReportTemplateSettings`; `BuiltInReportTemplates`,
  `TwelvePointScale` (throw); entities `ReportTemplate`, `ReportTemplateMark`,
  `ReportTemplateScaleRow` (not mapped); `CourseWork.ScheduledTime` as a throwing
  get-only property (so EF does not map it — the model stays unchanged);
  `CourseWorkDetails` gains `ScheduledTime` as an **optional** last parameter
  (IMPLEMENTATION may make it required); `AuditAction` +3 members,
  `AuditTargetType.ReportTemplate`.
- **Application:** `InstallationPolicies.UseReportTemplates` /
  `EditReportTemplates` (constants only); ports `IJournalFieldSource`,
  `IReportTemplateRepository`; records `JournalLessonRecord`,
  `JournalCourseMemberRecord`, `ReportTemplateListRecord`; results and outcomes
  `ReportTemplateOutcome`, `ReportPageOutcome`, `ReportPageResult`,
  `ReportTemplateFormResult`, `ReportTemplateDeletionResult`,
  `ReportTemplateSaveResult`; requests `ReportRequest`, `ReportTemplateFormInput`;
  the openapi DTOs under `Models/Dtos` (`Report*`, `Grading*`, `LessonTopic*`,
  `PersonName`, `ScaleRange`); `UniqueReportTemplateNameViolationException`; use
  cases `ListReportTemplatesQuery`, `GetReportTemplateFormQuery`,
  `SaveReportTemplateUseCase`, `DeleteReportTemplateUseCase`, `GetReportQuery`,
  `ReportTemplateReference` (throw).
- **Infrastructure:** `JournalFieldSource`, `ReportTemplateRepository` (throw).

**Binding signatures for IMPLEMENTATION** (beyond the entity model):

| Type | Signature |
|---|---|
| `ListReportTemplatesQuery(IReportTemplateRepository, SchoolTimeZone)` | `ExecuteAsync(CultureInfo uiCulture, ReportTemplateConfirmationKey? confirmation, CancellationToken)` → `ReportTemplateListPageModel` |
| `GetReportTemplateFormQuery(IReportTemplateRepository)` | `New()`; `CopyAsync(string? templateRef, string builtInName, string copySuffix, ct)`; `EditAsync(string?, ct)`; `DeletionAsync(string?, ct)` — translated texts come from the host |
| `SaveReportTemplateUseCase(IReadOnlyModeGuard, IReportTemplateRepository, IAuditEventRepository, IUnitOfWork, ServiceWriteScope, TimeProvider)` | `CreateAsync(long actorId, AppRole actorRole, ReportTemplateFormInput, string? requestId, ct)`; `ChangeAsync(actorId, actorRole, string? templateRef, form, requestId, ct)` → `ReportTemplateSaveResult` |
| `DeleteReportTemplateUseCase(same six)` | `ExecuteAsync(actorId, actorRole, string? templateRef, requestId, ct)` → `ReportTemplateOutcome` |
| `GetReportQuery(IJournalFieldSource, IReportTemplateRepository, SchoolTimeZone, TimeProvider)` | `ExecuteAsync(ReportRequest, CultureInfo uiCulture, ct)` → `ReportPageResult` |
| `ReportTemplateFormInput` | the posted fields as ordered name/value pairs; `Application` decides structure; unknown names (the antiforgery token) ignored |

The detailed semantics the tests assert (field-error keys and fields, row
numbers, refused-audit target id, list and report shapes) are those of the
TEST_WRITING brief recorded in the matrix and §5 below.

## 3. Test files

Fixtures written by the orchestrating session; test classes by four Sonnet
subagents from fixed briefs (the matrix plus the binding semantics); the full
suite run by the orchestrating session.

| File | Cases | Before implementation |
|---|---|---|
| `TestInfrastructure/ReportTemplateTestData.cs` | fixture | — |
| `TestInfrastructure/ReportTemplateFormBuilder.cs` | fixture | — |
| `TestInfrastructure/ReportTemplateWorld.cs` | fixture | — |
| `TestInfrastructure/FakeJournalFieldSource.cs` | fixture | — |
| `TestInfrastructure/ReportHostExtensions.cs` | fixture | — |
| `Application/UseCases/ReportTemplateExpectations.cs` | internal helper | — |
| `Application/UseCases/ReportTemplateInvariantTests.cs` | 30 | 29 red, 1 green |
| `Application/UseCases/ReportTemplateFormValidationTests.cs` | 86 | 86 red |
| `Application/UseCases/ReportTemplateSaveTests.cs` | 31 | 31 red |
| `Application/UseCases/ReportTemplateDeleteTests.cs` | 18 | 18 red |
| `Application/UseCases/ReportTemplateFormQueryTests.cs` | 8 | 8 red |
| `Application/UseCases/ListReportTemplatesQueryTests.cs` | 9 | 9 red |
| `Application/UseCases/CourseWorkScheduledTimeTests.cs` | 3 | 3 red |
| `Application/UseCases/ReportRequestValidationTests.cs` | 44 | 44 red |
| `Application/UseCases/ReportContentTests.cs` | 10 | 10 red |
| `Application/UseCases/ReportCellTests.cs` | 45 | 45 red |
| `Infrastructure/Google/GoogleClassroomReaderTests.cs` (+1 method) | 1 | 1 red |
| `Infrastructure/Persistence/JournalFieldSourceTests.cs` | 6 | 6 red |
| `Infrastructure/Persistence/ReportTemplateRepositoryTests.cs` | 7 | 7 red |
| `Web/Persistence/ReportTemplateSchemaTests.cs` | 19 | 18 red, 1 green |
| `Web/UseCases/ReportTemplateReadOnlyTests.cs` | 6 | 6 red |
| `Web/Pages/ReportTemplatePagesTests.cs` | 12 | 12 red |
| `Web/Pages/ReportPageTests.cs` | 7 | 7 red |
| `Web/Security/ReportTemplateAuthorizationTests.cs` | 39 | 39 red |
| `Web/Localization/ReportTemplateTranslationTests.cs` | 2 | 2 red |
| `Web/Logging/ReportTemplateLoggingTests.cs` | 3 | 3 red |
| **Total new** | **386** | **384 red, 2 green** |

Modified existing test file: `GoogleClassroomReaderTests.cs` — one method added,
nothing else changed.

## 4. Red-phase classification

- **Unit and adapter (284 red):** `NotImplementedException` from the skeleton use cases and
  entities; tests expecting `ReadOnlyModeException` / `ArgumentException` report
  `NotImplementedException` as the actual type.
- **PostgreSQL (37 red):** `42P01 relation "report_template" does not exist`,
  `42703 column "scheduled_time" does not exist`, `23514 ck_audit_event_action`
  (new action codes not yet allowed), or `NotImplementedException` from the
  skeleton repositories.
- **HTTP (63 red):** `404` — the pages do not exist (the anonymous and
  restricted-session theories too: an unrouted path answers `404` before the
  redirect) — or `42P01` while seeding templates; missing translation keys.

Green by design (2):

- `ReportTemplateInvariantTests.NoUseCaseOfThisStory_TakesAGooglePort` —
  reflection over the constructors; holds from the skeleton on, a regression guard
  for S-04.
- `ReportTemplateSchemaTests.TheModelHasNoPendingChanges` — the skeleton maps
  nothing; turns red if IMPLEMENTATION maps the entities without the migration.

No other new test passes before implementation.

## 5. Findings

Non-blocking; for IMPLEMENTATION and the human at `HUMAN_PR_APPROVAL`.

- **F-1 Copy suffix wording is fixed by a test.** No translation key for
  " (copy)" is named in the openapi. `ReportTemplatePagesTests` expects the copy
  form's name to be the built-in name followed by **" (копія)"** in Ukrainian.
  IMPLEMENTATION uses exactly that text (key name free).
- **F-2 Scale row numbers.** For `ScaleOverlap` / `ScaleGap` the tests name the
  later of the two offending rows in `from` order, by its submitted number; "not
  starting at 0" names the row with the lowest `from`, "not ending at 100" the row
  with the highest. A reading of spec VR-004 ("each starts at the previous `to` +
  1").
- **F-3 Logging wait.** `AValidationFailure_IsLoggedWithoutTheValue` waits for any
  `Warning` event, not one tied to the request; the "value absent" assertion is the
  real check.
- **F-4 Refused audit row type.** The tests fix the refused row's `TargetId` rule;
  its `TargetType` is asserted by the schema check
  (`ck_audit_event_report_template_shape` requires `report_template`), not by the
  unit tests.
- **F-5 Ungraded = no maximum.** A lesson with `maxPoints` null is ungraded, one
  with any number (zero included) graded (BR-052); a zero maximum shows raw points
  (spec I-5).

## 6. Coverage

Every Acceptance Criterion AC-001 … AC-014 maps to at least one test
(`docs/tests/US-027-ac-test-matrix.md`). Every openapi operation has status-code
tests. No untested criterion. No open decision (OD-001 … OD-005 resolved).

## 7. Commands

- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 3404 / 3020 passed / 384 failed / 0 skipped.
- Per-class runs by the subagents with `dotnet test tests/ClassroomAgent.Tests --filter "FullyQualifiedName~<Class>"`.
