---
artifact_type: test_generation_report
story: US-004
version: 1
status: DRAFT
created_at: 2026-09-17T11:07:03Z
updated_at: 2026-09-17T11:07:03Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-004-test-strategy.md
    version: 1
  - path: docs/tests/US-004-ac-test-matrix.md
    version: 1
  - path: docs/specifications/US-004-spec.md
    version: 1
  - path: docs/designs/api/US-004-openapi.yaml
    version: 1
  - path: docs/designs/database/US-004-db-design.md
    version: 1
supersedes: null
---

# US-004 Test Generation Report

## 1. Files

Created:

- `tests/ClassroomAgent.Tests/TestInfrastructure/InstallationStatusTestData.cs`
- `tests/ClassroomAgent.Tests/TestInfrastructure/InstallationStatusHostExtensions.cs`
- `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstallationStatusChangeTests.cs` — 12 methods / 16 cases
- `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstallationStatusUnchangedTests.cs` — 11 methods / 17 cases
- `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstallationStatusAuditTests.cs` — 6 methods / 6 cases
- `tests/ClassroomAgent.Tests/ControlPlane/Security/InstallationStatusAuthorizationTests.cs` — 5 methods / 15 cases
- `tests/ClassroomAgent.Tests/ControlPlane/Security/InstallationStatusAntiforgeryTests.cs` — 4 methods / 7 cases
- `tests/ClassroomAgent.Tests/ControlPlane/Localization/InstallationStatusTranslationTests.cs` — 2 methods / 11 cases

Modified:

- `tests/ClassroomAgent.Tests/ControlPlane/Security/InstallationAuthorizationTests.cs` —
  `InstallationEndpoints_ExistAndNoneAllowsAnonymous` now also excludes
  `installations/{id}/suspension` and `installations/{id}/resumption` (covered by
  `InstallationStatusAuthorizationTests`), as US-003 did for `…/admins`. Its US-002
  route list and assertions are unchanged.

No production file was touched.

## 2. Commands

| Step | Command | Result |
|---|---|---|
| Docker | `docker info` | server 29.8.0 running |
| Build | `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors |
| Tests | `dotnet test --solution ClassroomAgent.sln --no-build --report-xunit-trx --report-xunit-trx-filename us004-red.trx` | exit 2 (failures expected); 44.7 s |

TRX: `TestResults/us004-red.trx` (git-ignored).

## 3. Results

| | Cases |
|---|---|
| Total | 571 |
| Passed | 510 |
| Failed | 61 |
| Skipped | 0 |

- **Existing regression tests:** all 499 pre-existing cases pass, including the
  modified `InstallationAuthorizationTests` (30 cases).
- **New US-004 cases:** 72 — 61 fail, 11 pass.

### 3.1 Expected failures (61)

All in the six new classes; causes checked in the TRX messages:

- translation keys missing (`Translation key 'Installation.…' is missing for 'uk'`),
  including page tests that look a key up before asserting — 15;
- routes `/installations/{id}/suspension` and `/resumption` do not exist
  (anonymous requests fall to the `404` catch-all instead of `302 /sign-in` or
  `/setup`; confirmations answer `404` instead of `200`/`302`; POSTs answer `404`,
  so no status change, audit row or notice redirect) — 44;
- detail page has no status action or notice element (`Sub-string not found`) — 2.

No failure is caused by compilation, fixtures, the test host or a contradiction
with the approved artifacts.

### 3.2 Passing before implementation (11) — guards

| Test | Why it passes now | Stays meaningful because |
|---|---|---|
| `NonUuidIdentifier_Returns404ErrorPage` ×5 | non-GUID paths already hit the US-001 `404` catch-all | the new routes must keep the `guid` constraint |
| `UnknownInstallation_AllFourOperations_Return404_ChangeNothing` | missing routes answer `404` | after implementation the routes exist and must still answer `404` for an unknown GUID |
| `Detail_RepeatedNoticeParameter_IsNotShown` | no notice is ever rendered yet | a naive binding of the parameter would render it |
| `OtherMethods_DoNotChangeStatus` ×3 | no route accepts PUT/PATCH/DELETE | the new controller must not add them |
| `InstallationNameAndDomain_NeverReachTheLogFile` | the missing endpoints log nothing | once implemented, logging of name or domain would fail it |

## 4. Coverage

- Every Acceptance Criterion AC-001 … AC-011 maps to at least one failing test
  (`docs/tests/US-004-ac-test-matrix.md`).
- Untested Acceptance Criteria: none.
- Host-wide enumeration tests of US-001 (anonymous endpoints, antiforgery on every
  POST) and `TranslationCompletenessTests` will include the new routes and keys
  automatically.

## 5. Open Decisions

None.

## 6. Overall result

Red phase verified: tests compile, the environment is valid, existing tests are
green, and the new behaviour tests fail only for missing implementation.
