---
artifact_type: ac_test_matrix
story: US-064
version: 1
status: DRAFT
created_at: 2026-10-29T07:20:30Z
updated_at: 2026-10-29T07:20:30Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-064-spec.md
    version: 1
  - path: docs/decisions/US-064-open-decisions.md
    version: 1
  - path: docs/tests/US-064-test-strategy.md
    version: 1
supersedes: null
---

# US-064 Acceptance Criteria → Test Matrix

Test class: `tests/ClassroomAgent.Tests/Web/Security/DisabledDeanSessionTests.cs`
(namespace `ClassroomAgent.Tests.Web.Security`). Level: integration/security over HTTP, real
PostgreSQL (TC-2). Status at TEST_WRITING: all **green** against current code — expected for this
test-only Story (spec §1, FR-008); each test also asserts the pre-disabling success (FR-003), so it is
not vacuous.

| Acceptance Criterion | Scenario | Test Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Old cookie of a disabled Dean on the journal page (FR-002, FR-003, FR-004) | Integration / security | DisabledDeanSessionTests | TheOldCookieOfADisabledDean_IsSentToSignIn_OnTheJournalPage | Before: `200` with course name. After: `302` → `/sign-in`, no course name | GREEN |
| AC-002 | Old cookie + pre-disabling token + accepted body on the journal export (FR-002, FR-003, FR-005, VR-001) | Integration / security | DisabledDeanSessionTests | TheOldCookieOfADisabledDean_DownloadsNoExport | Before: `200` `.xlsx`, 1 audit row. After: `401` API-6 JSON (timestamp, status, `unauthorized`, `Api.Error.SignInRequired`, path), no file, audit count unchanged | GREEN |
| AC-003 | Disable → re-enable → old cookie; then new password sign-in (FR-002, FR-003, FR-006) | Integration / security | DisabledDeanSessionTests | ReEnabling_DoesNotReviveTheOldCookie_ButANewSignInWorks | Before: `200`. After re-enable: old cookie `302` → `/sign-in`; new sign-in → journal `200` | GREEN |
| AC-004 | Two Deans, one disabled (FR-002, FR-003, FR-007) | Integration / security | DisabledDeanSessionTests | DisablingOneDean_LeavesAnotherDeansSessionWorking | Before: both `200`. After: disabled `302` → `/sign-in`, other `200` with content | GREEN |

Every Acceptance Criterion has a mapped test. FR-001 (real pipeline, Admin page, normal mode) and
FR-008 (defect fix only if a test fails — none failed) hold for all four tests.
