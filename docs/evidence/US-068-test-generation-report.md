---
artifact_type: test_generation_report
story: US-068
version: 1
status: DRAFT
created_at: 2026-11-01T13:49:51Z
updated_at: 2026-11-01T13:49:51Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-068-spec.md
    version: 2
  - path: docs/decisions/US-068-open-decisions.md
    version: 2
  - path: docs/designs/api/US-068-api-design.md
    version: 1
  - path: docs/designs/database/US-068-db-design.md
    version: 1
  - path: docs/tests/US-068-test-strategy.md
    version: 1
  - path: docs/tests/US-068-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-068 Test Generation Report

## 1. Files

Created (tests):
- `tests/ClassroomAgent.Tests/TestInfrastructure/Styling.cs` — paths, stylesheet reader, view tokens, `<head>` links
- `tests/ClassroomAgent.Tests/TestInfrastructure/CssDeclaration.cs`
- `tests/ClassroomAgent.Tests/Architecture/BootstrapLayoutRuleTests.cs` (18 cases)
- `tests/ClassroomAgent.Tests/Architecture/VendoredBootstrapTests.cs` (13 cases)
- `tests/ClassroomAgent.Tests/Architecture/EmbeddedImageOverrideTests.cs` (9 cases)
- `tests/ClassroomAgent.Tests/Web/Pages/InstallationStylesheetLinkTests.cs` (12 cases)
- `tests/ClassroomAgent.Tests/ControlPlane/Controllers/ControlPlaneStylesheetLinkTests.cs` (13 cases)

Modified: none. Compile-only skeleton: none — the tests reference no new production declaration.

## 2. Commands and delegation

- Test design, helpers and all test code: written inline (the stylesheet reader and the AC-004 cross-check needed
  judgement; the HTTP classes are short).
- `dotnet build ClassroomAgent.sln` (inline): succeeded, 0 warnings, 0 errors.
- Test runs delegated to a `cheap-worker` subagent: one `dotnet test ClassroomAgent.sln --filter
  "FullyQualifiedName~<Class>"` per class for the five new classes and `InlineCodeRuleTests`. Docker running, no
  environment error (no Npgsql timeout, no Ryuk error).

## 3. Results (2026-11-01)

| Class | Total | Passed | Failed |
|---|---|---|---|
| BootstrapLayoutRuleTests | 18 | 15 | 3 |
| VendoredBootstrapTests | 13 | 0 | 13 |
| EmbeddedImageOverrideTests | 9 | 6 | 3 |
| InstallationStylesheetLinkTests | 12 | 1 | 11 |
| ControlPlaneStylesheetLinkTests | 13 | 2 | 11 |
| **New total** | **65** | **24** | **41** |
| InlineCodeRuleTests (existing, AC-005) | 29 | 29 | 0 |

### Expected failures (41) — each for missing implementation
- Layout link tests (2) and every rendered-page link test (installation 7, Control Plane 8): actual first link is
  `/css/site.css` — the library is not linked yet.
- `TheSiteStylesheet_KeepsTheLightThemeOnly` (installation): reports `:root { color-scheme: light dark }`, the rule
  FR-003.2 removes.
- All 13 `VendoredBootstrapTests`: `Missing folder/file … wwwroot/lib/bootstrap…`.
- `EmbeddedImageOverrideTests` 3: `bootstrap.min.css` missing.
- `ALibraryFile_IsServedWithItsContentType` for bootstrap/LICENSE/VERSION (3 per host): `404`.
- Installation `EveryOverrideImage_IsServedAsSvg`: `site.css` references no image yet (the test requires ≥1).

### Passing new tests (24) — why each is not a defect
- Guards that hold today by design and must stay green: `_ViewStart` sets the layout (2), no view assigns `Layout`
  (2), no Bootstrap script reference (2), no `data-bs-theme` (2), no kept-out component (2), `site.css` adds no
  text/animation (2), Control Plane light theme (1), `site.css` served `text/css` (2).
- Self-tests proving the helpers are not vacuous: stylesheet reader (1), `<head>` link reader (1), SVG external
  reference scan (4 rows).
- Vacuous today, meaningful after implementation: `EveryReferencedImage_IsAPlainSameHostSvg` (2) and Control Plane
  `EveryOverrideImage_IsServedAsSvg` (1) — `site.css` references no image yet. The installation's non-emptiness is
  forced by `EveryOverrideImage_IsServedAsSvg` (ISL) and `TheInstallationList_HoldsTheSelectArrowAndTheCheckMarks`;
  the Control Plane uses no select/checkbox/radio (spec §1.1), so an empty list there is correct.

Unexpected failures: none. Existing regression: `InlineCodeRuleTests` 29/29; the full suite was not run (no
production or existing test file changed; the new classes only add tests).

## 4. Coverage

Every AC has at least one mapped test (`docs/tests/US-068-ac-test-matrix.md`). AC-003 and AC-005 rest on existing
tests that enumerate every static file and every view and therefore cover the new files unchanged. AC-007 is manual
by the Specification (FR-011.2). Untested ACs: none.

## 5. Findings and notes

- INFO-1: `core.autocrlf=true` and `.gitattributes` does not cover `src/*/wwwroot/lib/**`; a fresh Windows checkout
  could rewrite `bootstrap.min.css` with CRLF and fail the pinned-hash test. IMPLEMENTATION should add a `-text`
  entry for the vendored files (supporting change).

Open Decisions: none new.

## 6. Overall result

PASS — red phase verified: 41 new tests fail only for the missing implementation, 24 pass by design (explained
above), no skeleton needed, build clean.
