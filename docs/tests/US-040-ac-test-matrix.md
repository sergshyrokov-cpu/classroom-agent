---
artifact_type: ac_test_matrix
story: US-040
version: 1
status: DRAFT
created_at: 2026-10-05T06:47:34Z
updated_at: 2026-10-05T06:47:34Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-040-spec.md
    version: 2
  - path: docs/designs/api/US-040-openapi.yaml
    version: 1
  - path: docs/tests/US-040-test-strategy.md
    version: 1
supersedes: null
---

# US-040 Acceptance Criteria → Test Matrix

Classes: **W** = `ClassroomAgent.Tests.Web.Security.NoStoreResponseTests`,
**C** = `ClassroomAgent.Tests.ControlPlane.Security.NoStoreResponseTests`,
**A** = `ClassroomAgent.Tests.Architecture.NoStoreRuleTests`.

Status is the red-phase state before IMPLEMENTATION: **RED** fails for the
missing rule (header absent); **GREEN-PRE** already passes — the framework
already writes a value containing `no-store` on that response (FR-006), or the
test is a guard (see the test-generation report §4).

| AC | Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|---|
| AC-001 | every endpoint, public port, Admin / Dean / anonymous | Integration | W | `EveryEndpoint_OnThePublicPort_CarriesNoStore` (3 rows) | no response without `no-store` | RED |
| AC-001 | every endpoint, private port | Integration | W | `EveryEndpoint_OnThePrivatePort_CarriesNoStore` | same | RED |
| AC-001 | signed-in pages (Admin, Dean) and anonymous sign-in page | Integration | W | `APage_Returns200_WithNoStore` (3 rows) | `200` + `no-store` | GREEN-PRE |
| AC-001 | journal with student data | Integration | W | `TheJournal_Returns200_WithNoStore` | `200` + `no-store` | GREEN-PRE |
| AC-001 | redirect to sign-in | Integration | W | `TheRedirectToSignIn_CarriesNoStore` | `302 /sign-in` + `no-store` | RED |
| AC-001 | HTTPS redirection | Integration | W | `TheHttpsRedirection_CarriesNoStore` | `3xx` + `no-store` | RED |
| AC-001 | `403` | Integration | W | `A403Refusal_CarriesNoStore` | `403` + `no-store` | RED |
| AC-001 | `404`, page and `/api/v1` | Integration | W | `A404_CarriesNoStore` (2 rows) | `404` + `no-store` | RED |
| AC-001 | antiforgery `400` | Integration | W | `AnAntiforgeryRefusal_CarriesNoStore` | `400` + `no-store` | GREEN-PRE |
| AC-001 | read-only `409` | Integration | W | `AReadOnlyRefusal_CarriesNoStore` | `409` + `no-store` | GREEN-PRE |
| AC-001 | exception-handler `500` | Integration | W | `AnExceptionHandler500_CarriesNoStore` | `500` + `no-store` | GREEN-PRE |
| AC-002 | every endpoint, Owner / anonymous / before setup | Integration | C | `EveryEndpoint_CarriesNoStore` (3 rows) | no response without `no-store` | RED |
| AC-002 | Owner pages, sign-in, setup | Integration | C | `APage_Returns200_WithNoStore` (4 rows) | `200` + `no-store` | GREEN-PRE |
| AC-002 | redirect to sign-in, setup-gate redirect | Integration | C | `ARedirect_CarriesNoStore` (2 rows) | `302` + `no-store` | RED |
| AC-002 | `404`, page and `/api/v1` | Integration | C | `A404_CarriesNoStore` (2 rows) | `404` + `no-store` | RED |
| AC-002 | antiforgery `400` | Integration | C | `AnAntiforgeryRefusal_CarriesNoStore` | `400` + `no-store` | GREEN-PRE |
| AC-002 | exception-handler `500` | Integration | C | `AnExceptionHandler500_CarriesNoStore` | `500` + `no-store` | GREEN-PRE |
| AC-003 | every `wwwroot` file | Integration | W | `AStaticFile_DoesNotCarryNoStore` | `200`, no `no-store` | GREEN-PRE |
| AC-003 | every `wwwroot` file | Integration | C | `AStaticFile_DoesNotCarryNoStore` | `200`, no `no-store` | GREEN-PRE |
| AC-003 | missing file under `/css` is not exempt | Integration | W | `AMissingFileUnderAStaticPath_CarriesNoStore` | not `200` + `no-store` | RED |
| AC-003 | missing file under `/css` is not exempt | Integration | C | `AMissingFileUnderAStaticPath_CarriesNoStore` | `404` + `no-store` | RED |
| AC-004 | enumeration per host fails on a later page without the header | Integration | W, C | the four `EveryEndpoint_*` tests | as above | RED |
| AC-004 | no per-page `[ResponseCache]` | Architecture | A | `NoTypeOrMember_CarriesAResponseCacheAttribute` (2 rows) | none found | GREEN-PRE (guard) |
| AC-005 | existing suite unchanged | Integration/all | all existing | full `dotnet test` | green | see report §3 |

Every Acceptance Criterion has at least one mapped test; AC-001 … AC-004 each
have at least one RED test.
