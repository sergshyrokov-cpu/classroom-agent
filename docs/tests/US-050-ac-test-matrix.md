---
artifact_type: ac_test_matrix
story: US-050
version: 1
status: DRAFT
created_at: 2026-10-16T08:30:00Z
updated_at: 2026-10-16T08:30:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-050-spec.md
    version: 1
  - path: docs/designs/api/US-050-api-design.md
    version: 1
  - path: docs/designs/database/US-050-db-design.md
    version: 1
  - path: docs/tests/US-050-test-strategy.md
    version: 1
supersedes: null
---

# US-050 Acceptance Criteria → Test Matrix

Classes: **W** = `ClassroomAgent.Tests.Web.Security.SecurityHeadersResponseTests` (integration);
**P** = `ClassroomAgent.Tests.ControlPlane.Security.SecurityHeadersResponseTests` (integration);
**I** = `ClassroomAgent.Tests.Architecture.InlineCodeRuleTests` (source scan);
**R** = `ClassroomAgent.Tests.Architecture.SecurityHeadersRuleTests` (source scan).
"Headers" = the four headers, each exactly once, values of spec FR-003 / FR-004.
Status is the red-phase state recorded in the test generation report.

| Acceptance Criterion | Scenario | Test Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | P-1 every endpoint × method, public port, Admin/Dean/anonymous | Integration | W | `EveryEndpoint_OnThePublicPort_CarriesTheSecurityHeaders` | no response missing a header | RED |
| AC-001 | P-2 every endpoint, private port | Integration | W | `EveryEndpoint_OnThePrivatePort_CarriesTheSecurityHeaders` | headers | RED |
| AC-001 | P-2 unknown address, private port | Integration | W | `AnUnknownAddressOnThePrivatePort_CarriesTheSecurityHeaders` | 404 + headers | RED |
| AC-001 | P-4 signed-in pages, sign-in page | Integration | W | `APage_Returns200_WithTheSecurityHeaders` | 200 + headers | RED |
| AC-001 | B-2 antiforgery page: `DENY` only | Integration | W | `APageWithAnAntiforgeryToken_CarriesOnlyXFrameOptionsDeny` | `X-Frame-Options` = [`DENY`] | RED |
| AC-001 | P-4 the journal | Integration | W | `TheJournal_Returns200_WithTheSecurityHeaders` | 200 + headers | RED |
| AC-001 | P-4 export / `/api/v1` / downloaded file | Integration | W | `TheJournalExport_DownloadedFile_CarriesTheSecurityHeaders` | 200 file + headers | RED |
| AC-001 | N-1 `/api/v1` API-6 refusal | Integration | W | `AnApiRefusal_CarriesTheSecurityHeaders` | 400 + headers | RED |
| AC-001 | P-4 redirect to sign-in | Integration | W | `TheRedirectToSignIn_CarriesTheSecurityHeaders` | 302 + headers | RED |
| AC-001 | P-4 HTTPS redirection | Integration | W | `TheHttpsRedirection_CarriesTheSecurityHeaders` | 307/308/302 + headers | RED |
| AC-001 | N-1 403 | Integration | W | `A403Refusal_CarriesTheSecurityHeaders` | 403 + headers | RED |
| AC-001 | N-1 404 page and `/api/v1` | Integration | W | `A404_CarriesTheSecurityHeaders` | 404 + headers | RED |
| AC-001 | N-1 antiforgery 400 | Integration | W | `AnAntiforgeryRefusal_CarriesTheSecurityHeaders` | 400 + headers | RED |
| AC-001 | N-1 read-only 409 | Integration | W | `AReadOnlyRefusal_CarriesTheSecurityHeaders` | 409 + headers | RED |
| AC-001 | N-1 exception handler 500 | Integration | W | `AnExceptionHandler500_CarriesTheSecurityHeaders` | 500 + headers | RED |
| AC-001 | P-4 every static file (I-3) | Integration | W | `EveryStaticFile_CarriesTheSecurityHeaders` | 200 + headers | RED |
| AC-001 | N-1 missing static file | Integration | W | `AMissingFileUnderAStaticPath_CarriesTheSecurityHeaders` | 404 + headers | RED |
| AC-001 / AC-004 | N-1 Google callback address | Integration | W | `TheGoogleCallbackAddress_CarriesTheSecurityHeaders` | headers | RED |
| AC-002 | P-3 every endpoint × method, Owner/anonymous/before setup | Integration | P | `EveryEndpoint_CarriesTheSecurityHeaders` | headers | RED |
| AC-002 | P-4 pages incl. setup | Integration | P | `APage_Returns200_WithTheSecurityHeaders` | 200 + headers | RED |
| AC-002 | B-2 antiforgery page: `DENY` only | Integration | P | `APageWithAnAntiforgeryToken_CarriesOnlyXFrameOptionsDeny` | [`DENY`] | RED |
| AC-002 | P-4 redirects (sign-in, setup gate) | Integration | P | `ARedirect_CarriesTheSecurityHeaders` | 302 + headers | RED |
| AC-002 | N-1 404 page and `/api/v1` | Integration | P | `A404_CarriesTheSecurityHeaders` | 404 + headers | RED |
| AC-002 | N-1 antiforgery 400 | Integration | P | `AnAntiforgeryRefusal_CarriesTheSecurityHeaders` | 400 + headers | RED |
| AC-002 | N-1 exception handler 500 | Integration | P | `AnExceptionHandler500_CarriesTheSecurityHeaders` | 500 + headers | RED |
| AC-002 | P-4 every static file | Integration | P | `EveryStaticFile_CarriesTheSecurityHeaders` | 200 + headers | RED |
| AC-002 | N-1 missing static file | Integration | P | `AMissingFileUnderAStaticPath_CarriesTheSecurityHeaders` | 404 + headers | RED |
| AC-002 / AC-003 | P-5, S-1 CP policy strict, `form-action 'self'` only, no origin | Integration | P | `TheSentPolicy_IsStrict_AndAllowsNoFormTargetOutsideTheHost` | directive-by-directive | RED |
| AC-003 | P-5, S-1, B-3 installation policy strict, only Google origin in `form-action` | Integration | W | `TheSentPolicy_IsStrict` | directive-by-directive | RED |
| AC-004 | P-6 sign-in start redirects to the origin `form-action` allows | Integration | W | `TheGoogleSignInStart_RedirectsToTheOriginTheFormActionAllows` | 302 to `https://accounts.google.com`, headers, `form-action` = [`'self'`, that origin] | RED |
| AC-004 | manual real-browser sign-in | Manual | — | implementation report | Admin signed in, no CSP violation in the browser console | MANUAL (IMPLEMENTATION) |
| AC-005 | S-3 every view of both hosts | Source scan | I | `EveryView_CarriesNoInlineCode` | no violation | GREEN (guard) |
| AC-005 | S-3 scan reaches the script-including views | Source scan | I | `TheScan_ReachesTheViewsThatIncludeScripts` | listed | GREEN (guard) |
| AC-005 | S-5 scanner reports violations (incl. m-1 `<style>`) | Unit | I | `TheScan_ReportsInlineCode` | non-empty | GREEN (scanner) |
| AC-005 | S-5 scanner accepts compliant markup | Unit | I | `TheScan_AcceptsCompliantMarkup` | empty | GREEN (scanner) |
| AC-001 / AC-002 (FR-001) | S-2 no header named outside `Security` | Source scan | R | `NoSourceFileOutsideTheSecurityNamespace_NamesASecurityHeader` | none | GREEN (guard) |
| AC-001 / AC-002 (FR-001) | S-2 no `<meta http-equiv>` | Source scan | R | `NoView_SetsAHeaderThroughMeta` | none | GREEN (guard) |
| AC-006 (FR-007) | S-4 scripts run under the policy | Source scan | I | `EveryScript_RunsUnderTheStrictPolicy` | no refused construct | GREEN (guard) |
| AC-006 (FR-007) | S-5 script scanner positive / negative | Unit | I | `TheScriptScan_ReportsRefusedConstructs`, `TheScriptScan_AcceptsAllowedConstructs` | as named | GREEN (scanner) |
| AC-006 | whole existing suite | All | all | all | stays green | GREEN (regression) |
