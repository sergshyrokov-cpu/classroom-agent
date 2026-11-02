---
artifact_type: security_review
story: US-069
version: 1
status: APPROVED
created_at: 2026-11-02T13:12:08Z
updated_at: 2026-11-02T13:12:08Z
produced_by: security-reviewer
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
  - path: docs/evidence/US-069-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-069-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 2
security_sensitive: true
runtime_checks: PARTIAL
verdict: PASS
---

# US-069 Security Review

## 1. Executive Summary

**PASS: 0 Critical, 0 Major, 0 Minor, 2 Informational.** The Story adds a same-host script and two icons to the password
pages of both hosts, four anonymous pages among them. The script changes an input's `type` and the control's state and
nothing else: I read it line by line and it never touches the value, the network or storage. No endpoint, policy, schema,
password rule, sign-in check, audit event or header changed. Build clean; full suite 7121/7121 in this run, the first full
run after the format fix; no vulnerable package.

**Limitation:** nothing here ran in a browser. The listeners were read, not watched (the Specification sets it so), and the
Owner's walk (AC-011, steps 1-4) has not happened. This is code, configuration and test review, not penetration testing.

**For the Owner to weigh:** INFO-1, the eye can reveal a password the browser filled in by itself.

## 2. Reviewed Artifacts

The `inputs` above, all current, none superseded; `HUMAN_SPEC_APPROVAL` recorded for Specification v3. API and database
designs are NOT_APPLICABLE, so there is no OpenAPI contract or entity model. Also read: `trebovaniya.md` §8 «Показ пароля»,
`security-conventions.md`, the working-tree diff of `src/`, every new production file, and the tests named in section 14.

## 3. Security-Relevant Scope

| Host | Pages with the control | Access |
|---|---|---|
| Installation | Dean sign-in | anonymous (SC-4 list) |
| Installation | forced password change, own password | Dean |
| Installation | Dean accounts (row and create forms) | Admin |
| Control Plane | Owner sign-in, first-run setup | anonymous (SC-4 list), private network |

**Assets:** the password being typed. **Boundary:** the browser only: the control sends nothing to the server.
The other four items (role line, one title, `/database` headings, Meet prompt) show only translation-file text.

## 4. Environment and Tools

