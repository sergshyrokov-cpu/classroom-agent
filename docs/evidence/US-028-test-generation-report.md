---
artifact_type: test_generation_report
story: US-028
version: 1
status: DRAFT
created_at: 2026-10-06T06:10:00Z
updated_at: 2026-10-06T06:10:00Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-028-test-strategy.md
    version: 1
  - path: docs/tests/US-028-ac-test-matrix.md
    version: 1
  - path: docs/decisions/US-028-open-decisions.md
    version: 2
supersedes: null
---

# US-028 Test Generation Report

## 1. Result

Red phase verified. The solution builds with 0 warnings and 0 errors. Of 3 776
tests, the 179 new ones are **176 red** for missing implementation and **3
green** as regression guards (§5). The 3 597 existing tests pass. One existing
architecture test went red because of the skeleton and was fixed by completing
the skeleton (§4).

## 2. Commands

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln` (Docker running, Testcontainers) | total 3 776, failed 177, passed 3 599, skipped 0; 5 m 23 s |
| after the registry fix: `dotnet test tests/ClassroomAgent.Tests --filter "FullyQualifiedName~ReadOnlyEnforcementTests\|FullyQualifiedName~WritePathRuleTests\|…"` | total 18, failed 0 |

So the suite now stands at 176 failed, all new, and 3 600 passed.

## 3. Files

**Test sources created** (`tests/ClassroomAgent.Tests/`):

| File | Cases |
|---|---|
| `Application/UseCases/ExportJournalCommandTests.cs` | 32 |
| `Application/UseCases/ReportWorkbookMapperTests.cs` | 36 |
| `Application/UseCases/JournalExportFileNameTests.cs` | 9 |
| `Application/UseCases/JournalExportedAuditEventTests.cs` | 4 |
| `Infrastructure/Export/ClosedXmlReportRendererTests.cs` | 19 |
| `Infrastructure/Persistence/JournalExportAuditSchemaTests.cs` | 14 |
| `Web/Pages/JournalExportTests.cs` | 24 |
| `Web/Security/JournalExportAuthorizationTests.cs` | 9 |
| `Web/Localization/JournalExportTranslationTests.cs` | 30 |
| `Web/Logging/JournalExportLoggingTests.cs` | 2 |
| `TestInfrastructure/FakeReportTexts.cs`, `FakeReportRenderer.cs`, `JournalExportWorld.cs`, `JournalExportHostExtensions.cs`, `BinaryResponse.cs` | fixtures |

**Test source modified:** `TestInfrastructure/FormClient.cs` gains
`SendForBytesAsync`. The existing methods are unchanged.

**Production: compile-only skeleton (OD-005 a).**
- New, every body throws `NotImplementedException`: `Domain/Enums/ExportFormat`;
  `Application/Models/Export/*` (7 types); `Application/Ports/IReportRenderer`,
  `IReportTexts`; `Application/Models/Requests/JournalExportRequest`;
  `Application/Models/JournalExportOutcome`, `JournalExportResult`;
  `Application/Models/Dtos/JournalExportFile`, `ExportField`,
  `ExportMessageKey`, `ExportFieldError`, `ExportAction`;
  `Application/UseCases/ReportWorkbookMapper`, `JournalExportFileName`,
  `ExportJournalCommand`; `Infrastructure/Export/ClosedXmlReportRenderer`;
  `Web/Security/LocalizedReportTexts`.
- Changed: `AuditAction` (+`JournalExported`), `AuditTargetType` (+`Course`),
  `AuditEvent` (six properties, `JournalExported` factory),
  `AuditEventConfiguration` (the six ignored until the migration),
  `ReportPageModel` (last optional `Export`), `ClassroomAgent.Infrastructure.csproj`
  (ClosedXML 0.105.1), `PermittedServiceWrites` (§4).
- Not added: DI registrations, a controller, a migration, views, scripts or
  translations.

The skeleton and two test files (the renderer and schema tests) were written by
delegated Sonnet agents from exact specifications. Their output was checked here
by build and run.

**ClosedXML 0.105.1 transitive packages** (for SECURITY_REVIEW, spec FR-011):
ClosedXML.Parser 2.0.0, DocumentFormat.OpenXml 3.1.1, DocumentFormat.OpenXml.Framework
3.1.1, ExcelNumberFormat 1.1.0, RBush.Signed 4.0.0, SixLabors.Fonts 1.0.0,
System.IO.Packaging 8.0.1. Licences are not checked at this stage.

## 4. Unexpected failure, resolved

`Architecture/ReadOnlyEnforcementTests.TheApplicationAssembly_HasNoUnprotectedWritePath`
failed on the skeleton's `ExportJournalCommand`. The command takes `IUnitOfWork`
without `IReadOnlyModeGuard`, which spec FR-009 requires. The US-007 rule demands
such a use case be declared in `PermittedServiceWrites`. The declaration
`[typeof(ExportJournalCommand)] = PermittedServiceWrite.AuditEvent` is part of
the skeleton: it is a declaration, it is what the design requires (BR-026: audit
rows), and it keeps the rule intact. After it the rule passes (§2).

## 5. New tests that pass before implementation

| Test | Why it is green now | Why it stays |
|---|---|---|
| `JournalExportAuthorizationTests.OutsideTheApi_AnonymousStillGoesToSignIn` | the existing redirect | guards the "outside `/api/v1` nothing changes" rule against the host-wide change of api-design §2.5 |
| `JournalExportTests.TheReportPage_OffersNoExport_WithoutAReport` | no action exists anywhere yet ("X did not happen" is free) | guards FR-002 against rendering the action unconditionally; its counterpart `TheReportPage_OffersTheExport_…` is red |
| `JournalExportTests.ViewingTheReport_WritesNoAuditRow` | nothing audits yet | guards AC-007 / FR-010 against auditing views; its counterparts `AnExport_WritesOneAuditRow_…` are red |

## 6. Red failures by cause

| Cause | Count |
|---|---|
| `NotImplementedException` from the skeleton | 125 |
| export route absent: `404` where `200` / `400` / `405` / `415` is expected, or no endpoint enumerated | 25 |
| export columns and constraints absent (no migration) | 14 |
| translation keys absent | 7 |
| `/api/v1` error answered as the HTML error page (content type, or a JSON parse of HTML) | 4 |
| export action and token meta tag absent from the report page | 1 |

None comes from a compile error, a fixture error or a wrong assertion: every
message was read and classified.

## 7. Coverage

Every Acceptance Criterion AC-001 … AC-011 maps to at least one red test
(`docs/tests/US-028-ac-test-matrix.md`). Every status code of
`exportJournalXlsx` has a test: 200, 400 ×3 causes, 401, 403 (restricted
session), 404, 405, 415. Exceptions: `403` for a policy denial has no actor to
test, because Admin and Dean are both allowed and no other role exists (TC-5
forbidden roles are anonymous and the restricted Dean). `500` is covered at the
use-case level (`AFailedRender_LeavesNoAuditRow`) and by the existing
exception-handler tests, not by an HTTP test.

Untested by design (strategy §6): the browser script's own behaviour.

## 8. Open Decisions

OD-005 raised and resolved (a) by the Owner on 2026-10-06. None open.

## 9. Findings for later stages

- **T-1** Entity model §3.4 omits `ServiceWriteScope` from the command. The skeleton's
  constructor takes it, as every audit-writing use case does: in read-only mode
  the commit must be declared `PermittedServiceWrite.AuditEvent`
  (`TheAuditRow_IsCommittedOnce_AsTheDeclaredAuditWrite`).
- **T-2** Key `Report.Export.Action` is fixed here for the button (spec FR-013
  left it open). The file's texts are not tied to key names.
- **T-3** `JournalExportAuditSchemaTests.ANonPositiveTemplateId_IsRejected` expects
  `ck_audit_event_export_template_id`. By the design's expressions it is the only
  constraint that fails for id −1 with `built_in = false`. This is confirmed only
  once the migration exists.
