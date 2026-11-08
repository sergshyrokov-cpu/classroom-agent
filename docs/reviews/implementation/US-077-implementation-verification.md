---
artifact_type: implementation_verification
story: US-077
version: 1
status: APPROVED
created_at: 2026-11-08T14:35:44Z
updated_at: 2026-11-08T14:35:44Z
produced_by: implementation-verifier
inputs:
  - path: docs/stories/US-077-diagnostics-before-pilot.md
    version: null
  - path: docs/specifications/US-077-spec.md
    version: 2
  - path: docs/decisions/US-077-open-decisions.md
    version: 1
  - path: docs/designs/api/US-077-api-design.md
    version: 1
  - path: docs/designs/database/US-077-db-design.md
    version: 1
  - path: docs/tests/US-077-test-strategy.md
    version: 1
  - path: docs/tests/US-077-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-077-test-generation-report.md
    version: 1
  - path: docs/evidence/US-077-implementation-report.md
    version: 1
supersedes: null
---

# US-077 Implementation Verification

## Summary

**Verdict: PASS.** All eleven Acceptance Criteria are met and proved, every functional requirement is implemented where
the report says, the report's change set equals the working tree, and all 117 tests of the Story's seven classes pass.
No Critical or Major finding; one Minor finding about a test (m-1).

The review read the code and tests themselves, in a fresh context. `dotnet build` and the test runs were delegated to
a `cheap-worker`, which reported facts only. All judgement was made here. The full suite was not rerun:
SECURITY_REVIEW runs it.

## Reviewed Artifacts

As listed in `inputs`. API and database designs are NOT_APPLICABLE, so `openapi` and `entity_model` do not exist. Also
reviewed: `git status`, `git diff`, and the ten untracked code and test files.

## AC Verification

| AC | Tests read | Verdict |
|---|---|---|
| AC-001 | `ConsecutiveReads_…`, `EveryPageOfAPagedRead_…`, `AWholeRun_RequestsOneTokenPerScopeSet` | Proved: one token, on every request's header; a whole run costs two, with more than ten reads |
| AC-002 | `DifferentAccounts_…`, `AccountsDifferingOnlyByCase_…`, `TheTwoScopeSets_…` | Proved: the token's subject and scopes are decoded from the signed assertion |
| AC-003 | `ATokenAtTheMargin_…` (3299/3300/3301), `AnExpiredToken_…`, `Expiry_IsCounted…`, `APagedReadCrossing…`, `ARetriedAttempt_…`, VR-002 and VR-002a tests | Proved, boundaries included |
| AC-004 | `ConcurrentReads_…` (five tests), `AReadCancelledWhileWaiting_…`, `TheTokenRequest_AndTheWaitForIt_…` | Met. The count, the one-in-flight bound and the success of every read are proved; "none crossed" between two accounts is only partly observed (m-1) |
| AC-005 | `ARefusal_KeepsItsDiagnosis_…`, `ATokenOutage_…`, `AnEmptyToken_…`, `ARefusalAfterExpiry_…`, `ARemovedKey_…`, three `401` tests | Proved. The older-token `401` test fails if the entry is dropped unconditionally |
| AC-006 | `TheKey_IsResolvedOnce…`, `BetweenRequests_…`, `NothingTheReaderHolds_…`, `AnExpiredToken_IsForgotten…`, `NeitherTheToken…`, `AfterARun_…` | Proved. The field walk must find the token, so it is not vacuous. The database and log check uses the token `GoogleClassroomScript` really issues |
| AC-007 | Eight real-handler tests, the 16-row table, `NoFailureObject_IsOther`, `AnotherExceptionType_…`, `ARefusedIdentity_CarriesNoKind` | Proved through the real framework messages; marker, email, code and `state` are checked absent from the whole log; audit row and redirect asserted |
| AC-008 | `EventIdCollisionTests` (4) | Proved. Additionally read: `5290` occurs once under `src` in any declaration style |
| AC-009 | `WorkspaceConnectionRepositoryOrderingTests` (3) | Proved: the warning is turned into an exception on real PostgreSQL; tracking asserted |
| AC-010 | `VersionedStaticFileRuleTests`, `VersionedStaticFilePageTests` | Proved: `v=` equals the SHA-256 of the file on disk, on both hosts and error pages. Additionally read: exactly 21 `asp-append-version` in views, and no unversioned own reference |
| AC-011 | Rule and page tests; diff read | Met. See below |

**Closed by reading (TEST_WRITING INFO-1).** No `.resx`, `.csproj`, `appsettings` or `Directory.*` file is modified or
untracked, so translation files and NuGet references are unchanged. `GoogleAccessProbe` is not in the diff (S-06). The
order-independent comparison of FR-002 is `KeyOf`: scopes sorted ordinally and joined by a space, which no scope
contains, so distinct sets cannot share a key.

