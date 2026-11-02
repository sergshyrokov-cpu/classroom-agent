---
artifact_type: implementation_report
story: US-069
version: 1
status: DRAFT
created_at: 2026-11-02T12:51:13Z
updated_at: 2026-11-02T18:13:31Z
produced_by: dotnet-implementor
attempt: 1
inputs:
  - path: docs/stories/US-069-small-interface-texts-and-show-password.md
    version: null
  - path: docs/specifications/US-069-spec.md
    version: 3
  - path: docs/decisions/US-069-open-decisions.md
    version: 1
  - path: docs/designs/api/US-069-api-design.md
    version: 1
  - path: docs/designs/database/US-069-db-design.md
    version: 1
  - path: docs/tests/US-069-test-strategy.md
    version: 1
  - path: docs/tests/US-069-ac-test-matrix.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-069 Implementation Report

## 1. Summary

All five items are in: «Роль: …», one page title, named `/database` blocks, the Meet reports prompt, and the eye control on all 9
password fields in both hosts. No endpoint, schema, policy, form field, audit event or export changed. No package added.

The build is clean and the format check passes. The full suite gave 7120 of 7121: the one failure, `AMark_RoundTrips`, is the known flaky
test and passed on rerun. AC-011 and manual steps 1-4 were walked by the Owner on 2026-11-02, with no defect found and one recorded limit in step 4 (§3.1).

Security-sensitive because anonymous sign-in and setup pages now load a script for the first time. The script switches the type and
never touches the value; SECURITY_REVIEW reads it, as the Specification asks.

## 2. Source Artifacts

See `inputs` above: Story, Specification v3, Open Decisions v1 (all resolved), API and database designs v1 (both NOT_APPLICABLE, so no OpenAPI
contract, entity model or migration), test strategy v1, AC matrix v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Test | Status |
|---|---|---|---|
| AC-001 | `Views/Home/Index.cshtml` `#signed-in-role`; key `Landing.RoleLine` | `SmallTextsPageTests.TheLandingPage_ShowsTheRoleInOneText` | passing |
| AC-002 | own `<h1>` removed from `MeetCodes/Index`, `MeetCodes/CourseChoice`, `Erasure/Index`, `Erasure/Confirmation` | `SmallTextsPageTests.EachPage_HasExactlyOneTitle_TheOwnOne`, `OnePageTitleRuleTests`, adapted `PageSearchHintTests` | passing |
| AC-003 | `Views/Database/Index.cshtml`; `DatabaseTextKeys.Block*` | `SmallTextsPageTests.TheDatabasePage_NamesItsThreeBlocks`, `EveryCounter_StaysInItsBlock` | passing |
| AC-004 | `Views/MeetReports/Index.cshtml` first option, existing key `Journal.Form.ChooseCourse` | `SmallTextsPageTests.TheCoursePicker_StartsWithThePrompt`, `WithAChosenCourse_…`, `AnEmptyCourseId_…` | passing |
| AC-005 | `_PasswordToggle.cshtml` per host, the 6 views, `js/password-toggle.js` | `PasswordToggleMarkupTests`, `ControlPlanePasswordToggleMarkupTests`, `PasswordToggleSourceTests` | automated part passing; manual step 1 passed |
| AC-006 | `spellcheck`, `autocapitalize` in the markup; `submit` and `pagehide` listeners | the same; `TheScript_HidesTheFields_OnSubmitAndOnPageHide` | automated part passing; manual steps 2, 3 passed, for the outcome only |
| AC-007 | control rendered `hidden`; ready class added only by the script; `::-ms-reveal` only under the ready class | `WithoutTheScript_…`, `NoRule_DefeatsTheHiddenAttribute`, `EdgesRevealButton_…` | automated part passing; manual step 4 passed, with the limit in §3.1 |
| AC-008 | same-host script and icons, no inline code, CSP untouched | `TheControl_AddsNoField…`, `TheScript_DoesNothingElse`, header tests and `InlineCodeRuleTests` unchanged | passing |
| AC-009 | nothing in the password path changed | `PasswordValueNotExposedTests`, `ControlPlanePasswordValueNotExposedTests`, existing tests unchanged | passing |
| AC-010 | six keys in the installation's files, two in the Control Plane's, uk and en | `SmallTextsTranslationTests`, `PasswordToggleTranslationTests` | passing |
| AC-011 | `site.css`, icons | manual | passed by the Owner (§3.1) |

