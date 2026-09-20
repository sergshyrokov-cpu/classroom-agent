---
artifact_type: test_generation_report
story: US-009
version: 1
status: DRAFT
created_at: 2026-09-20T14:05:00Z
updated_at: 2026-09-20T14:05:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-009-spec.md
    version: 1
  - path: docs/designs/api/US-009-openapi.yaml
    version: 1
  - path: docs/designs/database/US-009-db-design.md
    version: 1
  - path: docs/tests/US-009-test-strategy.md
    version: 1
  - path: docs/tests/US-009-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-009 Test Generation Report

## 1. Result

`PASS`. 84 test methods in nine new classes, 141 executed cases. The red phase
is verified on real PostgreSQL: **131 of the 141 fail** because the production
behaviour does not exist, **10 pass** and lock invariants this Story must not
break. The 1505 tests of US-001…US-008 are unaffected — all of them still pass.

## 2. Files created

| File | Contents |
|---|---|
| `tests/ClassroomAgent.Tests/TestInfrastructure/WorkspaceConnectionTestData.cs` | the path, the single field, synthetic addresses, audit codes, the nineteen translation keys |
| `tests/ClassroomAgent.Tests/TestInfrastructure/WorkspaceConnectionHostExtensions.cs` | signs an Admin in, opens the page, posts the save, reads the connection and audit rows |
| `tests/ClassroomAgent.Tests/Web/Security/WorkspaceConnectionAuthorizationTests.cs` | 10 methods — AC-001 |
| `tests/ClassroomAgent.Tests/Web/Pages/WorkspaceConnectionPageTests.cs` | 11 methods — AC-002, AC-009, AC-010, OD-002 |
| `tests/ClassroomAgent.Tests/Web/UseCases/SaveWorkspaceConnectionTests.cs` | 12 methods — AC-003, AC-004, AC-007 |
| `tests/ClassroomAgent.Tests/Web/UseCases/WorkspaceConnectionInvariantTests.cs` | 10 methods — AC-004, AC-005, AC-010 |
| `tests/ClassroomAgent.Tests/Web/UseCases/WorkspaceConnectionValidationTests.cs` | 9 methods — AC-006 |
| `tests/ClassroomAgent.Tests/Web/UseCases/WorkspaceConnectionAuditTests.cs` | 10 methods — AC-008 |
| `tests/ClassroomAgent.Tests/Web/UseCases/WorkspaceConnectionReadOnlyTests.cs` | 8 methods — AC-009 |
| `tests/ClassroomAgent.Tests/Web/Persistence/WorkspaceConnectionSchemaTests.cs` | 10 methods — AC-012, AC-007 |
| `tests/ClassroomAgent.Tests/Web/Localization/WorkspaceConnectionTranslationTests.cs` | 5 methods — AC-011 |

## 3. Files modified

None. No existing test was changed, disabled or weakened, and **no production
source file was created or modified** — unlike US-005 and US-007, this Story
needed no compile-only skeleton and therefore no Open Decision for one.

## 4. Commands and evidence

```
dotnet build ClassroomAgent.sln
  Сборка успешно завершена. Предупреждений: 0. Ошибок: 0.

dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~WorkspaceConnection"
  итог: 141   сбой: 131   успешно: 10   пропущено: 0

dotnet test ClassroomAgent.sln
  итог: 1646  сбой: 131   успешно: 1515  пропущено: 0
```

1646 − 141 = 1505 pre-existing cases; 1515 − 10 = 1505 of them pass. No
regression.

Integration tests ran against PostgreSQL in Testcontainers (Docker Desktop,
server 29.8.0); the InMemory provider is not used anywhere (TC-2).

## 5. Expected failures — the red phase

Every failure names missing production behaviour. The four shapes observed:

