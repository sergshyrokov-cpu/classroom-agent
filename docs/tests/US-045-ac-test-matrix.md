---
artifact_type: ac_test_matrix
story: US-045
version: 1
status: DRAFT
created_at: 2026-10-11T19:28:33Z
updated_at: 2026-10-11T19:28:33Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-045-spec.md
    version: 1
  - path: docs/designs/api/US-045-api-design.md
    version: 1
  - path: docs/designs/database/US-045-db-design.md
    version: 1
  - path: docs/tests/US-045-test-strategy.md
    version: 1
supersedes: null
---

# US-045 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests`):
`W` = `Web.Logging.RouteTemplateLoggingTests`,
`C` = `ControlPlane.Security.RouteTemplateLoggingTests`,
`A` = `Architecture.RouteTemplateLoggingRuleTests`,
`CC` = `Web.Logging.CourseCardLoggingTests` (US-021, unchanged).
Status is the red-phase result before IMPLEMENTATION.

| Acceptance Criterion | Scenario | Test Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | P-1, B-1, P-3: Meet code page with `zzq-path-probe` | Integration | W | `AMatchedPage_LogsItsRouteTemplate_AndNeverThePathValue` | in-endpoint `RequestPath` = `/workspace/meet-codes/{meetingCode}/course-choice`; before-routing = `(unmatched)`; request id present; value nowhere | RED (raw path logged) |
| AC-002 | N-1 signed in | Integration | W | `AnUnmatchedAddress_LogsTheMarker_AndTheErrorPageRoute_NeverTheValue(true)` | before-routing `(unmatched)`; error page `/error/{statusCode}`; value nowhere | RED |
| AC-002 | N-1 anonymous | Integration | W | `AnUnmatchedAddress_LogsTheMarker_AndTheErrorPageRoute_NeverTheValue(false)` | as above | RED |
| AC-002 | N-3 private port, fallback (I-2) | Integration | W | `AnUnmatchedAddressOnThePrivatePort_LogsTheMarker_EvenOnceTheFallbackHasAnswered` | before-routing and after-pipeline `(unmatched)`; value nowhere | RED |
| AC-002 | B-2 static file | Integration | W | `AStaticFile_LogsTheMarker_NotItsName` | after-pipeline `(unmatched)`; `site.css` nowhere | RED |
| AC-003 | P-2, B-1, P-3 matched with GUID value | Integration | C | `AMatchedPage_LogsItsRouteTemplate_AndNeverThePathValue` | in-endpoint `/installations/{id:guid}/admins/new`; GUID nowhere | RED |
| AC-003 | N-2 Owner / anonymous | Integration | C | `AnUnmatchedAddress_LogsTheMarker_AndTheErrorPageRoute_NeverTheValue(true/false)` | before-routing `(unmatched)`; error page `/error/{statusCode}`; value nowhere | RED |
| AC-003 | B-2 static file | Integration | C | `AStaticFile_LogsTheMarker_NotItsName` | after-pipeline `(unmatched)`; file name nowhere | RED |
| AC-004 | point fix type removed | Architecture | A | `TheRefusedValueRedaction_TypeNoLongerExists` | no type `RefusedValueRedaction` in the Web assembly | RED |
| AC-004 | point fix not referenced | Architecture | A | `NoProductionSource_NamesTheRefusedValueRedaction` | no `src/**/*.cs` names it | RED |
| AC-004 | guarantee kept | Integration | CC | all three methods, unchanged | refused course id never in the log file | GREEN (must stay green) |
| AC-005 | request id on every probe line; `RequestPath` present and well-formed on every request line (I-4) | Integration | W, C | `RouteProbe.AssertTheRuleAsync`, called by every W and C method | as stated | RED (with the scenarios above) |
| AC-005 | nothing else changes | Regression | whole suite | — | green after IMPLEMENTATION | GREEN before (only the new tests fail) |

Every AC maps to at least one test. No untested AC.
