---
artifact_type: implementation_report
story: US-027
version: 1
status: DRAFT
created_at: 2026-10-04T21:40:00Z
updated_at: 2026-10-04T21:40:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-027-report-templates.md
    version: null
  - path: docs/specifications/US-027-spec.md
    version: 2
  - path: docs/decisions/US-027-open-decisions.md
    version: 3
  - path: docs/designs/api/US-027-api-design.md
    version: 1
  - path: docs/designs/api/US-027-openapi.yaml
    version: 1
  - path: docs/designs/database/US-027-db-design.md
    version: 1
  - path: docs/designs/database/US-027-entity-model.md
    version: 1
  - path: docs/tests/US-027-test-strategy.md
    version: 1
  - path: docs/tests/US-027-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-027-test-generation-report.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-027 Implementation Report — Report templates and the on-screen report

## 1. Summary

The section "Reports and templates" is implemented: the template list with the
built-in "Academic journal", the new / copy / change / delete forms and saves, and
the on-screen report (Grading and Lesson topics) built from a template, a course
and a period. `CourseWork.ScheduledTime` is stored and fills the lesson date.
The OD-005 (a) compile-only skeleton is completed; no `NotImplementedException`
remains.

- Build: 0 warnings, 0 errors. Tests: **3404 / 3404 passed, 0 skipped**.
  `dotnet format --verify-no-changes`: exit 0.
- Security-sensitive: yes — new authorization policies, read-only refusals with
  audit rows, new audit actions, rendering of personal data (report page).
- Work split (Owner instruction "рутину субам"): Domain, the reference parser,
  test-defect rulings and verification by the orchestrating session; persistence,
  template use cases, the report query and the Web layer by four Sonnet subagents
  from fixed briefs; every result re-verified by the full suite.

## 2. Source Artifacts

As listed in `inputs` above; all `DRAFT`/`APPROVED`, none `SUPERSEDED`.
`HUMAN_SPEC_APPROVAL` recorded 2026-10-04 (spec v2). OD-001 … OD-005 resolved.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests |
|---|---|---|
| AC-001 | `BuiltInReportTemplates.AcademicJournal`; `ListReportTemplatesQuery`; built-in key refused in `SaveReportTemplateUseCase` / `DeleteReportTemplateUseCase` / `GetReportTemplateFormQuery.EditAsync` | `ReportTemplateInvariantTests`, `ListReportTemplatesQueryTests`, `ReportTemplateSaveTests`, `ReportTemplateDeleteTests`, `ReportTemplatePagesTests.TheBuiltIn_CannotBeEditedChangedOrDeleted_OverHttp` |
| AC-002 | `ReportTemplate.Create/Change`; save / delete use cases; `ReportTemplatesController` | `ReportTemplateSaveTests`, `ReportTemplateDeleteTests`, `ReportTemplateFormQueryTests`, `ReportTemplatePagesTests` |
| AC-003 | `ReportTemplateFormValidator` (VR-004); `GetReportQuery` scale conversion | `ReportTemplateFormValidationTests`, `ReportCellTests` |
| AC-004 | `GetReportQuery`, `JournalFieldSource` | `ReportContentTests`, `ReportPageTests`, `JournalFieldSourceTests` |
| AC-005 | settings → `ReportCell` resolution in `GetReportQuery` | `ReportCellTests`, `ReportPageTests.ACreatedTemplate_ShowsTheMaterials_WhenItDoesNotHideThem` |
| AC-006 | `ReportTemplateRefusalAudit` (guard first) | `ReportTemplateSaveTests`, `ReportTemplateDeleteTests`, `ReportTemplateReadOnlyTests`, `ReportTemplatePagesTests.InReadOnlyMode_ASaveIs409_AndNothingIsStored` |
| AC-007 | `AuditEvent.ReportTemplateWritten/WriteRefused`; audit check `ck_audit_event_report_template_shape` | `ReportTemplateSaveTests`, `ReportTemplateDeleteTests`, `ReportTemplateSchemaTests` |
| AC-008 | policies `UseReportTemplates`, `EditReportTemplates` | `ReportTemplateAuthorizationTests` |
| AC-009 | `ReportTemplateFormReader` / `ReportTemplateFormValidator`; `ReportTemplateLog` | `ReportTemplateFormValidationTests`, `ReportRequestValidationTests`, `ReportTemplateLoggingTests` |
| AC-010 | 84 keys in both resx files; Razor encoding | `ReportTemplateTranslationTests`, `ReportPageTests.TheReport_RendersInEnglish_AndLeavesSchoolAndGoogleTextAsWritten`, `ReportTemplatePagesTests.ATemplateName_IsHtmlEncoded` |
| AC-011 | no Google port in any constructor; synthetic data | `ReportTemplateInvariantTests.NoUseCaseOfThisStory_TakesAGooglePort`, `ReportTemplateReadOnlyTests` |
| AC-012 | `JournalFieldSource` lesson date `ScheduledTime ?? CreationTime ?? ItemDate`; `CourseWork.ScheduledTime`; `GoogleClassroomReader` | `JournalFieldSourceTests`, `ReportContentTests`, `CourseWorkScheduledTimeTests`, `GoogleClassroomReaderTests` |
| AC-013 | rounding `MidpointRounding.AwayFromZero`, clamp 0–100 | `ReportCellTests` |
| AC-014 | T1 left join on `author_id`, null email → `AccountDeleted` | `ListReportTemplatesQueryTests`, `ReportTemplateRepositoryTests` |