### 3.1 Manual walk

Walked by the Owner on 2026-11-02, on a temporary stand with synthetic data only. The stand was a temporary test class, removed after the walk and in no commit.

| Field | Value |
|---|---|
| Browser | Microsoft Edge 156.0.4314.58 (64-bit) |
| Widths | desktop width and DevTools device mode 375 px |

**AC-011**

| Page | Desktop | 375 px | Console |
|---|---|---|---|
| Dean sign-in | one eye, text not under it | same; no page scroll | no red messages |
| Control Plane setup (not required by AC-011) | same, each eye drives its own field | same | no red messages on the successful submit |
| Control Plane sign-in | same | same | no red messages |
| Dean accounts: row field and create form | same with a long password; fields aligned | same; only the table scrolls sideways, not the page | no CSP message; the 409 in step 4 below |
| Landing | «Роль: Адміністратор школи» (Admin), «Роль: Декан» (Dean) | Admin only: line readable, no page scroll | no problem reported, Admin |
| `/database` | three named blocks in a row | stacked; `scrollWidth` 375 = `clientWidth` 375 | `No issues` (screenshot, desktop) |

At 375 px the page moved slightly only while the mouse button was held and returned on release: device-mode overscroll, since the measurement shows no extra width. The D-1 padding rule held: a long password in a row field stays clear of the eye.

**Steps of the Specification**

| Step | Page | Result |
|---|---|---|
| 1. Show, hide, keyboard | Dean sign-in, a Dean-accounts row | Text shows, hides and is kept; Tab reaches the control, Enter and Space work; no submit |
| 2. Show, submit a wrong password, back | Dean sign-in | Common refusal message; after "back" the field is empty |
| 3. Show, leave by the address bar, back | a Dean-accounts row | The field came back **with its text, hidden** |
| 4. Scripts off | Dean sign-in | No control of ours; Edge's own reveal appears only while the field has text and focus; sign-in succeeded |
| 4. Scripts off | a Dean-accounts row | No control; the form submitted and reached the server, which answered 409 because the stand is in read-only mode |

**Limit of step 4.** The password reset did not complete on the stand: the 409 is the correct SC-5 refusal, and the row's end-to-end path was not seen with scripts off. The full path with scripts off was seen on the Dean sign-in page.

**Step 3, observation.** Edge most likely restored the page from memory despite `no-store`, the case FR-006.4 keeps the `pagehide` listener for. The field left shown and came back hidden. This is an inference from the outcome: nothing measured how the page was restored.

**Expected red console lines, neither a CSP message:** `POST /settings/deans/1/password 409` (step 4, read-only stand), and `POST /setup 409` when the Owner submitted setup a second time from an InPrivate window after creating the account (SC-2, one Owner account).

**Stand limits, not product behaviour.** The stand's clock is frozen at its start, so a Google sign-in fails after 15 minutes: the correlation cookie is issued already expired (seen in the response headers). The stand was restarted once for this, and Google was the local stub. The ASP.NET Core development certificate was trusted in Windows for this walk and turned out not to be needed.

## 4. Change Set

Production, under `src/`:

