---
artifact_type: implementation_report
story: US-025
version: 1
status: DRAFT
created_at: 2026-10-04T13:40:00Z
updated_at: 2026-10-04T13:40:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-025-journal-view-for-period.md
    version: null
  - path: docs/specifications/US-025-spec.md
    version: 1
  - path: docs/decisions/US-025-open-decisions.md
    version: 4
  - path: docs/designs/api/US-025-api-design.md
    version: 1
  - path: docs/designs/api/US-025-openapi.yaml
    version: 1
  - path: docs/designs/database/US-025-db-design.md
    version: 1
  - path: docs/designs/database/US-025-entity-model.md
    version: 1
  - path: docs/tests/US-025-test-strategy.md
    version: 1
  - path: docs/tests/US-025-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-025-test-generation-report.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-025 Implementation Report — Journal view for a period

## 1. Summary

The journal page `GET /workspace/journal` is implemented for Admin and Dean: the
course drop-down, the period in the school's time zone, the full / short view,
the columns, rows and cells of spec FR-004 … FR-006, the `400` / `404` answers
that re-render the page, the empty states, the home page link, the required
`Installation:TimeZone` setting (with the OD-010 Kyiv alias) and the two log
lines of FR-016.

- **Build** 0 warnings, 0 errors. **Tests** 3018 / 3018 passed, 0 skipped.
  **Format** `dotnet format --verify-no-changes` clean.
- All 177 tests red before implementation are green; the 6 green-by-design
  tests stay green; no existing test regressed.
- No migration, no new table, column, index or audit action (db-design §5);
  `TheModelHasNoPendingChanges_SoNoMigrationIsNeeded` is green.
- Security-sensitive: the page shows students' personal data and grades
  (minors) — the policy, the no-echo rule and the log content are in §3 / §7.

Two deviations from the plan are recorded in §7: the shared language switcher
(US-039) had to stop echoing the raw query string, and two journal test
fixtures raced the start-up retention purge.

## 2. Source Artifacts

As in the front matter: Story (unversioned), Specification v1 (APPROVED),
Open Decisions v4 (OD-001 … OD-010 all resolved), API design v1, OpenAPI v1,
DB design v1, entity model v1, test strategy v1, AC → test matrix v1,
test-generation report v1. None is `SUPERSEDED`.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `GetJournalQuery.ExecuteAsync` (columns, rows, cells, orders); `JournalController.Index`; `Views/Journal/Index.cshtml` | `JournalColumnsTests`, `JournalRequestValidationTests` (form, drop-down), `JournalPageTests.TheJournal_ShowsTheColumnsAndTheStudents_…` | PASS |
| AC-002 | `GetJournalQuery.IsStudentOfPeriod` + submitter rule; Q3 student filter in `JournalSource.GetStudentMembersAsync` | `JournalRowsTests` (10 scenarios), `JournalSourceTests.GetStudentMembers_…` | PASS |
| AC-003 | `GetJournalQuery.Cell` (graded branch, late mark); OD-008 (a) choice in `ExecuteAsync` | `JournalCellTests` (grade, states, due vs B, late, duplicates), `JournalRowsTests.OffRoster_WithTwoSubmissions_IsOneRow` | PASS |
| AC-004 | `GetJournalQuery.Cell` — no submission → `NotAssigned` | `JournalCellTests.*WithoutSubmission_IsNotAssigned`, `JournalPageTests.TheJournal_…` | PASS |
| AC-005 | `GetJournalQuery.Cell` (draft and turn-in date in the full view only); `view` parsing; switch links in the view | `JournalCellTests` draft / `TurnedInOn` tests, `JournalRequestValidationTests.View_…`, `JournalPageTests.ADraftGrade_…`, `EachView_LinksToTheOther` | PASS |
| AC-006 | `GetJournalQuery.Cell` — `Unrecognised` with raw state, late kept, no points | `JournalCellTests.Unrecognised_*`, `JournalPageTests.TitlesAndRawStates_AreHtmlEncoded` | PASS |
| AC-007 | `GetJournalQuery.Cell` (ungraded branch, material → `Empty`); `JournalColumn.Kind` | `JournalCellTests.Ungraded_*`, `Material_*`, `JournalColumnsTests.Kinds_…` | PASS |
| AC-008 | `GetJournalQuery.Label` and row order (named by culture, then unnamed by id) | `JournalRowsTests` naming and order tests | PASS |
| AC-009 | `GetJournalQuery` constructor (no guard, no unit of work, no Google port); home page link | `JournalReadOnlyTests` (4 causes × use case and HTTP), `JournalPageTests.TheHomePage_LinksToTheJournal` | PASS |
| AC-010 | `InstallationPolicies.ViewJournal`, its registration, `[Authorize]` on `JournalController` | `JournalAuthorizationTests` (anonymous, temporary password, Admin, Dean); existing endpoint enumeration test | PASS |
| AC-011 | Validation in `GetJournalQuery` (`Single`, `ParseCourseId`, `ReadDate`, view, pair); `400` / `404` in `JournalController`; `JournalLog.Refused` | `JournalRequestValidationTests` (45 cases), `JournalPageTests` 400 / 404 / not-echoed, `JournalLoggingTests.AMalformedParameter_…` | PASS |
| AC-012 | `JournalTextKeys`; 36 new keys in `SharedResource.uk.resx` / `.en.resx`; culture number and date formatting in the view | `JournalTranslationTests`, `JournalPageTests.AnEnglishUser_…`, empty-state tests | PASS |
| AC-013 | `JournalSource` (four untracked reads, one command each, UTC guard) | `JournalSourceTests` (12), `JournalColumnsTests` bounded-read tests | PASS |
| AC-014 | `GetJournalQuery.StartOfDay`, `DateIn`, default period from B in the school zone | `JournalPeriodTests` (8) | PASS |
| AC-015 | `InstallationSettingsReader.TimeZone` (required, trimmed, IANA only, OD-010 alias) | `TimeZoneConfigurationTests` (8), `InstallationConfigurationTests` | PASS |

