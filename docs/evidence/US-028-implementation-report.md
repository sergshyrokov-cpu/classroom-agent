---
artifact_type: implementation_report
story: US-028
version: 1
status: DRAFT
created_at: 2026-10-06T09:35:00Z
updated_at: 2026-10-06T09:35:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-028-excel-export.md
    version: null
  - path: docs/specifications/US-028-spec.md
    version: 1
  - path: docs/decisions/US-028-open-decisions.md
    version: 2
  - path: docs/designs/api/US-028-api-design.md
    version: 1
  - path: docs/designs/api/US-028-openapi.yaml
    version: 1
  - path: docs/designs/database/US-028-db-design.md
    version: 1
  - path: docs/designs/database/US-028-entity-model.md
    version: 1
  - path: docs/tests/US-028-test-strategy.md
    version: 1
  - path: docs/tests/US-028-ac-test-matrix.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-028 Implementation Report — Export a journal to Excel

## 1. Summary

The report page offers "Export to Excel" with a portrait/landscape choice. The
page's script posts `POST /api/v1/exports/journal-xlsx` (JSON, antiforgery
header); the installation validates with the report page's own rules, builds
the page's own report, maps it to the workbook model, renders `.xlsx` in memory
with ClosedXML, commits one `journal_exported` audit row and returns the file.
Every error under `/api/v1` is now the API-6 JSON body (api-design §2.5).

Status: every Acceptance Criterion implemented. Build 0 warnings / 0 errors;
3 776 tests, 0 failed, 0 skipped; `dotnet format --verify-no-changes` clean.
Security-sensitive: a host-wide change to `/api/v1` error answers, a new
POST endpoint, a new audit action, a new NuGet package (ClosedXML, OD-001).

## 2. Source Artifacts

As in the front matter. Story authored (no version); specification v1
(APPROVED at `HUMAN_SPEC_APPROVAL`); Open Decisions v2 (OD-001 … OD-005 all
resolved); designs and test artifacts v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (all green) |
|---|---|---|
| AC-001 | `ExportJournalCommand.ExecuteAsync`; `ReportBuilder.BuildAsync` (shared with `GetReportQuery`); `ReportWorkbookMapper.Map`; `ClosedXmlReportRenderer.RenderAsync` | `JournalExportTests.AnAdmin_ExportsTheBuiltInJournal_AsAnXlsxAttachment`, `TheFile_ShowsWhatTheReportPageShows`; `ExportJournalCommandTests.TheWorkbook_IsTheMappedReportOfThePage_ForTheSameInputs`; `ReportWorkbookMapperTests`; `ClosedXmlReportRendererTests` |
| AC-002 | `ReportBuilder` (template settings, as on screen); `ReportWorkbookMapper.GradingCell` | `JournalExportTests.ACreatedTemplateHidingMaterials_LeavesTheMaterialOut`; `ReportWorkbookMapperTests` marks and full view |
| AC-003 | `ReportWorkbookMapper.GradingCell` + `WholeNumber` (OD-003 a); `WorkbookCell` factories; renderer cell kinds | `ReportWorkbookMapperTests.AWholeNumberGradeAlone_IsANumber`, `AnyOtherLabel_IsText_AsWritten`, `RawPoints_AreText_…`; renderer kind tests |
| AC-004 | `IReportTexts` → `LocalizedReportTexts` (Web); culture from the session | `JournalExportTests.AnEnglishUser_…`; `JournalExportTranslationTests`; `ExportJournalCommandTests.ProgramText_IsTakenFromTheTextPort` |
| AC-005 | `JournalExportFileName.Build`; `JournalExportController` `Content-Disposition` via `SetHttpFileName` | `JournalExportFileNameTests`; `JournalExportTests.TheFileName_IsTheCourseAndPeriod_AndCarriesNoPerson` |
| AC-006 | `ExportJournalCommand` (no guard; commit under `ServiceWriteScope.Declare(AuditEvent)`); `PermittedServiceWrites` | `JournalExportTests.InReadOnlyMode_…` (3); `ExportJournalCommandTests.InReadOnlyMode_…` |
| AC-007 | `AuditEvent.JournalExported`; `AuditEventConfiguration`; migration `AddJournalExportAudit` | `JournalExportTests.AnExport_WritesOneAuditRow_WithoutPersonalData`, `ViewingTheReport_WritesNoAuditRow`; `JournalExportedAuditEventTests`; `JournalExportAuditSchemaTests` |
| AC-008 | `[Authorize(UseReportTemplates)]` + `[HttpPost]` on `JournalExportController`; `/api/v1` rules in `InstallationSecurityServices` (cookie events), `TemporaryPasswordMiddleware`, `GlobalAntiforgeryFilter`, `ErrorController`, `Program` fallback (405) | `JournalExportAuthorizationTests` (9); `InstallationCookieTests.EveryStateChangingPublicEndpoint_WithoutToken_Returns400` |
| AC-009 | `ReportRequestReader.Read(required: true)`; orientation check; controller body syntax and 415; `JournalExportLog.Refused` | `ExportJournalCommandTests` validation and not-found; `JournalExportTests` 400/404/415; `JournalExportLoggingTests` |
| AC-010 | host-wide `NoStoreMiddleware` (US-040), unchanged | `JournalExportTests.AnAdmin_ExportsTheBuiltInJournal_…` |
| AC-011 | Testcontainers, substituted ports, workbook read from response bytes | all the above |

