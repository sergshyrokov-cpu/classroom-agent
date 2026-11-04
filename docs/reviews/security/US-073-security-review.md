---
artifact_type: security_review
story: US-073
version: 2
status: APPROVED
created_at: 2026-11-04T21:54:52Z
updated_at: 2026-11-04T22:09:45Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-073-show-password-only-for-typed-text.md
    version: null
  - path: docs/specifications/US-073-spec.md
    version: 4
  - path: docs/decisions/US-073-open-decisions.md
    version: 1
  - path: docs/designs/api/US-073-api-design.md
    version: 1
  - path: docs/designs/database/US-073-db-design.md
    version: 1
  - path: docs/tests/US-073-test-strategy.md
    version: 2
  - path: docs/tests/US-073-ac-test-matrix.md
    version: 2
  - path: docs/evidence/US-073-implementation-report.md
    version: 2
  - path: docs/reviews/implementation/US-073-implementation-verification.md
    version: 2
supersedes: docs/reviews/security/US-073-security-review.md@v1
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: PARTIAL
---

# US-073 Security Review

## 1. Executive Summary

**Verdict: PASS.** No Critical or Major finding; three Informational.

The script does what the Specification says, and its logic held on every path traced. Version 1 of this review was `BLOCKED` on M-1: with the field shown as text, the browser's preview of a saved password might be drawn in clear text. The Owner checked it on 2026-11-05 in Edge and Chrome with a really saved password: the preview is drawn as dots in both. M-1 is withdrawn (section 18).

Build clean, 7665 of 7665 tests, no vulnerable package, no change to any endpoint, policy, header, view, style or dependency. What remains for the Owner is the open list of the implementation stages, section 18.

## 2. Reviewed Artifacts

The versions in the front matter, the working-tree diff against `c0e3dfb`, `trebovaniya.md` v110 section 8, `security-conventions.md` SC-2.

## 3. Security-Relevant Scope

- **Functionality:** the eye control on 9 password fields in 6 views of both hosts. Only the two static scripts changed; the other four changed files are tests.
- **Asset:** a password saved in the browser, on a shared school computer.
- **Trust boundary:** the person at the keyboard against the browser's password store. No server boundary is touched.

## 4. Environment and Tools

.NET SDK 10.0.401; Docker 29.8.0 with Testcontainers. Commands run: `dotnet build ClassroomAgent.sln`, `dotnet test ClassroomAgent.sln`, `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive`, `git diff --stat HEAD` for csproj, props, json, cshtml and css. Chromium files read from its public source mirror, branch `main`: `password_autofill_agent.cc`, `html.css`, `text_control_element.cc`, `text_field_input_type.cc`, `html_input_element.cc`. This review drove no browser itself: the check of M-1 was made by the Owner on a temporary stand, section 22.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-2 Showing a typed password | PASS | Section 6; the preview route checked by the Owner, section 22 |
| SC-4 Authorization | PASS | No endpoint, policy or view changed; the same static file loads on anonymous pages |
| SC-10 Hygiene | PASS | No log, response or storage touched; no `console.` in the script |
| SC-13 Outbound | PASS | No network call in the script (`PasswordToggleSourceTests.TheScript_DoesNothingElse`, and read) |
| SC-15 Headers | PASS | No inline code; no CSP change |
| SC-1, SC-3, SC-5 to SC-9, SC-11, SC-12 | NOT_APPLICABLE | Nothing on the server changed |

## 6. Authentication and Authorization

No sign-in, lockout, password rule or role changed. The script was read for what the Specification asks of this stage:

- **Only route to `ENTERED`:** `onInput`, `field.awaitingEntry ? ENTERED : FILLED`, inside `isEntryKind(kind) && field.state === EMPTY`. The state is written only in `setState`. PASS.
- **Entry kinds:** exactly the closed list of FR-003. PASS.
- **Doubt falls to `FILLED`:** a throwing `:autofill`, a missing `inputType`, an unknown kind, text the script did not see appear, a browser with no `beforeinput` (no mark, so `FILLED`), text present at load or on return from the cache. PASS.
- **The value:** one comparison in `isEmpty`, nothing else. PASS.
- **Paths traced by hand, each without a reveal:** fill at load and later; a late fill raising no event, then an edit; delete to empty, then undo or redo; type, then undo; drop; a fill over typed hidden text; a press after each of these; a fill arriving with an empty-looking value; a return from the back-forward cache; a browser restoring a value after the script ran.
- **Hidden control:** Bootstrap's `[hidden]{display:none!important}` holds against `.password-toggle` on both hosts, which sets no `display`.

**The preview, which raises no event and which the script cannot see:** drawn as dots on a field shown as text, section 22.

## 7. Credentials, Key and Google Access

No password handling on the server, no key and no Google access changed. The withdrawn M-1 concerned the browser's store only.

## 8. Sensitive Data Exposure

Responses, DTOs, logs, audit rows, exceptions and exports are untouched. No exposure on the screen remains: section 22.

## 9. Input Validation

No input added and no validation changed. The control has no name or value and adds nothing to a request.

## 10. API Security

No endpoint changed. API design NOT_APPLICABLE, confirmed by the diff.

## 11. Persistence and Configuration

No entity, migration or configuration changed. Database design NOT_APPLICABLE, confirmed by the diff.

## 12. Logging, Audit and Telemetry