## 4. Change Set

### Production — created

| File | Trace |
|---|---|
| `src/ClassroomAgent.Web/Controllers/JournalController.cs` | spec FR-001, FR-008, FR-012, FR-016; openapi `GET /workspace/journal` |
| `src/ClassroomAgent.Web/Views/Journal/Index.cshtml` | spec FR-001 … FR-009, FR-015, VR-005; api-design §2.3 – §2.5; NFR-070 |
| `src/ClassroomAgent.Web/Security/JournalTextKeys.cs` | spec FR-015; api-design §2.5 (key families) |
| `src/ClassroomAgent.Web/Security/JournalLog.cs` | spec FR-016 (event ids 5250, 5251) |

### Production — completed from the OD-009 skeleton

| File | Trace |
|---|---|
| `src/ClassroomAgent.Application/UseCases/GetJournalQuery.cs` | spec FR-001 … FR-009, FR-013, VR-001 … VR-004, OD-005 … OD-008; entity model §4 |
| `src/ClassroomAgent.Infrastructure/Persistence/Repositories/JournalSource.cs` | db-design §2 (Q1 … Q4); entity model §2 |

The other skeleton files (`IJournalSource`, the read records, `JournalRequest`,
`SchoolTimeZone`, `JournalPageOutcome`, `JournalPageResult`, the DTOs) are
unchanged from TEST_WRITING; their trace is the entity model §2 – §3 and the
openapi components.

### Production — modified

| File | Trace |
|---|---|
| `src/ClassroomAgent.Application/Authorization/InstallationPolicies.cs` | spec FR-012 — `ViewJournal` |
| `src/ClassroomAgent.Web/Security/InstallationSecurityServices.cs` | spec FR-012 — policy registration, Admin and Dean |
| `src/ClassroomAgent.Web/Security/SignInRoutes.cs` | api-design §2.1 — `/workspace/journal` |
| `src/ClassroomAgent.Web/Configuration/InstallationSettings.cs` | spec FR-010 — `TimeZone` |
| `src/ClassroomAgent.Web/Configuration/InstallationSettingsReader.cs` | spec FR-010, VR-006; OD-010 (a) |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | entity model §6; spec FR-010 — DI registration |
| `src/ClassroomAgent.Web/Views/Home/Index.cshtml` | spec FR-011; api-design §3 `GET /` |
| `src/ClassroomAgent.Application/Localization/SharedResource.uk.resx` | spec FR-015 — translation entries |
| `src/ClassroomAgent.Application/Localization/SharedResource.en.resx` | spec FR-015 — translation entries |
| `src/ClassroomAgent.Web/wwwroot/css/site.css` | spec §9 NFR-070 — table scrolls inside the page |
| `src/ClassroomAgent.Web/Views/Shared/_LanguageSwitcher.cshtml` | spec VR-005, api-design §2.3 (no echo) — deviation D-1 |
| `src/ClassroomAgent.Web/Controllers/UiLanguageController.cs` | the same — `ReturnPathViewDataKey` constant, deviation D-1 |

