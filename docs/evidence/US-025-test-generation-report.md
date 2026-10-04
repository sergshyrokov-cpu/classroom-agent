---
artifact_type: test_generation_report
story: US-025
version: 1
status: DRAFT
created_at: 2026-10-04T12:20:00Z
updated_at: 2026-10-04T12:20:00Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-025-test-strategy.md
    version: 1
  - path: docs/tests/US-025-ac-test-matrix.md
    version: 1
  - path: docs/specifications/US-025-spec.md
    version: 1
  - path: docs/designs/api/US-025-openapi.yaml
    version: 1
  - path: docs/designs/database/US-025-db-design.md
    version: 1
  - path: docs/designs/database/US-025-entity-model.md
    version: 1
  - path: docs/decisions/US-025-open-decisions.md
    version: 3
supersedes: null
---

# US-025 Test Generation Report

## 1. Result

**PASS — red phase verified.** Build 0 warnings / 0 errors. Full suite
(`dotnet test ClassroomAgent.sln`, Docker running): **3016 total, 2838 passed,
178 failed, 0 skipped**, 2 m 38 s. Of the 178 failures, 177 are new US-025
tests failing for missing production behaviour; the 1 other was an existing
test whose requirement this Story changes (§5), updated and re-run green
(`InstallationConfigurationTests`, 34/34). **No existing test regressed.**

## 2. Production skeleton (OD-009 a, Owner 2026-10-04)

Compile-only, nothing registered in DI, no existing behaviour changed:

- `src/ClassroomAgent.Application/Ports/IJournalSource.cs` (interface)
- `src/ClassroomAgent.Application/Models/` — `JournalCourseRecord`, `JournalItemRecord`, `JournalMemberRecord`, `JournalSubmissionRecord`, `SchoolTimeZone`, `JournalPageOutcome`, `JournalPageResult` (declarations)
- `src/ClassroomAgent.Application/Models/Requests/JournalRequest.cs` (declaration)
- `src/ClassroomAgent.Application/Models/Dtos/` — `JournalPageModel`, `CourseOption`, `Journal`, `JournalColumn`, `JournalRow`, `JournalCell`, `JournalCellState`, `JournalMessageKey`, `JournalEmptyStateKey`, `JournalView`, `JournalNameKind` (declarations, per openapi components)
- `src/ClassroomAgent.Application/UseCases/GetJournalQuery.cs` (throws)
- `src/ClassroomAgent.Infrastructure/Persistence/Repositories/JournalSource.cs` (throws)

Binding signatures for IMPLEMENTATION, beyond the entity model:
`GetJournalQuery(IJournalSource, SchoolTimeZone, TimeProvider)` with
`ExecuteAsync(JournalRequest, CultureInfo uiCulture, CancellationToken) →
JournalPageResult`; `JournalRequest` carries every occurrence of each query
parameter so "exactly one value" is decided in `Application`.

## 3. Test files

Fixtures written by the orchestrating session; test classes by three Sonnet
subagents from fixed briefs (the matrix), reviewed and the full suite run by the
orchestrating session.

| File | Tests (cases) | Before implementation |
|---|---|---|
| `TestInfrastructure/JournalTestData.cs` | fixture | — |
| `TestInfrastructure/FakeJournalSource.cs` | fixture | — |
| `TestInfrastructure/JournalHostExtensions.cs` | fixture | — |
| `TestInfrastructure/SeededJournal.cs` | fixture | — |
| `Application/UseCases/JournalCellTests.cs` | 51 | 51 red |
| `Application/UseCases/JournalRowsTests.cs` | 19 | 19 red |
| `Application/UseCases/JournalColumnsTests.cs` | 9 | 9 red |
| `Application/UseCases/JournalRequestValidationTests.cs` | 45 | 45 red |
| `Application/UseCases/JournalPeriodTests.cs` | 8 | 8 red |
| `Infrastructure/Persistence/JournalSourceTests.cs` | 12 | 11 red, 1 green |
| `Web/UseCases/JournalReadOnlyTests.cs` | 8 | 8 red |
| `Web/Configuration/TimeZoneConfigurationTests.cs` | 8 | 4 red, 4 green |
| `Web/Pages/JournalPageTests.cs` | 16 | 16 red |
| `Web/Security/JournalAuthorizationTests.cs` | 4 | 3 red, 1 green |
| `Web/Localization/JournalTranslationTests.cs` | 1 | 1 red |
| `Web/Logging/JournalLoggingTests.cs` | 2 | 2 red |
| **Total new** | **183** | **177 red, 6 green** |