## Specification Coverage

| Requirement | Where | Verdict |
|---|---|---|
| FR-001, S-01, S-02 | `GoogleAccessTokenCache` owned by the reader; `Entry` and `IssuedToken` are plain classes; the credential is a local of `RequestTokenAsync` | Implemented |
| FR-002, S-03 | `KeyOf`, ordinal record-struct key | Implemented |
| FR-003, I-11 | `beforeAttempt` at the top of each `GoogleRetryHandler` attempt, before the time bound and outside its catches; token client is per request | Implemented |
| FR-004 | Per-pair `SemaphoreSlim` with re-check | Implemented |
| FR-005, I-4 | `Accept` writes only on success; `ForgetUnusable`; `Forget` compares the token; `HandleResponseAsync` for `401` | Implemented |
| FR-006, VR-003, S-04 | `GoogleSignInFailureClassifier`, in table order; `CallbackFailedWithKind` writes only kind and request id | Implemented |
| FR-007 | `RefusalLog` = 5290 | Implemented |
| FR-008 | `OrderBy(c => c.Id)` in both reads | Implemented |
| FR-009, I-7 | 21 references; only the two tag helpers | Implemented |
| VR-001, VR-002, VR-002a | `Accept` | Implemented |
| VR-004, S-05, S-06, S-07 | Guards, endpoint, probe, audit and authorization untouched in the diff | Held |

No behaviour outside the artifacts was found.

## Design Conformance

No endpoint, schema or migration is in the change. AD-3: no project reference changed. AD-4: the cache and every Google
SDK type stay in `Infrastructure`; `IClassroomReader` is unchanged. AD-6 and AD-8 are not touched.

## Deviations

No departure from an approved artifact was found, disclosed or undisclosed. Report section 7 lists two tests corrected
at IMPLEMENTATION; both corrections are sound:

| Item | Reason | Verdict |
|---|---|---|
| `TheTokenRequest_AndTheWaitForIt_…` moved to a real 2 s bound | The bound runs on the system clock, so the manual clock could not exercise it; the test now fails if the token time is charged to the attempt | Accepted |
| `AVersionedAddress_…` requests through the client | The former helper put `?v=` into the path | Accepted |
| `InstallationDetailTests` pattern now requires `?v=` (report section 4) | Direct consequence of FR-009, and stricter than before | Accepted |

## Report Accuracy

The report's change set equals `git status`: 5 files in `Infrastructure`, 4 in `Web/Security`, 14 Web views, 5 Control Plane views, 7 new test classes and 4 changed test files. Every file is
traced (Definition of Done item 9). Each AC-to-test location exists, and test counts match the run below. No `TODO`,
`TBD`, `FIXME`, skipped test or `NotImplementedException` in the changed files.

The report's full run was 7952 of 7954, with its two failures rerun as classes only; a green full suite is still owed
and SECURITY_REVIEW runs it.

## Story Test Run

Delegated to `cheap-worker`. `dotnet build ClassroomAgent.sln`: exit 0, 0 warnings, 0 errors.

| Class | Exit | Total | Passed | Failed | Skipped |
|---|---|---|---|---|---|
| GoogleClassroomReaderTokenReuseTests | 0 | 41 | 41 | 0 | 0 |
| TokenReuseSyncTests | 0 | 2 | 2 | 0 | 0 |
| GoogleSignInFailureKindTests | 0 | 27 | 27 | 0 | 0 |
| EventIdCollisionTests | 0 | 4 | 4 | 0 | 0 |
| WorkspaceConnectionRepositoryOrderingTests | 0 | 3 | 3 | 0 | 0 |
| VersionedStaticFileRuleTests | 0 | 27 | 27 | 0 | 0 |
| VersionedStaticFilePageTests | 0 | 13 | 13 | 0 | 0 |

## Findings

**m-1 (Minor).** `GoogleClassroomReaderTokenReuseTests.ConcurrentReads_ForSeveralPairs_CauseOneTokenRequestPerPair`,
against the matrix row "Exactly K, none crossed" (AC-004). Its loop tells the two scope sets apart by scope count, but
all reads of `Account` and `OtherAccount` go to the same address, so a token crossed between those two accounts during
concurrent reads would still pass. The risk is small: the exact count needs separate keys, each read holds its own
key, and the sequential test `DifferentAccounts_…` does prove the subject. Reading a different course per account would
close it. Not routed back; loop-back key if the Owner wants it: `test_gap`.

**Not automated, by the Story's design:** the manual check on the server, that after the first kit update a page shows
new styles without Ctrl+F5.

## Verdict

PASS. Next stage: SECURITY_REVIEW.