## 4. Change Set

Production — new:

| File | Trace |
|---|---|
| `Application/Validation/ReportRequestReader.cs`, `ReportSelection.cs` | entity model §3.3 — shared shape rules (FR-003, VR-001) |
| `Application/UseCases/ReportBuilder.cs`, `ReportBuildResult.cs` | entity model §3.3 — one source of content (FR-003) |
| `Application/Models/JournalExportSummary.cs` | FR-012 log facts — deviation D-1 (§7) |
| `Infrastructure/Persistence/Migrations/20261006062229_AddJournalExportAudit*.cs` | db-design §4 (PC-2) |
| `Web/Controllers/JournalExportController.cs` | openapi `exportJournalXlsx`; api-design §2.1–2.8 |
| `Web/Security/ApiErrorResponse.cs` | api-design §2.5 — API-6 body for every `/api/v1` error |
| `Web/Security/JournalExportLog.cs` | FR-012 |
| `Web/Security/JournalExportTextKeys.cs` | FR-013 key constants |
| `Web/wwwroot/js/report-export.js` | api-design §2.6 |

Production — changed:

| File | Trace |
|---|---|
| skeleton files of OD-005 (`WorkbookCell`, `Workbook`, `Worksheet`, `WorksheetRow`, `JournalExportFileName`, `ReportWorkbookMapper`, `ExportJournalCommand`, `AuditEvent`, `ClosedXmlReportRenderer`, `LocalizedReportTexts`) | bodies implemented per entity model §1–3 |
| `Application/UseCases/GetReportQuery.cs` | entity model §3.3 split; sets `ExportAction` (FR-002) |
| `Application/Models/JournalExportResult.cs` | `Summary` member — deviation D-1 |
| `Infrastructure/.../AuditEventConfiguration.cs`, `ClassroomAgentDbContextModelSnapshot.cs` | db-design §3 |
| `Application/Localization/SharedResource.uk.resx`, `.en.resx` | FR-013, api-design §5 (13 keys each) |
| `Web/Views/Report/Index.cshtml` | FR-002, api-design §2.6 |
| `Web/Configuration/InstallationServices.cs` | DI: command, renderer, text port |
| `Web/Program.cs` | api-design §2.5 rule (5): 405 under `/api/v1` |
| `Web/Security/ErrorController.cs`, `GlobalAntiforgeryFilter.cs`, `InstallationSecurityServices.cs`, `TemporaryPasswordMiddleware.cs` | api-design §2.5 rules (1)–(6) |

Tests — changed (no assertion weakened):

| File | Trace |
|---|---|
| `AppUserMigrationTests`, `InstallationAuditEventSchemaTests`, `AccessCheckAuditSchemaTests`, `DeanAccountAuditSchemaTests`, `NamePartsPersistenceTests` | schema snapshots follow the new migration (db-design §3–4), as US-019/027/037 did; `AHalfSetTarget_IsRejected` example moved from `course` (now legal) to `meeting` |
| `PermittedServiceWriteTests`, `WorkspaceConnectionReadOnlyTests` | registry now holds `ExportJournalCommand` (declared at TEST_WRITING, OD-005) |
| `TestInfrastructure/JournalExportHostExtensions.cs` | finding T-4 (§7) |
| `Web/Pages/JournalExportTests.cs` | finding T-5 (§7), decided by the Owner |

