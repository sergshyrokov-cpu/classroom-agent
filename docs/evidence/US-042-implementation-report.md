---
artifact_type: implementation_report
story: US-042
version: 1
status: DRAFT
created_at: 2026-10-05T13:20:00Z
updated_at: 2026-10-05T13:20:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-042-names-in-report.md
    version: null
  - path: docs/specifications/US-042-spec.md
    version: 1
  - path: docs/decisions/US-042-open-decisions.md
    version: 2
  - path: docs/designs/api/US-042-api-design.md
    version: 1
  - path: docs/designs/api/US-042-openapi.yaml
    version: 1
  - path: docs/designs/database/US-042-db-design.md
    version: 1
  - path: docs/designs/database/US-042-entity-model.md
    version: 1
  - path: docs/tests/US-042-test-strategy.md
    version: 1
  - path: docs/tests/US-042-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-042-test-generation-report.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-042 Implementation Report — Names of students and teachers in a template report

## 1. Summary

Implemented: surname and given name of a participant synchronized from the
Google roster profile and stored; the template setting "names"
(`profile`/`email`) on the form, in the domain and in the database; one FR-003
name rule for every person of the template report; ordering by the shown name;
the `names` page parameter, the two-link switch and the effective source in the
report DTO and the `ReportBuilt` log line. The OD-001 compile-only skeleton is
fully replaced — no `NotImplementedException` remains.

Validation: build 0 warnings / 0 errors; **3597 tests, 3597 passed, 0 failed,
0 skipped**; `dotnet format --verify-no-changes` clean.

Security-sensitive: yes — new personal data columns (surname, given name) and a
new query parameter. No new endpoint, policy, Google scope or outbound
destination; nothing personal is logged, audited or put into an address.

Limitation: the switch's layout at phone width (NFR-070) is not automated — for
the human at HUMAN_PR_APPROVAL (test-generation report §6).

## 2. Source Artifacts

As in the front matter `inputs`. Requirements: `trebovaniya.md` v86.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `ClassroomParticipant.Import`/`UpdateFrom`/`Apply` (Surname, GivenName; replace, trim, blank→null, cut 750); `GoogleClassroomReader.Entry` (FamilyName/GivenName); `RunSynchronizationUseCase.ResolveParticipantsAsync`; `ClassroomParticipantConfiguration` | `ParticipantNamePartsTests`, `NamePartsPersistenceTests`, `GoogleClassroomReaderTests` (new case), `ClassroomParticipantSchemaTests` | PASS |
| AC-002, AC-003, AC-013 | `ReportPersonNameRule.Label`; `GetReportQuery` (teachers, Grading rows) | `ReportPersonNameTests`, `ReportContentTests`, `ReportPageTests` | PASS |
| AC-004 | `GetReportQuery`: named by `Label` in UI collation, ties by id, `Unnamed` last | `ReportPersonNameTests` (ordering cases) | PASS |
| AC-005 | `ReportTemplate.NameSource`, `ReportTemplateSettings.NameSource`, `BuiltInReportTemplates.AcademicJournal` = Profile, `ReportTemplateFormMapper` (default/copy/edit), migration fills `profile` | `ReportTemplateNameSourceTests`, `NamePartsPersistenceTests`, `ReportTemplateSchemaTests` | PASS |
| AC-006 | `GetReportQuery` (VR-002 parse, effective source, `NameSourceSwitch`, `ReturnPath(…, names)`); `ReportController` binds `names`; `Views/Report/Index.cshtml` switch | `ReportNameSourceTests`, `ReportNameSourcePageTests` | PASS |
| AC-007 | Report path has no guard and no write; save path unchanged (guard first) | `ReportNameSourceTests.InReadOnlyMode_TheSwitchWorks_WhileASaveIsRefused`, `ReportTemplateNameSourceTests` | PASS |
| AC-008 | Unchanged `SaveReportTemplateUseCase` audit (template id only) with the new setting | `ReportTemplateNameSourceTests` | PASS |
| AC-009 | No new endpoint; existing policies | `ReportNameSourceAuthorizationTests` | PASS |
| AC-010 | `NameSourceCode.TryParseSingle`; `ReportTemplateFormReader`/`Validator` (`NameSourceInvalid` field error, `Names` empty); `ReportMessageKey.NameSourceMalformed` last; logs name the key only | `ReportNameSourceTests`, `ReportTemplateNameSourceTests`, `ReportNameSourcePageTests`, `ReportNameSourceLoggingTests` | PASS |
| AC-011 | Six new keys in both resx files; `ReportTemplateTextKeys`; names rendered as stored, HTML-encoded by Razor | `ReportNameSourceTranslationTests`, `ReportTemplateTranslationTests` | PASS |
| AC-012 | Testcontainers PostgreSQL; Google port faked | whole suite | PASS |
| AC-014 | `ReturnPath` carries `names` only when the address had a valid one | `ReportNameSourceTests.TheReturnPath_*`, `ReportNameSourcePageTests` | PASS |

## 4. Change Set

Production (`src/`):

