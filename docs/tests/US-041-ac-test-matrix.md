---
artifact_type: ac_test_matrix
story: US-041
version: 1
status: DRAFT
created_at: 2026-10-05T07:33:25Z
updated_at: 2026-10-05T07:33:25Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-041-spec.md
    version: 2
  - path: docs/designs/api/US-041-openapi.yaml
    version: 1
  - path: docs/tests/US-041-test-strategy.md
    version: 1
supersedes: null
---

# US-041 Acceptance Criteria → Test Matrix

Classes: **W** = `ClassroomAgent.Tests.Web.Security.UnknownFileLikePathTests`,
**C** = `ClassroomAgent.Tests.ControlPlane.Security.UnknownFileLikePathTests`,
**WN** = `ClassroomAgent.Tests.Web.Security.NoStoreResponseTests`.
Status is the pre-implementation state: **RED** fails today with `302`,
**GREEN** passes today (regression guard).

| AC | Scenario | Level | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | S-1 anonymous × 4 paths | integration | W | AMissingFileLikePath_AnswersTheErrorPageWith404 | 404, error page, no Location | RED (4) |
| AC-001 | S-1 Admin, Dean × 4 paths | integration | W | AMissingFileLikePath_AnswersTheErrorPageWith404 | 404, error page | GREEN (8) |
| AC-001 | S-2 POST/PUT/DELETE | integration | W | AMissingFileLikePath_AnyMethod_Answers404ToAnAnonymousCaller | 404, no Location | RED (3) |
| AC-001 | S-4 writes nothing | integration | W | AMissingFileLikePath_WritesNothing | 404; no AppUser, no audit row | RED |
| AC-001 | S-5 not echoed | integration | W | AMissingFileLikePath_IsNotEchoed | 404; marker absent | RED |
| AC-001 | missing file under a static path | integration | WN | AMissingFileUnderAStaticPath_CarriesNoStore | 404 + no-store | RED |
| AC-002 | S-1 anonymous × 4 paths, after setup | integration | C | AfterSetup_AMissingFileLikePath_AnswersTheErrorPageWith404 | 404, error page, no Location | RED (4) |
| AC-002 | S-1 Owner × 4 paths | integration | C | AfterSetup_AMissingFileLikePath_AnswersTheErrorPageWith404 | 404, error page | GREEN (4) |
| AC-002 | S-3 before setup × 3 paths | integration | C | BeforeSetup_AMissingFileLikePath_Answers404NotTheSetupGate | 404, not 302 /setup; no Owner | RED (3) |
| AC-002 | S-4 writes no audit row | integration | C | AMissingFileLikePath_WritesNoAuditRow | 404; no audit row | RED |
| AC-003 | S-6 static files | integration | W | AnExistingStaticFile_IsStillServedAnonymously | 200 for every wwwroot file | GREEN |
| AC-003 | S-6 static files before setup | integration | C | AnExistingStaticFile_IsStillServedAnonymouslyBeforeSetup | 200 for every wwwroot file | GREEN |
| AC-004 | S-7 protected page | integration | W | ARealProtectedPage_StillRedirectsAnAnonymousCallerToSignIn | 302 to sign-in | GREEN |
| AC-004 | S-7 protected page after setup | integration | C | AfterSetup_ARealProtectedEndpoint_StillRedirectsAnAnonymousCaller | 302, not /setup | GREEN |
| AC-004 | S-8 setup gate | integration | C | BeforeSetup_ARealEndpoint_IsStillRedirectedToSetup | 302 /setup | GREEN |
| AC-004 | S-9 private prefix | integration | W | OnThePrivatePort_AnUnmatchedFileLikePathUnderAPrivatePrefix_Answers404 | 404, no Location, no error page | RED (2) |
| AC-004 | S-10 private port filter | integration | W | OnThePrivatePort_AFileLikePathOutsideThePrivatePaths_Answers404 | 404 | GREEN |
| AC-004 | whole existing suite | all | — | full `dotnet test` | green | see report |