Modified:

- `TestInfrastructure/InstallationTestHost.cs` — default settings gain
  `Installation:TimeZone` (spec FR-010 makes it required).
- `Web/Configuration/InstallationConfigurationTests.cs` —
  `OnlyTheSettingsOfThisStoryAreRequired_NoTimeZoneOrLanguage` asserted 11
  settings and the time zone's absence; spec FR-010 makes the time zone required.
  Renamed `OnlyTheRequiredSettingsAreGiven_NoLanguage`, now 12 settings and the
  time zone present; the language assertion is unchanged.

## 4. Red-phase classification

- **132 unit tests** fail with `NotImplementedException` from `GetJournalQuery`.
- **11 `JournalSourceTests`** fail with `NotImplementedException` (the two
  `ArgumentException` tests report "expected ArgumentException, actual
  NotImplementedException"; the one-command test fails at the first call).
- **4 Application-level read-only tests** fail: `GetJournalQuery` is not
  registered. **4 HTTP-level read-only tests**, **16 page**, **3 authorization**,
  **2 logging** tests fail with `404` — the page does not exist. The anonymous
  test is red too: an unrouted path answers `404`, not the sign-in redirect.
- **1 translation test** fails: `Journal.Cell.NotAssigned` missing for `uk`.
- **4 time zone refusal tests** fail: the host starts without validating the
  setting.

Green by design (4):

- `JournalSourceTests.TheModelHasNoPendingChanges_SoNoMigrationIsNeeded` — the
  Story adds no schema (db-design §5); a regression guard.
- `TimeZoneConfigurationTests.ValidTimeZone_Starts` (plain and padded) — guards
  against over-strict validation once validation exists.
- `JournalAuthorizationTests.ADeanWithATemporaryPassword_IsSentToTheForcedChange`
  — the restricted-session middleware redirects before routing, so it holds for
  any path. It stays meaningful after implementation (the page must not escape
  the restricted session), and the allowed-role tests in the same class are red.

Every "nothing in the log" assertion is paired with a positive wait for the
journal's own log event in the same test, so neither logging test is vacuous.

## 5. Findings

- **F-1 (environment; affects IMPLEMENTATION and deployment).** On this Windows
  10 machine the .NET 10 runtime does not know `Europe/Kyiv` (the tzdata 2022b
  name); it resolves only the older alias `Europe/Kiev` (Windows' bundled ICU).
  The first unit run failed with `TimeZoneNotFoundException` for that reason.
  The fixture now uses the first id the runtime knows (`Europe/Kyiv`, else
  `Europe/Kiev` — the same zone), in tests and in the default host setting.
  Consequence: a school configured with `Europe/Kyiv` on such a host would refuse
  to start under FR-010. Resolved as OD-010 (a) by the Owner on 2026-10-04: the
  Data Plane accepts both spellings. `TimeZoneConfigurationTests.EitherKyivSpelling_Starts`
  (2 cases) encodes it; green before implementation because nothing validates the
  setting yet, and it guards the alias once validation exists.
- **F-2.** `db-design §2` said Q1 runs only for a valid query shape; the form
  shows the drop-down on `400` / `404` too (api-design §2.3), so Q1 runs on every
  request. The DB design was corrected (same version, DRAFT) before the tests were
  written; the tests do not count `CourseCalls` on invalid input.
- **F-3.** `JournalLoggingTests` identifies the Information event by a
  structured property equal to the course id: the implementation must log the
  course id as a property (FR-016 already requires it in the line).

## 6. Coverage

Every Acceptance Criterion AC-001 … AC-015 maps to at least one test
(`docs/tests/US-025-ac-test-matrix.md`). No untested criterion. No open
decision remains (OD-008 a, OD-009 a, OD-010 a resolved).

## 7. Commands

- `dotnet build ClassroomAgent.sln` / `dotnet build tests/ClassroomAgent.Tests` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 3016 / 2838 passed / 178 failed / 0 skipped.
- `dotnet test tests/ClassroomAgent.Tests --filter "FullyQualifiedName~InstallationConfigurationTests"` — 34 / 34 passed after §3's update.