| Failure | Cause |
|---|---|
| `42P01: relation "workspace_connection" does not exist` | the migration of db-design §7.1 is not written |
| `No policy found: ConfigureWorkspaceConnection` | the policy of spec FR-010 is not registered |
| `Expected: Found, Actual: NotFound` and `Expected: Redirect/Conflict, Actual: NotFound` | neither endpoint of US-009 openapi exists |
| `Translation key '…' is missing for 'uk'` | the nineteen keys of spec FR-011 are not in the resource files |

No failure is caused by a syntax error, an invalid import, a missing dependency,
a broken fixture or a wrong assertion. Nothing was stubbed to make a test fail
"correctly".

## 6. Tests that pass before implementation

Ten cases pass, and each is deliberate. Two kinds:

**Invariant locks — they must keep passing.**

- `ThePermittedServiceWriteList_IsUnchanged` and
  `NoNewUseCase_IsRegisteredAsAPermittedServiceWrite`: the BR-026 closed list
  keeps its four members and its three registrations. Saving a connection is
  guarded, not exempt.
- `TheAnonymousList_GainsNothing`: the SC-4 closed list is untouched.
- `TheTwoFiles_CarryTheSameKeys`: the Ukrainian and English files stay in step —
  this will fail the moment a key is added to one file only.
- `NoRow_CarriesTheAddressOrTheDomain`: already meaningful over the US-008
  sign-in rows, and it extends to this Story's rows as they appear.

**Guards against a write that must never happen.** They are vacuously true while
nothing writes, and become real when the save exists:
`AMalformedRequest_WritesNoAuditRow`, `TheRejectedValue_IsNotLogged`,
`ARefusedSave_LogsNoAddress`, `ASuccessfulSave_LogsNoAddress`,
`TheSave_CallsNothingOutsideTheInstallation`.

**Two tests were corrected during the stage** because they passed for the wrong
reason. `AnUnknownTargetType_IsRejected` and `AMixedCaseRow_IsRejected` asserted
only the SQL state `23514`, which the *current* `ck_audit_event_action` already
produces — the row was rejected by a different rule than the one under test.
Both now assert the constraint **name** from the `PostgresException`, and both
are red. No assertion was weakened; two were made stricter.

## 7. Untested Acceptance Criteria

None. All twelve are mapped in the matrix.

Two coverage limitations are recorded rather than hidden (test strategy §8):

- **`DomainNotConfirmed` cannot be isolated over HTTP.** An installation that
  has never confirmed its legitimacy is always in read-only mode, and the guard
  runs first, so that refusal is what an Admin sees. The two tests of this case
  assert everything else exactly and accept either audit category. AC-010 asks
  for the branches to be provable independently; IMPLEMENTATION must add a
  direct unit test of `SaveWorkspaceConnectionUseCase` for the branch. This is
  the same shape of limitation US-008 recorded for `CompleteGoogleSignInUseCase`.
- **No unit test names a US-009 production type**, because TEST_WRITING may not
  create production source. The domain comparison rule of spec FR-007 is a pure
  function and deserves its own table of cases at implementation time; here it
  is covered through the host.

## 8. Interpretations this stage fixed, for IMPLEMENTATION to honour or correct

- **The path is `/settings/workspace-connection`** and the single form field is
  `impersonationUserEmail`, exactly as US-009 openapi names them.
- **Nineteen translation keys** are named in `WorkspaceConnectionTestData.TextKeys`,
  including `Landing.Settings.WorkspaceConnection` for the navigation entry. The
  Specification fixes that keys exist, not their spelling; these are the
  spellings the tests assert, and IMPLEMENTATION uses them rather than inventing
  others.
- **Four audit refusal categories** are asserted with the codes db-design §4.2
  fixes.
- **The page renders the stored address as text**, so `TheStoredAddress_IsRenderedAsStored`
  passes whether it sits in an input or beside one.
- The save helper falls back to the landing page for an antiforgery token while
  the settings page does not exist. Once the page exists it carries its own
  token and the fallback stops being used.

## 9. Open Decisions

None outstanding. OD-001 and OD-002 arrived resolved and the tests are written
against both: the request carries no domain field, and a stored connection whose
domain no longer matches is reported and left alone.