.NET SDK 10.0.401; Docker 29.8.0, Testcontainers available; free RAM 5.4 GB at the start.

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` | 0 | no vulnerable package in any of 7 projects |
| `dotnet test ClassroomAgent.sln` | see note | total 7121, passed 7121, failed 0, skipped 0; 10 min 56 s; no Docker or Npgsql error |

Note: the summary was confirmed in the saved output; the runner's own exit code was not captured separately.

**Delegated** to `cheap-worker`: the three commands above, facts only. Reading the code, every finding and the verdict stayed here.
**Unavailable:** a browser (none in the project's tooling; no application was started).

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role or policy changed. |
| SC-2 Authentication, «Showing a typed password» | PASS | Section 7. No controller or Application code changed, so password rules, the sign-in sequence, lockout, storage and logging cannot differ. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Google sign-in not touched. |
| SC-4 Authorization | PASS | The new files are static files: on the closed list, served by `UseStaticFiles` ahead of routing and authentication (`Web/Program.cs:157`, `ControlPlane/Program.cs:146`). No route added. No antiforgery exemption added; every form keeps its token. |
| SC-5 Read-only mode | NOT_APPLICABLE | No write use case changed. |
| SC-6 No DB UI | PASS | `/database` gains three headings only; no new data. |
| SC-7 Key | NOT_APPLICABLE | No secret handling touched. |
| SC-8 Google | NOT_APPLICABLE | No Google call touched. |
| SC-9 Channel | NOT_APPLICABLE | Not touched. |
| SC-10 Hygiene | PASS | Section 8. |
| SC-11 Audit | NOT_APPLICABLE | No audited action introduced or changed; none is required for a press of the control, which never reaches the server. |
| SC-12 Owner | PASS | Only texts and static files in the Control Plane; no `Contracts` change. |
| SC-13 Outbound | PASS | No network call in the script, no external URL in script, icons or CSS rules; `spellcheck="false"` on all 9 inputs from first render. |
| SC-14 No caching | PASS | Middleware unchanged; static files keep their exception (`NoStoreMiddleware.MarkStaticFile`, existing tests). |
| SC-15 Headers | PASS | `SecurityHeadersMiddleware` of both hosts not in the diff; `script-src 'self'`, `img-src 'self'` are enough; all six references are `<script src defer>` with no body; no `style` or `on*` attribute added. |

## 6. Authentication and Authorization

No declaration changed; the diff holds no controller. Anonymous pages load one more static file and gain no data: the script
and icons are identical for everyone and byte-identical between hosts.

## 7. Credentials, Key and Google Access

The script (`wwwroot/js/password-toggle.js`, 55 lines, identical in both hosts), read as FR-006 and the Specification's SC-4 and SC-14 items ask:

- **The value:** only `input.type` is read and written; `.value` appears nowhere. `getAttribute`/`setAttribute` touch `aria-pressed` and `aria-label` only, and the labels come from Razor-encoded data attributes through `setAttribute`, not `innerHTML`.
- **`submit` listener:** on `document`, bubbling, and it calls `hideAll` over every registered field. It runs before the browser builds the form data and before any form-history or password-manager step, as a dispatch handler must. No other script on these pages stops propagation: `rg stopPropagation|preventDefault` over the hosts' own `wwwroot/js` finds nothing, and none of the other scripts is referenced by the six views.
- **`pagehide` listener:** on `window`, the target the event is dispatched to, also `hideAll`. Covers a link, the address bar and "back". Under `no-store` it is the barrier for the rare page restore (FR-006.4).
- **A form that does not submit** (for example the browser's own validation stops it): no `submit` event fires, the page stays, and the field stays as the person left it. Nothing leaves the page, so this is correct.
- **Without the script:** the control stays `hidden`, no ready class, Edge's own reveal stays (`::-ms-reveal` only under `.password-field--ready`).
- **Coverage:** `rg 'type="password"'` over `src/` gives exactly the 9 inputs of the Specification, and none is rendered through a tag helper or `DataType.Password`, so no field lacks the control.
- **The request:** the `button` has no `name`, `value`, `form` or `id` and is `type="button"`, so it adds nothing and submits nothing. `TheControl_AddsNoField…` proves this on the rendered forms.

Password rules, service-account key and Google access: not touched.

## 8. Sensitive Data Exposure

- **Views:** no input gained a `value`; the Control Plane's `value=""` stays. `AfterARefused…` show a re-rendered field starts hidden.
- **Logs:** no logger call in the diff; the only C# change is three constants in `DatabaseTextKeys`.
- **The other items:** the role line and headings are translation text; the four removed `<h1>` held the same key as `ViewData["Title"]`, with no data in it; the Meet prompt has `value=""`, as the empty option had.
- **Exports, audit rows, telemetry:** not touched.

## 9. Input Validation

No input added; validation unchanged. Server-side rules do not see the control (Specification, Validation Rules).

## 10. API Security

No endpoint or response body changed. `AnEmptyCourseId_…` holds the no-course request at today's behaviour.

## 11. Persistence and Configuration

No entity, migration or configuration change; no `.csproj` or settings file in the diff.

## 12. Logging, Audit and Telemetry

No change. `docs/hooks/tool-usage.jsonl` remains git-ignored (`.gitignore:66`).

## 13. Dependencies

No package or project reference added: no `.csproj` in the diff. `--vulnerable --include-transitive` reports none, against nuget.org and the local SDK source.

## 14. Security Test Coverage

| Property | Test | Assessment |
|---|---|---|
| Script never reads the value, no network, storage, console | `TheScript_DoesNothingElse` + `TheScriptScan_ReportsAForbiddenConstruct` | Sound: the scan has its own non-vacuity cases. |
| Listeners on the right targets | `TheScript_HidesTheFields_…`, `TheSubmitCheck_…`, `ThePageHideCheck_…` | Proves they are registered; reading proves what they do; nothing proves a run (Specification). |
| The 9 fields' attributes | `AssertField`, `EveryPasswordField_…` | Sound. |
| No form field added | `TheControl_AddsNoField…` | Sound. |
| Headers unchanged | `SecurityHeadersResponseTests`, `InlineCodeRuleTests` | Unchanged and green. |
| No password in a response or log | `PasswordValueNotExposedTests`, `ControlPlanePasswordValueNotExposedTests` | Guards. See `US-069/IMPLEMENTATION_VERIFICATION/m-1`. |
| Static files served anonymously, with type | `AStaticFile_IsServedWithItsContentType` | Sound. |

TC-5's role tests stay as they were: no endpoint changed.

**`US-069/IMPLEMENTATION_VERIFICATION/m-1` from the security side:** the gap is real, the own-password guard has no status assert. It hides no risk this Story adds: `Own.cshtml` renders no `value`, and neither the controller nor any logger changed. It stays OPEN: closing it is a test change or the Owner's call.

## 15. Abuse Case Review

| Scenario | Expected | Evidence | Status |
|---|---|---|---|
| Script tampered with through another site or a CDN | Not possible from outside the host | Same-host file, `script-src 'self'`, no external reference | Protected |
| Markup injection through the control's names | Encoded | Translation text through Razor; `setAttribute` in the script | Protected |
| A malicious SVG run from the icons | No script in the file | Icons hold only `path` and `circle`; identical in both hosts; CSS background use | Protected |
| Shown password stored as plain text in form history | Hidden before submit | `submit` listener, section 7 | Protected by reading; not observed |
| Shown password restored on "back" | Hidden | `no-store` plus `pagehide`, section 7 | Protected by reading; outcome in steps 2, 3 |
| Shown password sent to a spell-check service | Off | `spellcheck="false"` from first render | Protected, as SC-2 asks; see section 21 |
| Someone at an unattended browser reveals a saved password | Not defined by any artifact | INFO-1 | Noted |
| Someone at an unattended page reads a shown password | Not defined by any artifact | The field stays shown until pressed or left; INFO-1 | Noted |

## 16. Repository Hygiene

No secret-like, database, `.xlsx` or IDE-local file among the changes; `rg` for private keys, client secrets and connection-string passwords over the new non-`docs` files finds nothing. `google_credentials.json` and `classroom_cache.db` remain ignored and were not opened.

## 17. Deviations

`D-1` (one padding rule outside the shared CSS) and `D-2` (`max-width` on the wrapper) are layout only, with no security effect. They stay OPEN for the Owner. No undisclosed security behaviour was found.

## 18. Findings

**INFO-1 (Informational, PASSWORD_HANDLING, SC-2).** *Affected:* `wwwroot/js/password-toggle.js`, both hosts.
*Observed:* the eye works on any value in the field, including one the browser filled in from its saved passwords, and a shown field stays shown until pressed again or the page is left. Edge's own reveal, which this control replaces on a ready field, is usually offered only for text just typed, as far as known from the browser's behaviour; not tested here.
*Risk:* on a shared computer with a saved password, anyone at the sign-in page could read that password with one press. This gives no new capability in the strict sense, since browser developer tools do the same, but it does make it easy. The sign-in itself needs no reveal: the saved password already signs that person in. The gain is knowing the password.
*Expected:* no artifact defines this. `trebovaniya.md` §8 says "введённый текст" and is silent on filled-in text. The behaviour conforms to the Specification.
*Correction:* none required. If the Owner wants it closed, that is a new item for `trebovaniya.md`, for example no reveal of a field the browser filled in, or hiding on leaving the field. Loop-back: none.

**INFO-2 (Informational, TEST_COVERAGE).** *Observed:* `TheScript_DoesNothingElse` scans for named constructs, so a new way of reading a value (for example a destructured `{ value } = input`) would pass it. *Risk:* none today: the script as written has none, as read here. *Correction:* none required; a later change to the script needs reading, not only the scan. Loop-back: none.

## 19. Positive Controls

Independently observed: the script's whole text; both hosts' script and icons identical; `hidden` control until the script runs; the fixed CSP strings untouched; static files ahead of authentication and without `no-store`, with the headers middleware ahead of them; 9 of 9 inputs carrying `spellcheck="false"` and `autocapitalize="off"`; `type="button"` with no name; full suite 7121/7121; no vulnerable package.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001…OD-005 are resolved; `trebovaniya.md` §7 holds no item this Story depends on.

## 21. Review Limitations

- No browser ran. That the listeners fire, that the eye shows and hides, and how Edge treats a shown field rest on reading the code and on the Owner's pending walk (`US-069/IMPLEMENTATION/INFO-1`).
- `spellcheck="false"` is what SC-2 asks. Whether Edge's other typing aids honour it on a shown field cannot be checked here.
- Dependency evidence covers known advisories in the two NuGet sources only.
- Not a penetration test.

## 22. Verdict Rationale

**PASS.** The report's build and test evidence is concrete and was reproduced here with a full green run. There is no Critical, Major or Minor finding; every touched SC item is PASS; the two things the Specification asked this review to read, the value handling and the `submit` and `pagehide` listeners, do what it says. Nothing here accepts a risk: INFO-1 is the Owner's to weigh at `HUMAN_PR_APPROVAL`.

Earlier OPEN findings handled here: none closed. `SPEC_REVIEW/m-4`, `IMPLEMENTATION/D-1`, `IMPLEMENTATION/INFO-1`, `IMPLEMENTATION_VERIFICATION/m-1`, `D-2` and `INFO-1` have no security effect, and closing them is not this stage's.

Next stage: `HUMAN_PR_APPROVAL`.