No log or audit call changed. `docs/hooks/tool-usage.jsonl` and `phase-timing.jsonl` stay git-ignored.

## 13. Dependencies

No package or project reference changed. `dotnet list package --vulnerable --include-transitive` against nuget.org reports no vulnerable package in any of the seven projects.

## 14. Security Test Coverage

Source tests fix the deciding functions as text; verification's mutation probe confirmed it. No test runs the script, by the Story's own terms; browser behaviour rests on the AC-008 walk. The check of the preview, section 22, was also made by a person.

## 15. Abuse Case Review

| Scenario | Expected | Status |
|---|---|---|
| A saved password fills at load, the next person presses anything | No control | PASS (read; walked) |
| Empty, undo, press | Hidden, no control | PASS (read; walked) |
| Type a character, press the eye, hover the saved suggestion in the login field's list | Saved password not readable | PASS: dots in Edge and Chrome, section 22 |
| Type a character, leave it hidden, fill, edit, press | No reveal | PASS (read; walked on the installation) |

## 16. Repository Hygiene

No secret-like file, database or export in the change set. `classroom_cache.db` and `google_credentials.json` remain ignored; no credential file was opened.

## 17. Deviations

No undisclosed deviation from the Specification. D-1 to D-5 are in the implementation report and verification.

## 18. Findings

### M-1 (Major) WITHDRAWN: the browser's preview of a saved password

Raised in version 1 from Chromium's source: `DoPreviewField` sets a suggested value with no event, and the only mask rule read in `html.css` applies to `type="password"`. **The reading was wrong or incomplete.** In the running browsers the preview on a field shown as text is drawn as dots, section 22. The finding stays in the ledger, closed `WITHDRAWN`.

### INFO-1 An updated script can come from the browser's cache

`password-toggle.js` is referenced with no version, and static files are the one exception to `no-store`, sent with no `Cache-Control`. A browser may reuse an earlier copy for a while after an update. Today this has no effect, since no school runs the US-069 version. It matters once a later Story changes a security script after the pilot starts.

### INFO-2 Browsers and filling outside the walk

Firefox, Safari and filling on a phone were not walked, as the Story provides. On those a fill reported as an entry kind and not matching `:autofill` would count as entered. No school policy on browsers exists; recorded for the base version.

### INFO-3 A stale comment

`_PasswordToggle.cshtml` of both hosts says the script reveals the control, which is now true only in `ENTERED`. No effect on behaviour.

### Open findings of the Story

None closed here. IMPLEMENTATION/m-1 to m-5, IMPLEMENTATION_VERIFICATION/m-2 and D-4 stay OPEN for the Owner. From the security side: m-2 is on the safe side as read; IMPLEMENTATION_VERIFICATION/m-2 agrees with this reading, since `hideAll`, `fields.push` and the listeners are correct today; m-3 leaks nothing.

## 19. Positive Controls

The state machine of section 6; two identical host scripts; the initial `hidden` in the partial; no value read beyond `isEmpty`; no network, storage or console; the ready class hiding Edge's own reveal button; the full suite run independently: 7665 of 7665, 0 skipped, 12 min 11 s, ended 2026-11-04T21:52:19Z.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- Code, configuration and test review plus a source reading of Chromium; no penetration test.
- This review ran no browser itself. The preview rests on the Owner's check: Edge and Chrome 156, on the installation's sign-in page only (the two hosts' scripts are identical), one result per browser for the hover and the arrow key together. The dots come from the browser, not from this project's code, so a later browser version could differ. No test can watch this.
- The two earlier flaky tests (IMPLEMENTATION/m-5) did not fail in this run; that does not show they are fixed.
- Build, tests and package check were delegated to `cheap-worker`, and two Chromium files were first summarised by `web-fetch`; the lines this finding rests on were then read directly. Reading the code, the findings and the verdict stayed in this Skill.

## 22. Verdict Rationale

**The check of M-1.** The Owner, 2026-11-05, Edge 156.0.4314.58 and Chrome 156.0.8078.80, clean temporary profiles, a synthetic password saved through each browser's own prompt, on `https://localhost:5443/sign-in`: one character typed into the empty password field, the eye pressed so the character was readable, the login field clicked, the saved suggestion hovered and then reached by arrow key, not chosen. **Result in both browsers: dots in place of the character.**

**Stand cleanup:** the temporary test class is deleted, the browser profiles are deleted, and the certificate's trust is removed. No production code or test changed between the full run in section 19 and this version, so that run stands.

No Critical or Major finding remains, every touched SC item is `PASS`, the required tests pass, and no security Open Decision is open. `PASS`.

```yaml
result:
  verdict: PASS
  stage: SECURITY_REVIEW
  story: US-073
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/security/US-073-security-review.md
  next_stage: HUMAN_PR_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "INFO-1: The script is referenced with no version and static files have no Cache-Control, so an update may come late; no effect before a later change to a security script."
    - "INFO-2: Firefox, Safari and filling on a phone were not walked, as the Story provides; a fill reported as an entry kind there would count as entered."
    - "INFO-3: The comment in _PasswordToggle.cshtml of both hosts still says the script reveals the control."
  closed_findings:
    - id: US-073/SECURITY_REVIEW/M-1
      resolution: WITHDRAWN
      note: "The Owner's check in Edge and Chrome: the preview on a field shown as text is drawn as dots."
      ref: docs/reviews/security/US-073-security-review.md
```
