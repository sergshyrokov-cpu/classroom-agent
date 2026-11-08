---
artifact_type: test_generation_report
story: US-077
version: 1
status: DRAFT
created_at: 2026-11-08T13:53:03Z
updated_at: 2026-11-08T13:53:03Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-077-test-strategy.md
    version: 1
  - path: docs/tests/US-077-ac-test-matrix.md
    version: 1
  - path: docs/specifications/US-077-spec.md
    version: 2
supersedes: null
---

# US-077 Test Generation Report

## 1. Files

**Created** (under `tests/ClassroomAgent.Tests/`):

- `Infrastructure/Google/GoogleClassroomReaderTokenReuseTests.cs`
- `Infrastructure/Persistence/WorkspaceConnectionRepositoryOrderingTests.cs`
- `Web/BackgroundServices/TokenReuseSyncTests.cs`
- `Web/Logging/GoogleSignInFailureKindTests.cs`
- `Web/Pages/VersionedStaticFilePageTests.cs`
- `Architecture/EventIdCollisionTests.cs`
- `Architecture/VersionedStaticFileRuleTests.cs`

**Modified** (test support only):

- `TestInfrastructure/GoogleSignInStub.cs`: `TokenAnswer`, `UserInformationAnswer`.
- `TestInfrastructure/PageHelpTestData.cs` and `TestInfrastructure/ConnectionInstructionHostExtensions.cs`:
  `ScriptSources` returns the address without its `?v=` (FR-009 changes those addresses).

**Compile-only skeleton:** none. No production file was changed.

**Delegated:** the three source-rule and page classes (`EventIdCollisionTests`, `VersionedStaticFileRuleTests`,
`VersionedStaticFilePageTests`) were written by a `cheap-worker` against a fixed brief; every build and test run was
delegated. The test design, the other classes and the red-phase verdict were not.

## 2. Commands

`dotnet build tests/ClassroomAgent.Tests/ClassroomAgent.Tests.csproj`: 0 errors, 0 warnings.

`dotnet test tests/ClassroomAgent.Tests/ClassroomAgent.Tests.csproj --no-build --filter FullyQualifiedName~<Class>`, one
class per run. Docker and Testcontainers worked in every run.

The full suite was not run: no production code changed, and the only shared code changed is the three test helpers,
whose users were run (section 3).

## 3. Results

| Class | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| GoogleClassroomReaderTokenReuseTests (new) | 41 | 18 | 23 | 0 |
| WorkspaceConnectionRepositoryOrderingTests (new) | 3 | 0 | 3 | 0 |
| TokenReuseSyncTests (new) | 2 | 1 | 1 | 0 |
| GoogleSignInFailureKindTests (new) | 27 | 1 | 26 | 0 |
| EventIdCollisionTests (new) | 4 | 2 | 2 | 0 |
| VersionedStaticFileRuleTests (new) | 27 | 2 | 25 | 0 |
| VersionedStaticFilePageTests (new) | 13 | 2 | 11 | 0 |
| **New, in all** | **117** | **26** | **91** | **0** |
| GoogleSignInCallbackTests (existing) | 14 | 14 | 0 | 0 |
| ConnectionInstructionHandoverTests (existing) | 14 | 14 | 0 | 0 |
| PageHelpTests (existing) | 20 | 20 | 0 | 0 |
| GoogleClassroomReaderRetryTests (existing) | 35 | 35 | 0 | 0 |

The first run of the first class hung for 9.5 minutes and was stopped: `A401ForAnOlderToken_DoesNotDropTheNewerOne` gave
its second read the same held answer, a defect of the test. It was corrected, and the figures above are from the
rerun (41 tests, 9 s).

## 4. Red-phase verdict

**The 91 failures are all expected.** Each is the missing behaviour named by its assertion: several tokens where one
is due (the reader asks per read); `FailureKind` absent (null); id 5201 where 5290 is due;
`FirstWithoutOrderByAndFilterWarning` raised; no `asp-append-version`, no `?v=`, no `@addTagHelper`; an empty or
already-expired token sent; nothing remembered by the reader. No failure comes from the setup. The `FailureKind`
failures occur after the category filter has found exactly one line, so that line's `SourceContext` is confirmed.

**The 26 that pass today, and why that is not vacuous:**

- The reader asks per read today, so these hold trivially now and become real once a token is remembered:
  `EveryPageOfAPagedRead_…`, `AccountsDifferingOnlyByCase_…`, `ATokenAtTheMargin` (3300, 3301),
  `AnExpiredToken_IsReplacedBeforeTheReadIsSent`, `Expiry_IsCountedFromBeforeTheTokenRequestWasSent`,
  `AShortLivedToken_…` (3 rows), `ARefusalAfterExpiry_…`, `AnExpiredToken_IsForgotten…`. Each is paired with a RED test
  proving reuse exists (`ATokenAtTheMargin` 3299, `ConsecutiveReads_…`, `ATokenJustAboveTheMargin_…`,
  `BetweenRequests_…`).
- Today's behaviour that must not change, per AC-005, FR-005 and S-01: `ARefusal_KeepsItsDiagnosis_…` (3),
  `ATokenOutage_…` (2), `EveryAttemptAnswering401_StopsAsToday` (measured today within 3),
  `NeitherTheTokenNorTheKey_…`, `AfterARun_TheTokenIsInNoLogLine_…`, `ARefusedIdentity_CarriesNoKind`,
  `RetentionPurgeStarted_KeepsItsId`, `AVersionedAddress_IsServedUnderThePagesPolicy`, `AForm_StillCarries…`,
  `NoView_AddsTagHelpersOfItsOwn` (2).
- `TheScan_FindsKnownEvents` proves the source scan reads real events.

**Unexpected failures:** none.

## 5. Findings handled

- SPEC_REVIEW m-1: covered by `ConcurrentReads_WithATokenThatCannotBeRemembered_…` and the VR-002 tests.
- SPEC_REVIEW m-2: the test `TheTokenRequest_AndTheWaitForIt_AreNotChargedToTheClassroomAttempt` exists. The Owner asked
  for it in the tests and in the implementation, so it stays open for IMPLEMENTATION.

## 6. Untested Acceptance Criteria

None. Two parts of AC-011 (translation files, NuGet references) and the order-independent comparison of scope sets
(FR-002) are not automated, for the reasons in the strategy's section 5; IMPLEMENTATION_VERIFICATION checks them.

## 7. Open Decisions

None.

## 8. Overall result

PASS: every AC is mapped, the tests compile, and the setup is valid; existing tests that use the changed helpers pass.