### Tests — modified by IMPLEMENTATION

| File | Trace |
|---|---|
| `tests/ClassroomAgent.Tests/TestInfrastructure/JournalHostExtensions.cs` | `ac_test_matrix` fixture — deviation D-2 (wait for the start-up purge before seeding) |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/JournalSourceTests.cs` | `ac_test_matrix` SRC — deviation D-2, the same wait; no assertion changed |

All other test files are TEST_WRITING's (test-generation report §3) and were
not changed. No secret, generated database file, `.xlsx` or IDE-local file is
in the change set.

## 5. Validation Evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | exit 0; 0 warnings, 0 errors |
| Unit + persistence | `dotnet test tests/ClassroomAgent.Tests --filter "FullyQualifiedName~Journal&FullyQualifiedName!~Web"` | 144 / 144 passed (after the use case and port) |
| Journal set, repeated | `dotnet test tests/ClassroomAgent.Tests --filter "FullyQualifiedName~Journal"` × 4 after D-2 | 175 / 175 passed each run |
| Full suite | `dotnet test ClassroomAgent.sln` (Docker running) | exit 0; 3018 total, 3018 passed, 0 failed, 0 skipped, 2 m 58 s |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0, no changes |
| Markers | `rg "TODO\|TBD\|FIXME\|???"` over the new production files | none |

Before D-2 the journal set failed intermittently (1 of 175 in 3 of 6 runs) with
`23503 fk_submission_classroom_participant` or the membership FK while seeding —
see §7.

## 6. Configuration Changes

| Change | Required by |
|---|---|
| New required setting `Installation:TimeZone` — an IANA id, trimmed; absent, blank, unknown or a Windows id (e.g. `FLE Standard Time`) stops the start with `InstallationSettingException` naming the key | spec FR-010, VR-006, AC-015; DC-3 |
| `Europe/Kyiv` and `Europe/Kiev` are each tried for the other when the runtime does not know the configured one; no other alias | OD-010 (a) |

The repository holds no Web `appsettings` file; the test host already carries
the key (TEST_WRITING). Deployments must set it — DC-3 already lists the time
zone among the required settings.

## 7. Deviations and Discovered Problems

- **D-1 The language switcher echoed the raw query string (supporting change,
  security-relevant).** `_LanguageSwitcher.cshtml` (US-039) wrote
  `Request.Path + Request.QueryString` into a hidden `returnPath` field, so a
  malformed date or an unknown parameter reached the journal page body —
  against spec VR-005 and api-design §2.3, and caught by
  `JournalPageTests.AMalformedDate_…` and `AnUnknownParameter_…`. Minimal
  fix: a page may set its own return path in
  `ViewData[UiLanguageController.ReturnPathViewDataKey]`; the journal sets its
  canonical URL built from the validated values only. Every other page
  behaves exactly as before (the US-039 tests are green). The return path is
  still checked by `UiLanguageController.LocalReturnPath` on the way back.
  Related to the earlier US-039 INFO-1 (the switcher reflects the query
  string); the journal query carries no personal data (spec S-05), but its
  rejected values must not be reflected either. For SECURITY_REVIEW.
- **D-2 Test fixtures raced the start-up retention purge.** The purge (US-037)
  runs at every host start, in every mode (BR-026), and deletes participants
  without a membership. `JournalSourceTests` inserts participants without a
  membership right after `StartAsync`, and `SeedJournalAsync` inserts a
  participant before its membership; when the purge ran in between, the next
  insert failed on a foreign key. Both now wait for the first purge run
  (`WaitForPurgeRunsAsync(1, …)`, the existing US-037 helper) before seeding.
  No assertion changed; the fix is in fixture timing only. A TEST_WRITING
  defect that does not contradict any approved artifact.
- **Course label separator.** Spec I-7 says the section follows the name; the
  drop-down renders `name — section` (an em dash, not a translatable word).
- **`Journal.WorkspaceSection`** is "Google Workspace" in both languages — a
  product name, not translated (the US-025 translation test covers only the
  cell, validation and empty-state families).
- **Earlier findings unchanged:** API-8 wording (I-9), other screens not yet in
  the school time zone (I-10), no `Cache-Control: no-store` convention for
  pages with personal data (api-design §2.8), no read-only transaction
  convention (db-design F-2).

## 8. Open Decisions

None touched or newly required. OD-001 … OD-010 are resolved; OD-008 (a) and
OD-010 (a) are implemented as resolved.
