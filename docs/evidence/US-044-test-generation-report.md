---
artifact_type: test_generation_report
story: US-044
version: 1
status: DRAFT
created_at: 2026-10-11T08:30:00Z
updated_at: 2026-10-11T08:30:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-044-spec.md
    version: 1
  - path: docs/decisions/US-044-open-decisions.md
    version: 2
  - path: docs/designs/api/US-044-api-design.md
    version: 1
  - path: docs/designs/api/US-044-openapi.yaml
    version: 1
  - path: docs/designs/database/US-044-db-design.md
    version: 1
  - path: docs/designs/database/US-044-entity-model.md
    version: 1
  - path: docs/tests/US-044-test-strategy.md
    version: 1
  - path: docs/tests/US-044-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-044 Test Generation Report

## 1. Result

Red phase verified. 253 new tests: **245 red** for missing implementation,
**8 green** as regression guards (§5). The existing suite passes, after two
registry tests were updated for the two new BR-026 declarations (§4). The 25
`Meet*` cases of the existing US-028 test
`JournalExportTranslationTests.EveryFileText_ResolvesInBothLanguages`, which
enumerates every `ReportText` member, are red for the missing translations as
well. They turn green with the implementation.

## 2. Commands

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --no-build` (Docker running, Testcontainers) | total 4 827, failed 273, passed 4 554, skipped 0; 5 m 01 s |
| `dotnet test tests/ClassroomAgent.Tests --no-build --filter "…MeetExport…\|…ExportMeet…\|MeetReportsExportTests\|MeetMeetingExportTests\|ClosedXmlReportRendererMeetTests"` | total 253, failed 245, passed 8 |
| after the registry fix: `--filter "MeetingCodeLinkRepositoryTests\|PermittedServiceWriteTests\|WorkspaceConnectionReadOnlyTests"` | total 36, failed 0 |
| full rerun after the fix | total 4 827, failed 270 (all new behaviour), passed 4 557 |

The 273 failures of the first full run are: 245 new, 25 `Meet*` cases of
`JournalExportTranslationTests`, 2 registry tests (fixed, §4) and 1
`MeetingCodeLinkRepositoryTests.AMark_RoundTrips`. That last test passes in
isolation and is unrelated to this Story (§4).

### 2.1 Full rerun

`dotnet test ClassroomAgent.sln --no-build`: total 4 827, failed 270, passed 4 557, skipped 0; 5 m 16 s. The 270 are exactly the 245 new red tests and the 25 `Meet*` cases of `JournalExportTranslationTests`. No existing test fails, and `AMark_RoundTrips` passed.

## 3. Files

**Created (tests):**

| File | Level | Tests |
|---|---|---|
| `Application/UseCases/MeetExportFileNameTests.cs` | Unit | 22 |
| `Application/UseCases/MeetExportAuditEventTests.cs` | Unit | 6 |
| `Application/UseCases/ExportMeetReportsCommandTests.cs` | Unit | 30 |
| `Application/UseCases/ExportMeetMeetingCommandTests.cs` | Unit | 19 |
| `Infrastructure/Export/ClosedXmlReportRendererMeetTests.cs` | Infra | 9 |
| `Infrastructure/Persistence/MeetExportAuditSchemaTests.cs` | Infra (PostgreSQL) | 18 |
| `Web/Pages/MeetReportsExportTests.cs` | Host | 30 |
| `Web/Pages/MeetMeetingExportTests.cs` | Host | 25 |
| `Web/Pages/MeetExportActionTests.cs` | Host | 10 |
| `Web/Security/MeetExportAuthorizationTests.cs` | Security | 26 |
| `Web/Logging/MeetExportLoggingTests.cs` | Logging | 4 |
| `Web/Localization/MeetExportTranslationTests.cs` | Localization | 54 |
| `TestInfrastructure/MeetExportWorld.cs` | fixture | — |
| `TestInfrastructure/MeetExportHostExtensions.cs` | fixture | — |

All paths are under `tests/ClassroomAgent.Tests/`.

**Modified (tests):** `Application/UseCases/PermittedServiceWriteTests.cs` and
`Web/UseCases/WorkspaceConnectionReadOnlyTests.cs`: the registry list and count
gain the two Meet export commands (§4).

**Production: compile-only skeleton (OD-014 a).** Nothing is registered in DI,
there is no migration and no route, and no existing behaviour changed.

- New files. Every member that would do work throws `NotImplementedException`:
  `Application/Models/Requests/MeetReportsExportRequest`,
  `MeetMeetingExportRequest`; `Application/Models/Dtos/MeetReportsExportAction`,
  `MeetMeetingExportAction`, `MeetExportField`, `MeetExportMessageKey`,
  `MeetExportFieldError`; `Application/Models/MeetExportOutcome`,
  `MeetExportResult`; `Application/UseCases/ExportMeetReportsCommand`,
  `ExportMeetMeetingCommand`, `MeetExportFileName`.
- Changed:
  - `AuditAction` gains the two actions.
  - `AuditEvent` gains `ExportMeetingId` and `ExportMeetings`, plus the two
    factories, which throw.
  - `AuditEventConfiguration` ignores the two properties until the migration and
    adds the two action-code arms.
  - `WorkbookCellKind` gains `Time` and `DateTime`.
  - `WorkbookCell` gains `Time`, `LocalDateTime`, `NumberFormat` and the factories
    `OfTime`, `OfDateTime`, `OfNumber(decimal, string?)`, which throw.
  - `Worksheet` gains `AutoFilter` and `TableLastRowIndex`.
  - `Workbook` gains `TimeFormat` and `DateTimeFormat`.
  - `ReportText` gains 25 `Meet*` members.
  - `MeetReportsPageModel` and `MeetReportMeetingPageModel` each gain a defaulted
    `Export` member, always null for now.
  - `PermittedServiceWrites` gains the two commands as `AuditEvent`, as US-028
    did with its skeleton.
- Where the skeleton names what the entity model left indicative: test strategy
  §6.

**Artifacts:**
- `docs/decisions/US-044-open-decisions.md` v2 adds OD-014.
- New: `docs/tests/US-044-test-strategy.md`, `docs/tests/US-044-ac-test-matrix.md`
  and this report.

**Delegation.** The test design, the skeleton, both fixtures, the strategy, the
matrix, the full-suite runs and the red-phase verdict were done inline. Three
`cheap-worker` agents wrote the classes from the matrix:
1. the unit, renderer and schema classes;
2. the two workbook host classes;
3. the action, authorization, logging and localization classes.

Their output was checked here by build and run. Their stated choices are taken
into §6.

## 4. Unexpected failures, resolved or explained

- `PermittedServiceWriteTests.TheRegistry_DeclaresOnlyWritesOnTheClosedList` and
  `WorkspaceConnectionReadOnlyTests.NoNewUseCase_IsRegisteredAsAPermittedServiceWrite`
  failed on the skeleton's registry entries.
  - The spec (FR-012) requires both commands to be declared as
    `PermittedServiceWrite.AuditEvent`, which is already on the closed list.
  - Both tests name or count the registry. They were updated as US-028, US-037
    and US-039 updated them, each with a comment naming US-044.
  - They now pass.
- `MeetingCodeLinkRepositoryTests.AMark_RoundTrips` (US-032) failed once in the
  full run (`Assert.NotNull`, line 103) and passed in isolation. It touches no
  type of this Story. See §2.1 for the rerun.

## 5. New tests that pass before implementation

| Test | Why it is green now | Why it stays |
|---|---|---|
| `ClosedXmlReportRendererMeetTests.AutoFilter_False_SetsNoFilter` | the renderer sets no autofilter | guards the opt-in rule (spec I-7) |
| `ClosedXmlReportRendererMeetTests.NumberCell_WithoutAFormat_StaysGeneral` | existing behaviour | guards the journal's number cells |
| `MeetReportsExportTests.TheJournalExport_IsUnchanged_ItsSheetsHaveNoAutofilter` | the journal sets no autofilter | AC-016: the journal workbook must not change |
| `MeetExportActionTests.TheReportsPage_WithoutAReport_OffersNoExport` ×3 | no action exists yet ("X did not happen" is free) | guards FR-002 against an unconditional action; its counterparts `TheReportsPage_Offers…` are red |
| `MeetExportActionTests.TheMeetingNotFoundPage_OffersNoExport` | as above | counterpart `TheMeetingPage_Offers…` is red |
| `MeetExportActionTests.ViewingThePages_WritesNothing` | nothing audits yet | guards FR-013 "viewing writes no audit row"; the export audit tests are red |

## 6. Red failures by cause

| Cause | Count |
|---|---|
| `NotImplementedException` from the skeleton (commands, factories, file name, cell factories) | 76 |
| expected `ArgumentOutOfRangeException` / `InvalidOperationException`, got `NotImplementedException` | 4 |
| export routes absent: `404` where `200`, `400`, `401`, `405` or `415` is expected, a `404` message other than the export's, or no endpoint enumerated | 78 |
| `Meet*` texts unresolved (`LocalizedReportTexts` has no arm), or `Export.Validation.MeetingMalformed` missing | 57 |
| `export_meetings` / `export_meeting_id` columns and the new CHECKs absent (no migration), in the schema tests and in three host tests that read the audit table | 21 |
| renderer ignores the new cell kinds and autofilter | 4 |
| export action, token meta tag and data attributes absent from the pages | 5 |

Total: 245.

Every failure message was read and classified. None comes from a compile error, a
fixture error or a wrong assertion.

Choices recorded by the delegated agents and accepted here:
- The log filter matches content, not a logger category. It assumes the
  orientation is written as the word `landscape` (spec FR-014 "the
  orientation").
- The `405` and no-token messages follow the journal tests (`Error.NotFound`,
  `Error.PageExpired`).
- The `415` asserts `Export.Validation.RequestMalformed`, per api-design §2.2.
- The CHECK-name expectations of the schema tests follow db-design §3.3.
  PostgreSQL reports the first violated CHECK in name order. A different but
  equivalent constraint split at IMPLEMENTATION is a test correction, not a
  design change.

## 7. Coverage

Every Acceptance Criterion AC-001 … AC-017 maps to at least one red test in
`docs/tests/US-044-ac-test-matrix.md`. Every status code of both operations
(200, 400 for each cause, 401, 403, 404 for each cause, 405, 415) has a test
asserting the code and the API-6 shape. The `500` on a render or audit failure
is covered at unit level: no file is returned (TC-3, as in US-028).

## 8. Open Decisions

OD-014 (compile-only skeleton) was raised and resolved (a) by the Owner on
2026-10-11. None is open.

## 9. Overall

PASS. Red phase verified, existing suite green (§2.1), no blocking issue.