Not in the Story's scope, to be committed separately: `.claude/skills/dotnet-implementor/SKILL.md`
and `.claude/skills/test-writer/SKILL.md` (a "Delegation" section, requested by
the Owner during this stage). No secret, database file, `.xlsx` or IDE file is
in the change set.

## 5. Validation Evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --no-build` | 0 | 3 776 total, 0 failed, 3 776 passed, 0 skipped; 3 m 31 s |
| `dotnet format ClassroomAgent.sln --verify-no-changes --verbosity minimal` | 0 | no output |

Incremental runs along the way: unit classes of the mapper, renderer, file
name and audit factory (68/68); report and export classes (581/583, the two
then-missing Web pieces); schema classes (330/330 after the snapshot update);
translation classes (351/351); a first full run with 29 failures, all
explained in §7 and fixed.

Delegated (cheap-worker): every full and filtered build/test/format run after
the first two; translation entries and `LocalizedReportTexts`; the view,
`report-export.js` and DI registration; the schema/registry snapshot updates
and the fixture fix. Kept inline: Application and Domain code, the migration,
the controller and the `/api/v1` error mapping, the test findings, this report.
Delegation started late in the stage — the Owner had to ask for it.

## 6. Configuration Changes

None in `appsettings*`. Package: ClosedXML 0.105.1 in
`ClassroomAgent.Infrastructure.csproj` only (OD-001; added with the skeleton).
Transitive: ClosedXML.Parser 2.0.0, DocumentFormat.OpenXml 3.1.1,
DocumentFormat.OpenXml.Framework 3.1.1, ExcelNumberFormat 1.1.0,
RBush.Signed 4.0.0, SixLabors.Fonts 1.0.0, System.IO.Packaging 8.0.1 —
licences for SECURITY_REVIEW (spec FR-011, S-12).

## 7. Deviations and Discovered Problems

- **D-1 `JournalExportResult.Summary`.** The entity model fixes the result as
  outcome, file, errors. The success log of FR-012 needs the effective name
  source, its origin and the row/topic counts, which only the command knows.
  An optional `JournalExportSummary` (ids, dates, codes, counts — no personal
  data) was added rather than re-parsing the request in the controller.
- **D-2 415 in the controller.** openapi says `415` is checked after
  antiforgery; `[Consumes]` is not used because its place in the pipeline does
  not guarantee that order. The controller checks `HasJsonContentType()` first.
- **D-3 405 under `/api/v1`.** The SC-4 catch-all fallback matches a wrong-method
  request before routing can answer `405`. The fallback now answers `405` with
  `Allow` when a literal `/api/v1` route serves the path with another method;
  everything else is unchanged.
- **D-4 JSON encoder.** The API-6 body writes Cyrillic unescaped
  (`JavaScriptEncoder.Create(UnicodeRanges.All)`, HTML-sensitive characters
  still escaped); the existing `409` body of `InstallationExceptionHandler` is
  unchanged.
- **D-5 `ReportBuilder` reads the clock itself.** `GetReportQuery` used one
  instant for the date defaults and the build; now the defaults and the build
  read it separately. Behaviour unchanged (existing report tests green).
- **T-4 (test fixture).** `JournalExportHostExtensions.ExportAsync` reused a
  token taken from the sign-in page before signing in, which belongs to the
  anonymous user and is rejected after sign-in (all but one export test got
  `400`). It now always takes a fresh token from the report page, as the
  browser does. No assertion changed.
- **T-5 (test expectation, Owner decision 1a).** `TheFile_ShowsWhatTheReportPageShows`
  and `AnEnglishUser_…` expected `SeededJournal.StudentName` ("Test Student One")
  in the file. The seeded participant has no name parts, so the report — screen
  and file alike — shows the email local part `student.one` (US-042 FR-003).
  The two assertions now expect `student.one`; the file-equals-screen check of
  the test was already passing.
- **For SECURITY_REVIEW:** the host-wide `/api/v1` change (api-design §2.5);
  `TemporaryPasswordMiddleware` resolves the message culture from the account
  claim / school default because it runs before request localization.

## 8. Open Decisions

None touched or newly required. OD-001 … OD-005 resolved.