| File | Trace |
|---|---|
| `Domain/Entities/ClassroomParticipant.cs` | FR-001, VR-003, entity model §1.2 |
| `Domain/Entities/ReportTemplate.cs` | FR-002, entity model §1.4 |
| `Domain/Rules/ReportTemplateSettings.cs` | entity model §1.3 (`NameSource` now required) |
| `Domain/Rules/BuiltInReportTemplates.cs` | FR-002, D-7 |
| `Application/Models/JournalCourseMemberRecord.cs` | FR-006, entity model §3.2 (`FullName` → `Surname`, `GivenName`) |
| `Application/Models/Dtos/Report.cs` | FR-006, openapi `Report.nameSource`/`nameSourceOrigin` |
| `Application/Models/Dtos/ReportPageModel.cs` | FR-005, openapi `nameSwitch` |
| `Application/Models/Dtos/ReportTemplateFormValues.cs` | FR-002, openapi `ReportTemplateForm.names` |
| `Application/UseCases/ReportPersonNameRule.cs` (new) | FR-003, entity model §3.3 |
| `Application/UseCases/GetReportQuery.cs` | FR-003 … FR-006, VR-002; binding note 1 (skeleton removed) |
| `Application/UseCases/ReportTemplateFormMapper.cs` | FR-002 (default, copy, edit values) |
| `Application/UseCases/RunSynchronizationUseCase.cs` | FR-001 (upsert writes the parts) |
| `Application/Validation/NameSourceCode.cs` (new) | api-design §2.1 — one parser for form and query |
| `Application/Validation/ParsedReportTemplateForm.cs`, `ReportTemplateFormReader.cs`, `ReportTemplateFormValidator.cs` | VR-001, api-design §2.3 |
| `Application/Localization/SharedResource.uk.resx`, `.en.resx` | FR-010 (supporting: translation entries) |
| `Infrastructure/Google/GoogleClassroomReader.cs` | entity model §3.1 |
| `Infrastructure/Persistence/Configurations/ClassroomParticipantConfiguration.cs` | db-design §2 |
| `Infrastructure/Persistence/Configurations/ReportTemplateConfiguration.cs` | db-design §3, D-4 |
| `Infrastructure/Persistence/Repositories/JournalFieldSource.cs` | db-design §5 (F3 selects the parts) |
| `Infrastructure/Persistence/Migrations/20261005125917_ParticipantNamePartsAndTemplateNameSource.cs`, `.Designer.cs`, `ClassroomAgentDbContextModelSnapshot.cs` | db-design §4 (supporting: migration) |
| `Web/Controllers/ReportController.cs` | FR-005 (binds `names`), FR-011 |
| `Web/Security/ReportTemplateLog.cs` | FR-011, binding note 5 |
| `Web/Security/ReportTemplateTextKeys.cs` | FR-010 |
| `Web/Views/Report/Index.cshtml` | FR-005, api-design §2.2 |
| `Web/Views/ReportTemplates/Form.cshtml` | FR-002, binding note 4 |

Tests (`tests/ClassroomAgent.Tests/`):

| File | Trace |
|---|---|
| `TestInfrastructure/FakeJournalFieldSource.cs` | binding note 2 (`FullName: null,` removed) |
| `Infrastructure/Persistence/AppUserMigrationTests.cs` | D-1 below |
| `Infrastructure/Persistence/NamePartsPersistenceTests.cs` | D-2 below |
| `Application/UseCases/ReportPersonNameTests.cs` | D-3 below |

`RosterEntry.cs` and `ReportRequest.cs` keep the skeleton's shape (committed in
`4b1eef4`). No secret, generated database file, `.xlsx` or IDE-local config is
in the change set.

## 5. Validation Evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | exit 0; 0 warnings, 0 errors |
| Tests, first run | `dotnet test ClassroomAgent.sln` | exit 2; 3597 total, 3594 passed, 3 failed (D-1 … D-3) |
| Tests, final | `dotnet test ClassroomAgent.sln` | exit 0; **3597 total, 3597 passed, 0 failed, 0 skipped**, 3 m 25 s |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0 |
| Migration | `dotnet ef migrations add ParticipantNamePartsAndTemplateNameSource --project src/ClassroomAgent.Infrastructure --startup-project src/ClassroomAgent.Web` | generated; adjusted per db-design §4 |
| Markers | search `TODO|TBD|FIXME|NotImplementedException|US-042 skeleton` in `src/` | none |

Baseline: the test-generation report records 115 expected red / no unrelated
failure at `4b1eef4`; it was not re-run before the first edit.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

Test defects, fixed with the Owner's ruling of 2026-10-05 in this conversation
("Исправить тесты"), as US-027 D-1 … D-3:

- **D-1 Stale migration count.** `AppUserMigrationTests` asserted 11 migrations;
  db-design §4 adds the 12th. Count set to 12, comment extended.
- **D-2 Test context without production options.**
  `NamePartsPersistenceTests.Context` built a `DbContext` with bare `UseNpgsql`,
  so EF used PascalCase columns (`42703: column "CreatedAt"`). It now uses
  `ClassroomAgentDbContextOptions.Configure`, as `JournalFieldSourceTests` does.
- **D-3 Collation expectation.** `ReportPersonNameTests.Teachers_AreNamedAndOrderedByTheSameRule`
  expected Latin `aa.teacher` before Cyrillic names under `uk-UA`. The
  Ukrainian collation required by spec FR-006 (the US-025/US-027 comparer,
  unchanged) orders Cyrillic first; the expectation now follows it.

Design-signature differences (indicative signatures, entity model §1.2/§3.1):

- `ClassroomParticipant.Import` takes `surname`/`givenName` as optional
  parameters instead of a separate overload; the submitter-only import keeps
  passing three arguments (nulls). The two-argument `UpdateFrom` is removed —
  only the four-argument form exists, so a caller cannot forget the parts.
- `RosterEntry` keeps the skeleton's optional `Surname`/`GivenName` (existing
  tests construct it with three arguments).
- `NameSourceCode` is public so the Razor views render the codes they post.

## 8. Open Decisions

None touched or newly required. OD-001 resolved (a) earlier.