## 4. Change Set

### Domain
- `Entities/ReportTemplate.cs`, `ReportTemplateMark.cs`, `ReportTemplateScaleRow.cs` — entity model §2; invariants of §2.1.
- `Entities/CourseWork.cs`, `CourseWorkDetails.cs` — FR-020, entity model §3.1 (`ScheduledTime` stays an optional last parameter, as the skeleton left it).
- `Entities/AuditEvent.cs` — factories `ReportTemplateWritten` / `ReportTemplateWriteRefused` (FR-016, db-design §4; target type always `report_template`).
- `Enums/Report*.cs`, `AuditAction.cs`, `AuditTargetType.cs` — entity model §1.1, db-design §4.
- `Rules/BuiltInReportTemplates.cs`, `TwelvePointScale.cs`, `ReportMark.cs`, `ReportLateMark.cs`, `ReportScaleRow.cs`, `ReportTemplateSettings.cs` — FR-006, FR-015, OD-002, OD-003.

### Application
- `UseCases/ListReportTemplatesQuery.cs` — FR-002, FR-014.
- `UseCases/GetReportTemplateFormQuery.cs` — FR-007 … FR-010, api-design §2.3, §2.4.
- `UseCases/SaveReportTemplateUseCase.cs`, `DeleteReportTemplateUseCase.cs` — FR-007 … FR-010, FR-013, FR-016, api-design §2.6, db-design §2.4.
- `UseCases/ReportTemplateRefusalAudit.cs` (internal) — FR-013, api-design §2.6 target-id rule.
- `UseCases/ReportTemplateFormMapper.cs` (internal) — settings ↔ form values, FR-007 defaults.
- `UseCases/ReportTemplateReference.cs` — VR-006, api-design §2.2.
- `UseCases/GetReportQuery.cs` — FR-004, FR-005, FR-011, FR-015, VR-007.
- `UseCases/JournalCellRule.cs`, `JournalQueryRules.cs` (internal), `GetJournalQuery.cs` — FR-004 "one cell computation": the US-025 cell function and query helpers moved verbatim into shared helpers; journal behaviour unchanged (182 journal tests green).
- `Validation/ReportTemplateFormReader.cs`, `ParsedReportTemplateForm.cs`, `ReportTemplateFormValidator.cs` (internal) — api-design §2.7, VR-001 … VR-005.
- `Authorization/InstallationPolicies.cs` — FR-012.
- `Ports/IJournalFieldSource.cs`, `IReportTemplateRepository.cs`; `Models/*` and `Models/Dtos/*` of §2 of the test-generation report; `Exceptions/UniqueReportTemplateNameViolationException.cs` — entity model §4, openapi.
- `Localization/SharedResource.uk.resx`, `.en.resx` — FR-017 (84 keys, including `ReportTemplate.Copy.Suffix` " (копія)" / " (copy)", F-1).

### Infrastructure
- `Persistence/Configurations/ReportTemplate*Configuration.cs` (3 new), `CourseWorkConfiguration.cs`, `AuditEventConfiguration.cs` — db-design §2, §3, §4.
- `Persistence/ClassroomAgentDbContext.cs` — the new set and configurations.
- `Persistence/Migrations/20261004202609_ReportTemplates.cs`, `.Designer.cs`, `ClassroomAgentDbContextModelSnapshot.cs` — PC-2, db-design §7 (generated).
- `Persistence/UnitOfWork.cs` — `uq_report_template_normalized_name` → `UniqueReportTemplateNameViolationException` (db-design §2.1).
- `Persistence/TimestampInterceptor.cs` — stamps the three template entities (PC-6, db-design §2.4).
- `Persistence/Repositories/ReportTemplateRepository.cs` (T1–T3), `JournalFieldSource.cs` (F1–F4) — db-design §5.
- `Google/GoogleClassroomReader.cs` — passes `scheduledTime` for both resources (FR-020; nothing new requested).

