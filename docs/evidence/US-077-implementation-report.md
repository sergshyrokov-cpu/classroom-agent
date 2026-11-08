---
artifact_type: implementation_report
story: US-077
version: 1
status: DRAFT
created_at: 2026-11-08T14:23:49Z
updated_at: 2026-11-08T14:23:49Z
produced_by: dotnet-implementor
attempt: 1
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
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-077 Implementation Report

## 1. Summary

All five parts are implemented: the reuse of the Google access token, the failure kind in the log, the distinct event
id, the removal of warning 10103, and versioned static files. No migration, endpoint, setting, translation key,
package or audit event was added.

The full suite ran once, 7952 of 7954, and the two failures were then resolved and their classes rerun green (section
5). There was no second full run: SECURITY_REVIEW runs it.

## 2. Source Artifacts

As listed in `inputs` above. API and database design are NOT_APPLICABLE; `openapi` and `entity_model` were not produced.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (`docs/tests/US-077-ac-test-matrix.md`) | Status |
|---|---|---|---|
| AC-001 | `GoogleAccessTokenCache.GetAsync`; `GoogleClassroomReader.TokenAuthorizer.AuthorizeAsync` | `GoogleClassroomReaderTokenReuseTests`, `TokenReuseSyncTests` | Pass |
| AC-002 | `GoogleAccessTokenCache.KeyOf` | `GoogleClassroomReaderTokenReuseTests` | Pass |
| AC-003 | `GoogleAccessTokenCache.ValidToken`, `Accept`; `GoogleRetryHandler` `beforeAttempt` | `GoogleClassroomReaderTokenReuseTests` | Pass |
| AC-004 | `GoogleAccessTokenCache` per-pair `SemaphoreSlim` | `GoogleClassroomReaderTokenReuseTests` | Pass |
| AC-005 | `Accept`, `ForgetUnusable`, `Forget`; `TokenAuthorizer.HandleResponseAsync`; failures pass through `GuardAsync` | `GoogleClassroomReaderTokenReuseTests`, `GoogleClassroomReaderRetryTests` | Pass |
| AC-006 | `GoogleClassroomReader.RequestTokenAsync` (credential local); `GoogleAccessTokenCache.Entry`, `IssuedToken` (plain classes) | `GoogleClassroomReaderTokenReuseTests`, `TokenReuseSyncTests` | Pass |
| AC-007 | `GoogleSignInFailureClassifier.Classify`, `GoogleSignInFailureKind`, `SignInLog.CallbackFailedWithKind`, `GoogleSignInEvents.OnRemoteFailureAsync` | `GoogleSignInFailureKindTests` | Pass |
| AC-008 | `RefusalLog.Refused` = 5290 | `EventIdCollisionTests` | Pass |
| AC-009 | `WorkspaceConnectionRepository` `OrderBy(c => c.Id)` in both reads | `WorkspaceConnectionRepositoryOrderingTests` | Pass |
| AC-010 | `asp-append-version="true"` on the 21 references | `VersionedStaticFileRuleTests`, `VersionedStaticFilePageTests` | Pass |
| AC-011 | Only `ScriptTagHelper` and `LinkTagHelper` enabled; no `.resx` or `.csproj` in the change set | `VersionedStaticFileRuleTests`, `VersionedStaticFilePageTests`; `git status` shows no `.resx`, `.csproj` or `appsettings` | Pass |

**Design decisions the Specification left open (FR-003).** The token step is a `beforeAttempt` delegate of
`GoogleRetryHandler`, called at the top of each attempt, before the attempt's time bound starts and outside its
`catch` blocks. So each attempt takes its token at that moment; the token request and the wait for it are not charged
to the attempt (SPEC_REVIEW m-2); and a failed token request ends the read rather than being repeated (I-11). `401`
is handled by an `IHttpUnsuccessfulResponseHandler`, so the library's own tries repeat the attempt as its credential
did. `GoogleMeetReportsReader` passes no delegate and is unchanged.

## 4. Change Set