| File | Trace |
|---|---|
| `ClassroomAgent.Web/Views/Home/Index.cshtml` | FR-001 |
| `ClassroomAgent.Web/Views/MeetCodes/Index.cshtml`, `MeetCodes/CourseChoice.cshtml`, `Erasure/Index.cshtml`, `Erasure/Confirmation.cshtml` | FR-002 |
| `ClassroomAgent.Web/Views/Database/Index.cshtml`, `ClassroomAgent.Web/Security/DatabaseTextKeys.cs` | FR-003 |
| `ClassroomAgent.Web/Views/MeetReports/Index.cshtml` | FR-004 |
| `ClassroomAgent.Web/Views/Shared/_PasswordToggle.cshtml`, `ClassroomAgent.ControlPlane/Views/Shared/_PasswordToggle.cshtml` | FR-005, FR-005.2 |
| `ClassroomAgent.Web/Views/SignIn/Index.cshtml`, `DeanPassword/Forced.cshtml`, `DeanPassword/Own.cshtml`, `DeanAccounts/Index.cshtml` | FR-005, FR-005.5 |
| `ClassroomAgent.ControlPlane/Views/Setup/Index.cshtml`, `SignIn/Index.cshtml` | FR-005, FR-005.5 |
| `ClassroomAgent.Web/wwwroot/js/password-toggle.js`, `ClassroomAgent.ControlPlane/wwwroot/js/password-toggle.js` | FR-006 |
| `wwwroot/img/password/eye.svg`, `eye-off.svg` in both hosts | FR-008.1 |
| `wwwroot/css/site.css` in both hosts | FR-006.1, FR-008 |
| `ClassroomAgent.Application/Localization/SharedResource.{uk,en}.resx` | FR-001, FR-003, FR-005.4, AC-010 |
| `ClassroomAgent.ControlPlane/Localization/SharedResource.{uk,en}.resx` | FR-005.4, AC-010 |

Tests and test artifacts: as listed in `docs/evidence/US-069-test-generation-report.md`; none was changed at this stage except whitespace in `PasswordToggleMarkupTests.cs`.

No secret, database file, IDE-local config or migration is in the change set.

## 5. Validation Evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln` (free RAM 7.14 GB) | 2 | total 7121, failed 1, passed 7120, skipped 0; 10 min 39 s |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | 2 | 21 whitespace errors, all in `PasswordToggleMarkupTests.cs` |
| `dotnet format ClassroomAgent.sln --include …/PasswordToggleMarkupTests.cs` | 0 | applied |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | clean |
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `--filter FullyQualifiedName~MeetingCodeLinkRepositoryTests` | 0 | 10/10, `AMark_RoundTrips` included |
| `--filter FullyQualifiedName~.PasswordToggleMarkupTests.` | 0 | 29/29 |

The single failure: `MeetingCodeLinkRepositoryTests.AMark_RoundTrips`, `Assert.NotNull() Failure: Value is null`, line 103. It touches nothing this Story changed and is recorded as flaky since US-043. The
full suite was not rerun after the format fix, which changed only whitespace in one test file.

**Baseline.** TEST_WRITING's full run: 7121 total, 81 red for missing behaviour, all others green. All 81 are green now.

**Delegated** to `cheap-worker`: every build, test and format run. **Kept inline, with the reason:** the script (the security rule), `site.css` and the view edits, applied by one script that checked every
anchor before writing; a brief for those edits would have been as long as the edits themselves.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

- **D-1.** FR-008.3 puts the control's rules in the part of `site.css` shared between hosts. One rule is in the installation's Dean-accounts section instead: `#dean-accounts td:last-child .password-field--ready .form-control { padding-right: 2.25rem; }`.
  The row's own compact `padding` has higher specificity and would otherwise put the text under the eye. It concerns a page only the installation has, and names no `.password-toggle` rule, so the shared part is still word for word.
- **D-2.** `.password-field` also carries `max-width: var(--content-width)`, the limit `.page .form-control` sets on the input: otherwise a wide wrapper would put the eye outside a capped field. Within FR-008.3 ("keep the width").

## 8. Open Decisions

None touched or newly required. `US-069/SPEC_REVIEW/m-4` (the digest's wording) stays OPEN: the digest is not this stage's artifact.