### Web
- `Controllers/ReportTemplatesController.cs`, `ReportController.cs` — api-design §1–§4.
- `Views/ReportTemplates/{Index,Form,Delete}.cshtml`, `Views/Report/Index.cshtml`, `Views/Home/Index.cshtml` — FR-001, FR-002, FR-005, FR-011, VR-009.
- `wwwroot/js/report-template-scale.js` — api-design §2.8.
- `Security/ReportTemplateLog.cs`, `ReportTemplateTextKeys.cs`, `SignInRoutes.cs`, `InstallationSecurityServices.cs` — FR-019, §2.9, §2.1, §2.10.
- `Configuration/InstallationServices.cs` — DI of the two ports and five use cases.

### Tests (changed after TEST_WRITING — see §7)
- `Application/UseCases/ReportTemplateSaveTests.cs`, `ListReportTemplatesQueryTests.cs` — two defects corrected, Owner ruling.
- `Infrastructure/Persistence/AppUserMigrationTests.cs`, `DeanAccountSchemaTests.cs`, `InstallationAuditEventSchemaTests.cs`, `Web/Persistence/AccessCheckAuditSchemaTests.cs`, `Web/UseCases/AdminLoginCheckEveryTimeTests.cs` — hard-coded table / check / migration lists extended by this Story's design (as every earlier Story did).
- The US-027 test files of TEST_WRITING (unchanged otherwise).

No secret, generated database file, `.xlsx` or IDE-local file in the change set.
The Python prototype is untouched.

## 5. Validation Evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln` (Docker running) | 0 | 3404 total, 3404 passed, 0 failed, 0 skipped, 3 m 16 s |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | no changes |

Per-layer runs during implementation: Domain invariants 33/33; report query
99/99 plus journal 182/182; template use cases 152/152 after the test
corrections; persistence filter 387/387 after the list updates; Web 1342/1342.

## 6. Configuration Changes

None. No `appsettings` key, no package, no Control Plane change.

## 7. Deviations and Discovered Problems

- **D-1 Test defect — create commits (Owner ruling 2026-10-04: fix the test).**
  `ReportTemplateSaveTests.AValidForm_CreatesTheTemplate_WithTheActorAsAuthor`
  expected one commit; db-design §2.4 requires two saves in one transaction (the
  audit row needs the generated id). Assertion changed to 2 commits,
  `Staged == [(1,0),(1,1)]`; still one transaction.
- **D-2 Test defect — fixture author emails (same ruling).**
  `ListReportTemplatesQueryTests.CreatedTemplates_AreOrderedByName_ThenId_WithTheirAuthor`
  seeded five templates under one author id while the fixture keeps one email per
  author; each now gets its own author id (the tie pair shares one).
- **D-3 Stale schema lists.** Five pre-existing tests enumerate every table /
  every `ck_audit_event_*` check / the migration count; extended with the three
  template tables, `ck_audit_event_report_template_shape` and the 11th migration.
- **D-4 Refused audit target type.** A subagent first wrote a refused row without
  target type when the id is null; that violates `ck_audit_event_report_template_shape`
  (db-design §4). Corrected: the target type is always `report_template`.
- **D-5 `TimestampInterceptor`** stamps an explicit list of entity types; the three
  template entities were added (db-design §2.4 relies on it).
- **D-6 `AuditEvent` factories** were not in the skeleton; added in Domain (the
  entity's constructor is private, the established pattern).
- **D-7 Report edge cases chosen in implementation** (not fixed by the spec):
  members are read even when no lesson is in the period (the header needs the
  teachers); a period whose only items are materials hidden by the template shows
  Grading with no columns rather than "nothing published"; a malformed `template`
  falls back to the built-in in the drop-down with `TemplateMalformed`.
- **D-8 Scale error text.** Field-error sentences carry no placeholder; a scale
  error is rendered as the prefix "Рядок {0}:" / "Row {0}:" plus the sentence.

## 8. Open Decisions

None touched or newly required. No security-sensitive decision is open.
Carried non-blocking findings for `HUMAN_PR_APPROVAL`: spec F-1 (AGENTS.md
Domain Essentials still marked v80), db-design N-1 … N-4, test-generation F-1 …
F-5, and D-7 above.