| File | Trace |
|---|---|
| `src/ClassroomAgent.Infrastructure/Google/GoogleAccessTokenCache.cs` (new) | FR-001…FR-005, VR-001, VR-002, VR-002a, S-01, S-02 |
| `src/ClassroomAgent.Infrastructure/Google/GoogleClassroomReader.cs` | FR-001, FR-003, FR-005, S-02 |
| `src/ClassroomAgent.Infrastructure/Google/GoogleRetryHandler.cs` | FR-003, FR-004 |
| `src/ClassroomAgent.Infrastructure/ReadOnly/RefusalLog.cs` | FR-007 |
| `src/ClassroomAgent.Infrastructure/Persistence/Repositories/WorkspaceConnectionRepository.cs` | FR-008 |
| `src/ClassroomAgent.Web/Security/GoogleSignInFailureKind.cs`, `GoogleSignInFailureClassifier.cs` (new) | FR-006, VR-003 |
| `src/ClassroomAgent.Web/Security/SignInLog.cs`, `GoogleSignInEvents.cs` | FR-006, S-04 |
| `src/ClassroomAgent.Web/Views/`: `Shared/_Layout`, `ConnectionInstruction/Index`, `Courses/Index`, `Journal/Index`, `MeetCodes/CourseChoice`, `MeetReports/Index`, `MeetReports/Meeting`, `Report/Index`, `ReportTemplates/Form`, `DeanAccounts/Index`, `DeanPassword/Forced`, `DeanPassword/Own`, `SignIn/Index`, `_ViewImports` | FR-009 |
| `src/ClassroomAgent.ControlPlane/Views/`: `Shared/_Layout`, `Installations/Detail`, `Setup/Index`, `SignIn/Index`, `_ViewImports` | FR-009 |
| `tests/.../Infrastructure/Google/GoogleClassroomReaderTokenReuseTests.cs` (new) | ac_test_matrix |
| `tests/.../Infrastructure/Persistence/WorkspaceConnectionRepositoryOrderingTests.cs` (new) | ac_test_matrix |
| `tests/.../Web/BackgroundServices/TokenReuseSyncTests.cs` (new) | ac_test_matrix |
| `tests/.../Web/Logging/GoogleSignInFailureKindTests.cs` (new) | ac_test_matrix |
| `tests/.../Web/Pages/VersionedStaticFilePageTests.cs` (new) | ac_test_matrix |
| `tests/.../Architecture/EventIdCollisionTests.cs`, `VersionedStaticFileRuleTests.cs` (new) | ac_test_matrix |
| `tests/.../TestInfrastructure/GoogleSignInStub.cs` | Supporting: failures for AC-007 |
| `tests/.../TestInfrastructure/PageHelpTestData.cs`, `ConnectionInstructionHostExtensions.cs` | Supporting: FR-009 changes script addresses |
| `tests/.../ControlPlane/Controllers/InstallationDetailTests.cs` | Supporting: its pattern now expects the `?v=` of FR-009 |

No secret, generated database file or IDE-local config is in the change set.

## 5. Validation Evidence

Every run below was delegated to a `cheap-worker`.

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` (final) | 0 | 0 warnings, 0 errors |
| `dotnet format ClassroomAgent.sln --verify-no-changes --no-restore --include` the 11 changed C# files then existing | 0 | No changes needed |
| `dotnet test` for each of the 7 new classes and `GoogleClassroomReaderTests`, `…RetryTests`, `…ValueRangeTests` | 0 | 41, 3, 2, 27, 4, 27, 13 and 14, 35, 36, all passed |
| `dotnet test ClassroomAgent.sln --no-build` | 2 | Total 7954, passed 7952, failed 2, skipped 0; 16 min 55 s; no Docker errors |
| `dotnet test --filter FullyQualifiedName~InstallationDetailTests` (after the fix) | 0 | 13 of 13 |
| `dotnet test --filter FullyQualifiedName~StatusPushLoggingTests` | 0 | 12 of 12 |

The two failures of the full run:

1. `InstallationDetailTests.Detail_IdentifierIsSelectableText_…`: its pattern required the script address with nothing
   after `.js`. FR-009 appends the version. The pattern was changed to require `?v=`.
2. `StatusPushLoggingTests.DeferredPush_IsNotLogged_…`: timed out waiting for a third Control Plane call. This test is
   already recorded as flaky (US-062 follow-ups). This Story touches nothing in that path; free memory during the run
   was 3.4 GB, and the class passed on rerun.

Not delegated: the C# of the token cache and the sign-in classifier, the meaning of each failure, this report. The view
edits were delegated against the FR-009 table.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

- **Test changed at this stage:** `TheTokenRequest_AndTheWaitForIt_AreNotChargedToTheClassroomAttempt` advanced the manual
  clock, but the per-attempt bound runs on the system clock (`GoogleRetryHandler`), so the test could not have failed for
  its reason. It now uses a real 2 s bound and two 1.2 s steps. This makes the assertion stronger, and it costs about
  2.4 s of real time.
- **Test changed at this stage:** `AVersionedAddress_IsServedUnderThePagesPolicy` built the request through a helper that
  puts `?v=` into the path, which answers 404. It now goes through the client, as a browser does.
- The token cache's scope comparison is order-independent (`KeyOf` sorts); no public path can exercise it (test
  strategy section 5).

## 8. Open Decisions

None touched; none required.
