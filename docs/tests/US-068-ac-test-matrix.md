---
artifact_type: ac_test_matrix
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
  - path: docs/tests/US-068-test-strategy.md
    version: 1
supersedes: null
---

# US-068 Acceptance Criteria → Test Matrix

Status: **Red** — fails now for the missing implementation (expected); **Guard** — passes now by design and must
stay green; **Existing** — an existing test the AC relies on unchanged; **Manual** — implementation report §3.1.
Classes: `BLR` = `Architecture/BootstrapLayoutRuleTests`, `VBT` = `Architecture/VendoredBootstrapTests`,
`EIO` = `Architecture/EmbeddedImageOverrideTests`, `ISL` = `Web/Pages/InstallationStylesheetLinkTests`,
`CSL` = `ControlPlane/Controllers/ControlPlaneStylesheetLinkTests`. "×2" = one row per host.

| AC | Scenario | Level | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 (a) | Layout links library then site.css, nothing else | Structural | BLR | `TheLayout_LinksTheLibraryThenTheSiteStylesheet_AndNothingElse` ×2 | exactly the two links in order | Red |
| AC-001 (a) | `_ViewStart` sets the shared layout | Structural | BLR | `TheViewStart_SetsTheSharedLayout` ×2 | assignment present | Guard |
| AC-001 (a) | No view assigns its own `Layout` | Structural | BLR | `NoView_AssignsItsOwnLayout` ×2 | none | Guard |
| AC-001 (b) | Dean sign-in, Admin `/`, Dean `/` link both in order | Integration | ISL | `APage_LinksTheLibraryThenTheSiteStylesheet` ×3 | 200, two links | Red |
| AC-001 (b) | Installation error page 400/403/404/500 | Integration | ISL | `TheErrorPage_LinksTheLibraryThenTheSiteStylesheet` ×4 | status kept, two links | Red |
| AC-001 (b) | CP sign-in, setup, Owner `/` and `/installations` | Integration | CSL | `TheSignInPage_…`, `TheSetupPage_…`, `AnOwnerPage_…` ×2 | 200, two links | Red |
| AC-001 (b) | CP error page 400/403/404/500 | Integration | CSL | `TheErrorPage_LinksTheLibraryThenTheSiteStylesheet` ×4 | status kept, two links | Red |
| AC-001 (b) | `bootstrap.min.css` served `text/css` | Integration | ISL, CSL | `ALibraryFile_IsServedWithItsContentType(bootstrap)` ×2 | 200 text/css | Red |
| AC-001 (b) | `site.css` served `text/css` | Integration | ISL, CSL | `ALibraryFile_IsServedWithItsContentType(site.css)` ×2 | 200 text/css | Guard |
| AC-001 (b) | Reader of `<head>` links | Unit | BLR | `TheStylesheetLinkReader_ReadsTheHeadInOrder` | head only, in order | Guard |
| AC-002 | Folder holds exactly three files | Structural | VBT | `TheLibraryFolder_HoldsExactlyTheThreeFiles` ×2 | `LICENSE.txt`, `VERSION.txt`, `css/bootstrap.min.css` | Red |
| AC-002 | No `*.js` / `*.map` | Structural | VBT | `TheLibraryFolder_HoldsNoScriptAndNoSourceMap` ×2 | none | Red (folder missing) |
| AC-002 | MIT licence naming Bootstrap | Structural | VBT | `TheLicence_IsTheMitLicenceOfBootstrap` ×2 | text present | Red |
| AC-002 | Note: version, archive URL, date, hash, "unmodified" | Structural | VBT | `TheVersionNote_RecordsVersionArchiveDateHashAndUnmodified` ×2 | all present | Red |
| AC-002 | SHA-384 equals recorded hash | Structural | VBT | `TheStylesheet_MatchesTheRecordedIntegrityHash` ×2 | equal | Red |
| AC-002 | Stylesheet header names the recorded version | Structural | VBT | `TheStylesheetHeader_NamesTheRecordedVersion` ×2 | equal | Red |
| AC-002 | Hosts byte-identical | Structural | VBT | `BothHosts_CarryTheSameBytes` | identical | Red |
| AC-002 | No view references a Bootstrap script | Structural | BLR | `NoView_ReferencesABootstrapScript` ×2 | none | Guard |
| AC-002 | `LICENSE.txt`, `VERSION.txt` served `text/plain` | Integration | ISL, CSL | `ALibraryFile_IsServedWithItsContentType(LICENSE/VERSION)` ×4 | 200 text/plain | Red |
| AC-003 | Headers on every static file incl. new ones | Integration | existing | both `SecurityHeadersResponseTests.EveryStaticFile_CarriesTheSecurityHeaders` | 200 + headers | Existing |
| AC-003 | Static files without `no-store` | Integration | existing | both `NoStoreResponseTests.AStaticFile_DoesNotCarryNoStore`, `UnknownFileLikePathTests` | 200, no no-store | Existing |
| AC-003 | CSP strict, unchanged | Integration/Structural | existing | `TheSentPolicy_IsStrict…` (both hosts), `SecurityHeadersRuleTests` | unchanged string | Existing |
| AC-004 | Every `data:` image in use overridden in site.css | Structural | EIO | `EveryEmbeddedImageInUse_IsOverriddenWithASameHostFile` ×2 | no missing pair | Red |
| AC-004 | Installation list holds select arrow + 3 check marks | Structural | EIO | `TheInstallationList_HoldsTheSelectArrowAndTheCheckMarks` | 4 selectors present | Red |
| AC-004 | Referenced images are plain same-host SVG | Structural | EIO | `EveryReferencedImage_IsAPlainSameHostSvg` ×2 | no script/data/image/external href | Guard (vacuous until site.css references images) |
| AC-004 | SVG scan not vacuous | Unit | EIO | `TheExternalReferenceScan_ReportsOnlyReferencesToOtherFiles` ×4 | as in data | Guard |
| AC-004 | Override images served `image/svg+xml` | Integration | ISL | `EveryOverrideImage_IsServedAsSvg` | ≥1 image, all 200 svg | Red |
| AC-004 | Override images served `image/svg+xml` (CP) | Integration | CSL | `EveryOverrideImage_IsServedAsSvg` | all 200 svg | Guard (CP may need none) |
| AC-004 | Stylesheet reader correctness | Unit | BLR | `TheStylesheetReader_FindsDeclarationsInsideMediaRulesAndSelectorLists` | as in data | Guard |
| AC-004 / AC-007 | No component the spec keeps out (FR-005.4/5) | Structural | BLR | `NoView_UsesAComponentTheSpecificationKeepsOut` ×2 | none | Guard |
| AC-005 | No inline style/script in any view | Structural | existing | `InlineCodeRuleTests` (29 cases) | pass unchanged | Existing |
| AC-006 | No text produced or transformed by CSS, no animation | Structural | BLR | `TheSiteStylesheet_AddsNoTextAndNoAnimation` ×2 | none | Guard |
| AC-006 | Visible texts unchanged | Integration | existing | full suite, no text assertion or `.resx` changed | green | Existing |
| AC-007 | Key pages look right, desktop and ~375 px | Manual | — | implementation report §3.1, Owner at HUMAN_PR_APPROVAL | recorded | Manual |
| AC-008 | No dark colour scheme in site.css | Structural | BLR | `TheSiteStylesheet_KeepsTheLightThemeOnly` (Web) | none | Red |
| AC-008 | Same, Control Plane | Structural | BLR | `TheSiteStylesheet_KeepsTheLightThemeOnly` (CP) | none | Guard |
| AC-008 | No `data-bs-theme` in views | Structural | BLR | `NoView_SetsABootstrapTheme` ×2 | none | Guard |

Every AC (AC-001…AC-008) has at least one mapped scenario; AC-007 is manual by the Specification (FR-011.2).
