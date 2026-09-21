---
artifact_type: security_review
story: US-010
version: 1
status: APPROVED
created_at: 2026-09-21T01:05:00Z
updated_at: 2026-09-21T05:12:48Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-010-connection-instructions.md
    version: null
  - path: docs/specifications/US-010-spec.md
    version: 1
  - path: docs/decisions/US-010-open-decisions.md
    version: 1
  - path: docs/designs/api/US-010-api-design.md
    version: 1
  - path: docs/designs/api/US-010-openapi.yaml
    version: 1
  - path: docs/designs/database/US-010-db-design.md
    version: 1
  - path: docs/tests/US-010-test-strategy.md
    version: 1
  - path: docs/tests/US-010-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-010-implementation-report.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 1
informational_findings: 5
security_sensitive: true
runtime_checks: PARTIAL
---

# US-010 Security Review — Connection instructions for the school super-admin

## 1. Executive Summary

**Verdict: PASS.** 0 Critical, 0 Major, 1 Minor, 5 Informational.

US-010 adds one authenticated `GET` that renders configuration values already
stored in the installation's own database. It is the smallest attack surface any
Story in this project has added, and the review confirmed that independently
rather than accepting it from the Implementation Report:

- the page **writes nothing** — no audit row, no table, in any mode. Verified
  structurally: the query's only dependency is `ILegitimacyStateRepository`, it
  calls `GetForReadAsync` (untracked), and no `IUnitOfWork`,
  `IAuditEventRepository`, `IReadOnlyModeGuard` or `IGoogleDataPort` appears
  anywhere in the call path;
- **no Google call and no Control Plane call** happens while rendering, in any
  mode. This Story adds no Google port at all, so the US-007 finding F-5 moves on
  to US-011 untouched;
- the **two identifiers §6 (v78) warns against confusing** with the service
  account's client ID are absent by construction: the view model has no field for
  either, the controller reads no configuration, and tests assert the configured
  OAuth client id and the `Installation` UUID appear nowhere in the response;
- **no secret** reaches the response, the view model or the copied text, and
  nothing in this Story could carry one: the new code touches no
  `IConfiguration`, no `ISecretStore` and no environment variable;
- the **SC-4 anonymous closed list gains nothing** — verified by enumerating
  `AllowAnonymous` in the host: the five sites are all pre-existing (Dean
  sign-in, the fallback catch-all, the error page and the two private endpoints);
- **PermittedServiceWrite still has exactly four members**, and the BR-026 closed
  list is unchanged;
- **no migration, no package, no project reference, no configuration change** —
  `git diff` over `*.csproj`/`*.sln` is empty and the migrations directory is
  untouched.

The single Minor finding is inherited rather than new: the forbidden-role half of
the new page's authorization is proven against the real policy object with a
synthetic Dean principal, because no Dean can sign in until US-012.

The Story's security value is in what it refuses to print. OD-001 keeps every
Google Workspace admin-role name out of the instruction while `trebovaniya.md` §7
item 10 is unverified, and the implementation enforces that with tests over both
translation files **and** the rendered page — so a well-meaning translation change
fails a test instead of leading a school to over-grant privileges to an account
that reads students' data.

Recommended next action: proceed to `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-010-connection-instructions.md` | authored |
| Specification | `docs/specifications/US-010-spec.md` | 1 (APPROVED) |
| Open Decisions | `docs/decisions/US-010-open-decisions.md` | 1 |
| API design | `docs/designs/api/US-010-api-design.md` | 1 |
| OpenAPI | `docs/designs/api/US-010-openapi.yaml` | 1 |
| Database design | `docs/designs/database/US-010-db-design.md` | 1 (NOT_APPLICABLE) |
| Test strategy | `docs/tests/US-010-test-strategy.md` | 1 |
| AC → test matrix | `docs/tests/US-010-ac-test-matrix.md` | 1 |
| Implementation report | `docs/evidence/US-010-implementation-report.md` | 1 (PASS) |
| Requirements | `trebovaniya.md` | 79 |

No input is `SUPERSEDED`. `HUMAN_SPEC_APPROVAL` is recorded
(2026-09-20T19:54:41Z). There is no `entity_model`: DB_DESIGN returned
`NOT_APPLICABLE` and its §3 was read in its place, as the stage instructed.

## 3. Security-Relevant Scope

**Exposed functionality.** One operation on the installation's public port:
`GET /settings/connection-instruction`, policy `ViewConnectionInstruction`
(Admin only). The Control Plane gains nothing; the private port is untouched.

**Assets touched.** The `Installation` domain and the service account's numeric
client ID, both from `LegitimacyState` — the two **non-secret** values §1 and
BR-032 allow to cross from the Owner to a school. No personal data, no password
hash, no token, no key.

**Trust boundaries crossed.** Browser → installation host (public HTTPS, session
cookie); controller → `Application` query → `ILegitimacyStateRepository` →
PostgreSQL. **Not** crossed: Google, the Control Plane service channel, the
secret store, the report renderer, any other outbound destination.

**Security components affected.** One new authorization policy; one new static
JavaScript file; eighteen translation entries; one shared read rule
(`ConfirmedLegitimacy`) that US-009 now also uses.

## 4. Environment and Tools

| Item | Value |
|---|---|
| .NET SDK | 10.0.401 |
| Docker / Testcontainers | available; integration tests ran against real PostgreSQL |
| `dotnet build ClassroomAgent.sln` | 0 errors, 0 warnings |
| `dotnet test ClassroomAgent.sln` | **1791 passed, 0 failed, 0 skipped** — re-run independently by this review |
| `dotnet list package --vulnerable` | no vulnerable packages in any of the seven projects |
| `git diff` / `git status` | change set and repository hygiene inspected |

Not performed: penetration testing, fuzzing, browser-level execution of the copy
script (see §21).

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | **PASS** | `InstallationPolicies` gains exactly one constant, for the §2 row "Просмотр инструкции по подключению" (✔ Admin, ✘ Dean, v39). `AppRole` unchanged — no Teacher, no Student. No other matrix cell implemented. |
| SC-2 Authentication | **PASS** (unaffected) | No change to Identity, passwords, cookies, HTTPS redirection or HSTS. `git diff` over `Security/` is 13 added lines: the policy and the route constant, nothing else. |
| SC-3 AllowedAdmin | **NOT_APPLICABLE** | No login path is touched. |
| SC-4 Authorization | **PASS** | `[Authorize(Policy = ViewConnectionInstruction)]` on `ConnectionInstructionController`; the deny-by-default fallback is unchanged; the five `AllowAnonymous` sites in the host are all pre-existing and on the SC-4 list. The route accepts only `GET` (`TheEndpoint_AcceptsOnlyGet`), so no antiforgery exemption is needed and none was added. No state-changing action is reachable by `GET`: the operation writes nothing at all. |
| SC-5 Read-only mode | **PASS** | Nothing is written, so no guard is required — and BR-026 names the instruction as viewable. All three BR-025 causes answer `200` with the reason in the body, never `409` (`InReadOnlyMode_TheAnswerIsNeverAConflict`, 3 cases). `PermittedServiceWrite` still has four members. |
| SC-6 No DB UI | **PASS** | No diagnostic or database endpoint added. |
| SC-7 Key | **PASS** | The new code reads no configuration, no secret store and no environment variable (verified by search). The view model has no field that could carry a key, a reference or the OAuth client secret. `TheHandedOverText_CarriesNoSecret` and `ThePage_CarriesNoSecretAnywhere` assert the synthetic secret and its reference appear nowhere. Data Protection configuration untouched. |
| SC-8 Google | **PASS** | No Google port, no SDK type, no runtime scope request, no call — in any mode. The six scopes rendered are §6's exactly, as text; `AForbiddenScope_AppearsNowhere` (5 cases) asserts `drive.file`, `classroom.profile.photos` and the three identity scopes appear nowhere. Every scope in the constant is read-only. |
| SC-9 Channel | **PASS** | No Control Plane call while rendering; the values come from the installation's own `legitimacy_state`. No `Contracts` type added; `ContractVersion` unchanged. |
| SC-10 Hygiene | **PASS** | No log statement added, so nothing new is logged. No `Html.Raw` or equivalent bypass in the new view; every value is HTML-encoded (see F-2). No query string is echoed (`AQueryStringIsIgnored`). |
| SC-11 Audit | **PASS** | Viewing is not an audited action in §5, and no audit row is written — asserted by comparing every table's row count before and after three renders, in normal mode, in each read-only cause and with no `legitimacy_state` row. |
| SC-12 Owner | **PASS** | Nothing is reported to the Control Plane; `ControlPlane` still does not reference `Domain`. |
| SC-13 Outbound | **PASS** | The only external destination reachable from this Story's code is the installation's own database. The copy script makes no network call of any kind — verified by searching it for `fetch`, `XMLHttpRequest`, `sendBeacon`, `WebSocket` and any `src`/`import`: none present. |

## 6. Authentication and Authorization

The policy is **its own**, not US-009's, which is what §2 (v39) requires: the two
matrix rows are separate because read-only mode blocks the connection settings and
permits the instruction. `ThePolicy_IsSeparateFromTheConnectionSettingsPolicy`
resolves both from `IAuthorizationPolicyProvider` and asserts both exist.

| Principal | Result | Evidence |
|---|---|---|
| Admin | reaches the page (`200`) | `ThePolicy_AdmitsAnAdmin`, `ASignedInAdmin_ReachesTheInstruction` |
| Dean | refused by the policy | `ThePolicy_RefusesADean` (synthetic principal — see F-1) |
| Anonymous principal | refused by the policy | `ThePolicy_RefusesAnAnonymousPrincipal` |
| Anonymous request | `302` to `/sign-in`, and no part of the instruction in the body | `AnAnonymousVisitor_IsSentToSignIn`, `AnAnonymousVisitor_SeesNoPartOfTheInstruction` |

The endpoint declares the policy rather than relying on the fallback (API-9), and
the SC-4 enumeration test of US-008 passes unchanged.

## 7. Credentials, Key and Google Access

Nothing to hand over and nothing to protect at rest: this Story introduces no
credential, reads none and stores none. The service-account **key** stays where
DC-3 and SC-7 put it — the Owner's secret store, reachable only through
`ISecretStore`, which this Story does not use.

What the Story does render is the **client ID**, which is not a secret: §1 and
BR-032 name it and the scope list as the only two values that cross the boundary
from the Owner to a school. Rendering it to the school's own Admin is the
requirement (§4, Epic 6), not a leak.

Google access: none. The instruction **states** what must be configured and never
checks it, so the first real Google port — and the US-007 finding F-5 that goes
with it — arrives with US-011.

## 8. Sensitive Data Exposure

| Channel | Result |
|---|---|
| Response / view | Two non-secret configuration values, the six scope URIs and translated text. `ThePage_ShowsNoPersonalData` asserts even the signed-in Admin's own address is absent — the landing page shows it, this page has no reason to. |
| View model | Six fields, none of which can hold a secret or a forbidden identifier (§2.7 of the api-design, implemented as written). |
| Logs | No log statement added by this Story. |
| Audit | No row written. |
| Exceptions | Unchanged: the single `IExceptionHandler` and the translated error page. |
| Exports | None. |
| Telemetry | Unchanged. |
| Test fixtures | Synthetic throughout (TC-4): a `.example.test` domain, a fabricated client ID, a fabricated OAuth client id and a fabricated secret. |

One observation worth recording: the saved technical-account address (US-009) is
deliberately **not** shown here, and a test asserts it
(`ThePage_DoesNotInventATechnicalAccountAddress`). That keeps a school account
address off a page whose whole purpose is to be copied and forwarded.

## 9. Input Validation

The operation accepts **no input**: no route parameter, no query parameter, no
body, no form. There is therefore nothing to validate and — more importantly — no
input by which a caller could influence the rendered text.

`AQueryStringIsIgnored` proves it end to end: a request carrying
`?clientId=999&domain=attacker.example.test` returns the same handed-over text as
a plain request, contains neither injected value, and writes nothing.

The values that *are* rendered come from outside the installation (the Control
Plane, via the legitimacy check). They are handled as data: HTML-encoded on
render, never assembled into markup. See F-2 for the one constraint gap behind
that.

## 10. API Security

| Aspect | Result |
|---|---|
| Endpoints | Exactly the one the contract documents; no undocumented route |
| Methods | `GET` only; `UnsafeMethodsAccepted` is empty |
| `POST` | None — `TheInstructionPath_AcceptsNoPost` |
| Download routes | None — `.txt`, `.pdf`, `/download`, `/export` all `404` |
| Response fields | The six of the contract's schema, no more |
| Status codes | `200`, `302`, `403` — the contract documents no `400` and no `409`, and the implementation produces neither |
| Error bodies | The translated error page; no internals (SC-10) |

## 11. Persistence and Configuration

**No schema change and no migration.** Independently verified: `git status` over
`src/ClassroomAgent.Infrastructure/` is empty and the migrations directory still
holds three migrations plus the snapshot. `AppUserMigrationTests`, which pins
three migrations and five tables, passes untouched — the guard db-design §6 asked
for worked.

**No configuration change.** No `appsettings*.json` edit, no new setting, no
environment variable. The new code reads no configuration at all.

No connection string, generated database file or `.xlsx` export is in the change
set. `google_credentials.json` and `classroom_cache.db` remain git-ignored
(confirmed with `git check-ignore`; neither file was opened).

## 12. Logging, Audit and Telemetry

Nothing added in any of the three. Viewing the instruction is not one of §5's
audited actions, and the review confirms no row is written: the AC-007 tests
compare the row count of **every** table before and after rendering, which is a
stronger assertion than checking the audit table alone.

## 13. Dependencies

No package added, no package version changed, no project reference added —
`git diff` over `*.csproj` and `*.sln` is empty. `Application` still references no
package and `Domain` keeps zero package references, so placing the scope constant
in `Domain` (spec I-2) introduced no dependency.

`dotnet list package --vulnerable`: no vulnerable packages reported in any of the
seven projects. This is a point-in-time result against nuget.org's advisory data,
not a guarantee.

## 14. Security Test Coverage

| Requirement | Test | Status |
|---|---|---|
| Allowed-role and forbidden-role (TC-5) | `ThePolicy_AdmitsAnAdmin`, `ThePolicy_RefusesADean` | PASS (see F-1 for the HTTP half) |
| Anonymous access refused | `AnAnonymousVisitor_IsSentToSignIn`, `..._SeesNoPartOfTheInstruction` | PASS |
| SC-4 list unchanged | `TheAnonymousList_GainsNothing` (asserts the endpoint exists **and** is not anonymous) | PASS |
| Method restriction | `TheEndpoint_AcceptsOnlyGet`, `TheInstructionPath_AcceptsNoPost` | PASS |
| No write, any mode | `OpeningThePage*` (3 methods, 5 cases), `ConnectionInstructionBranchTests.TheQuery_WritesNothing` | PASS |
| No secret exposed | `TheHandedOverText_CarriesNoSecret`, `ThePage_CarriesNoSecretAnywhere` | PASS |
| Forbidden identifiers absent | `ThePage_DoesNotShowTheOAuthWebClientId`, `..._DoesNotShowTheInstallationIdentifier` | PASS |
| Scope list exact, forbidden scopes absent | `ConnectionInstructionScopeTests` (6 methods, 15 cases) | PASS |
| No Workspace role name (OD-001) | `NoWorkspaceAdminRoleName_AppearsInEitherLanguage` (9), `..._ReachesThePage` (9) | PASS |
| No inline script | `TheCopyAffordance_IsAStaticScriptFileAndNotInlineScript` | PASS |
| Translations complete (TC-8) | `ConnectionInstructionTranslationTests` (6 methods, 42 cases) | PASS |

**Test quality note.** This review looked specifically for assertions that would
pass vacuously, because the test-generation report admits the first draft had 31
of them. The strengthening is real: every absence assertion now asserts the page
was served (`200`) first, the scope assertions require a non-empty list, and
`TheAnonymousList_GainsNothing` asserts the endpoint exists before asserting it is
not anonymous. A test that cannot fail is not evidence, and these can fail.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| A Dean opens the instruction | refused, nothing revealed | policy test; the matrix cell | PASS |
| An anonymous visitor requests it | challenged, no content in the body | 2 tests | PASS |
| A caller injects a client ID or domain via the query string | ignored; the rendered text unchanged; nothing written | `AQueryStringIsIgnored` | PASS |
| A caller `POST`s to the path | not accepted | `TheInstructionPath_AcceptsNoPost` | PASS |
| A caller guesses a download route to obtain a document | none exists | 4 cases | PASS |
| A school is led to over-grant Workspace privileges by a printed role name | no role name is printed | 18 cases over both languages and the page | PASS |
| A school authorises the wrong client (the OAuth web client id) | that identifier is never rendered | 2 tests | PASS |
| A school authorises a write-capable scope | only the six read-only scopes are rendered; the excluded ones are asserted absent | 15 cases | PASS |
| Repeated page views inflate the audit trail | no row is written | 5 cases | PASS |
| A suspended school loses access to the instruction it needs to fix delegation | served in all three read-only causes | 7 cases | PASS |
| A stale copy outlives a rotated client ID | no document is produced; the page re-reads per request | `WhenTheClientIdIsRotated_*`; no download route | PASS |
| The copy carries something the server never rendered | the script copies `textContent` of the rendered element; no interpolation, no network | code read (D-2) | PASS |

## 16. Repository Hygiene

No secret-like file, generated database file or `.xlsx` export in the change set
(checked by pattern over `git status`). No `.env`. No connection string. The live
credential files stay git-ignored and were not opened.

The change set contains one new binary-adjacent asset —
`src/ClassroomAgent.Web/wwwroot/js/copy-instruction.js`, 1.7 KB of source — which
is a tracked application file, not a build artifact.

## 17. Deviations

### D-1 verified: the shared read rule is behaviour-preserving

The Implementation Report flags one non-additive change and asks for it to be
reviewed. It was, by diffing the method rather than reading the claim:

- **before:**
  `state?.LastSuccessfulCheckAt is null || string.IsNullOrWhiteSpace(state.Domain) ? null : state.Domain`
- **after:** `ConfirmedLegitimacy.DomainOf(state)`, which is
  `state?.LastSuccessfulCheckAt is null || string.IsNullOrWhiteSpace(value) ? null : value`

The two are semantically identical, so the US-009 control (BR-020: an installation
may only work with the domain the Owner approved) is unchanged — and every US-009
test passes untouched, including the crafted-request test that proves a request
carrying its own domain is ignored.

The change is **security-positive** rather than merely acceptable: the alternative
readings of spec I-1 were to duplicate the predicate or to add a third database
read. A duplicated "is this value trustworthy" predicate is precisely the kind of
divergence that later lets one screen treat an `upgrade_required` answer as
confirmed while another does not.

### D-2 verified: one rendered instruction, copied from the DOM

The instruction is a single `<pre id="connection-instruction">` that is both the
visible text and the copy source; the script reads `instruction.textContent`. Two
consequences the review confirms:

- the copied text **cannot** contain anything the server did not render — there is
  no interpolation and no second fetch;
- the visible and copied texts cannot diverge, because there is only one of them.

### D-3 verified: the affordance degrades rather than failing

The script hides the button when `navigator.clipboard.writeText` is unavailable
and when a write is refused. No dialog, no alert, no exception surfaced. With
scripting disabled the page is unchanged — which the tests prove by reading the
instruction out of the server's markup.

No undocumented security behaviour, omitted control or permissive default was
found. No claim in the Implementation Report was found to be false or
overstated.

## 18. Findings

### F-1 — Minor — TEST_COVERAGE (TC-5)

**Affected:** `tests/ClassroomAgent.Tests/Web/Security/ConnectionInstructionAuthorizationTests.cs`.

**Observed:** the forbidden-role half is proven by asking the host's real
`IAuthorizationService` to authorize a **synthetic** Dean principal, not by
signing a Dean in and receiving `403` over HTTP.

**Expected:** TC-5 asks for an allowed-role and a forbidden-role test per
protected endpoint; the HTTP-level version is the stronger form.

**Risk:** low. The endpoint is separately shown to declare the policy
(`TheEndpoint_DeclaresAPolicyAndAllowsNoAnonymousAccess`) and to allow no
anonymous access, and the policy itself refuses the Dean role, so the HTTP path is
covered by construction. The gap is the composition, not the rule.

**Cause:** no Dean can sign in until US-012 creates Dean accounts.

**Required correction:** **US-012** must add the HTTP-level forbidden-role test —
now for **two** settings pages, the connection settings (US-009 F-1) and this
instruction. This finding extends US-009 F-1 rather than repeating it.

**Loop-back:** none. Carried to US-012.

### F-2 — Informational — PERSISTENCE / DATA_EXPOSURE

**Observed:** of the two values this page renders, `legitimacy_state.client_id`
carries a character-set constraint (`^[0-9]{10,32}$`) but
`legitimacy_state.domain` carries only a **length** constraint
(`char_length BETWEEN 3 AND 253`). So a domain containing markup characters is not
impossible at the database level, and US-010 is the first Story to render that
value into a block a reader copies and pastes elsewhere.

**Verified defence:** the view uses no `Html.Raw` or equivalent, so Razor
HTML-encodes the value, and the copy path uses `textContent`, which yields literal
characters. Upstream, the Control Plane validates a domain when the Owner
registers an `Installation` (US-002). Nothing is exploitable today.

**Recorded because:** the encoding is currently the *only* in-process defence for
that value. A later Story that renders the domain into an attribute, a URL, a
generated document or an export must not assume the value is constrained. If a
future Story needs that guarantee, adding a format constraint to the column is a
DB_DESIGN decision, not an implementation detail.

### F-3 — Informational — GOOGLE_ACCESS (carried, SC-8)

The US-007 finding F-5 — the first Story adding a real Google data port must prove
its own read-only refusal there — **stays open and moves to US-011**. US-010 adds
no Google port: the instruction states what must be configured and never checks
it. Confirmed by search: no `IGoogleDataPort`, no SDK type, no scope requested at
runtime.

### F-4 — Informational — API_SECURITY (carried)

The US-008 finding F-5 — a wrong HTTP method on `/sign-in/google` or `/sign-out`
answers `404` where that contract documents `405`, because the anonymous catch-all
matches first — **is not inherited by this route and is not re-opened**. Verified:
this route is not anonymous, and the US-010 contract documents no `405`, so its
`404` for a wrong method is the documented behaviour rather than a divergence.

### F-5 — Informational — READ_ONLY_MODE (carried)

The US-009 finding F-2 — a declared service write commits the **whole**
`DbContext`, not one row — is **not exercised** by this Story, which stages nothing
and declares no service write. It remains relevant to the next Story that stages a
write before auditing a refusal.

### F-6 — Informational — CONFIGURATION (forward-looking)

Neither host sends a `Content-Security-Policy` header today, and no approved
artifact requires one — so this is an observation, not a finding against US-010.
It is recorded because US-010 is the Story that introduced the program's **first
JavaScript**, and it did so in the shape that keeps the option open: an external
file under `wwwroot/js/`, with no inline script anywhere on the page and a test
that fails if one appears. Whenever a CSP is adopted, this page needs no
`unsafe-inline` exception.

## 19. Positive Controls

Independently observed and verified:

1. The endpoint declares `ViewConnectionInstruction`; deny-by-default is intact;
   anonymous access is impossible and the SC-4 list gained nothing.
2. The policy is separate from US-009's, matching the §2 split.
3. The route accepts `GET` only; no antiforgery exemption was added or needed.
4. The page writes nothing, in any mode — proven against every table, not just the
   audit table.
5. `PermittedServiceWrite` still has exactly four members; BR-026's closed list is
   unchanged.
6. No Google port, SDK type, runtime scope request or call exists in this Story.
7. No Control Plane call while rendering; the values come from the installation's
   own database.
8. No secret can reach the page: the new code touches no configuration, secret
   store or environment variable, and the view model has no field for one.
9. Neither of the two confusable identifiers, nor the service account's own
   address, is rendered.
10. The six scopes are exactly §6's, read-only, with the excluded ones asserted
    absent.
11. No Workspace admin-role name appears in either translation file or on the
    page — the OD-001 guard is real and enforced 18 ways.
12. Every rendered value is HTML-encoded; no `Html.Raw`.
13. The copy script makes no network call and copies only what the server
    rendered.
14. No migration, no schema change, no package, no project reference, no
    configuration change.
15. No vulnerable package reported; repository hygiene clean; live credential
    files still ignored and never opened.
16. Build clean and the whole suite green (1791/1791), re-run by this review
    rather than taken from the report.

## 20. Open Decisions

No blocking security Open Decisions were identified.

OD-001 and OD-002 are both resolved and both were implemented as resolved. The
review specifically confirmed that OD-001 was not quietly widened: no role name
appears anywhere, and the test that enforces it covers both languages and the
rendered page.

`trebovaniya.md` §7 item 10 stays open. US-010 does not depend on it, and its
resolution will not require a code change beyond translation entries and their
test — which is the property OD-001 was chosen for.

## 21. Review Limitations

1. **No penetration testing or fuzzing.** This is a code, configuration and test
   review.
2. **The copy script was not executed in a browser.** Clipboard behaviour, the
   hidden-button fallback and the confirmation text are reviewed as source and
   asserted structurally (no inline script, the file is served, the text is in the
   markup). No test drives a real clipboard, as the test strategy states.
3. **Vulnerability scanning is point-in-time** against nuget.org advisory data.
4. **The forbidden-role half is proven against the real policy object, not over
   HTTP** — F-1.
5. **The `InstallationNotConfirmed` + not-read-only combination is proven by unit
   test only**, because PostgreSQL constraints make it unreachable through the
   database (db-design §2.1). The review accepts this: it is the same limitation
   shape US-009 recorded, and the implementation added the test rather than
   dropping the branch.
6. `runtime_checks: PARTIAL` — the HTTP behaviour, authorization, absence of
   writes and translation completeness were exercised against the running host and
   a real database; the browser-side affordance was not.

## 22. Verdict Rationale

**PASS.**

No Critical and no Major finding. Every SC item the Story touches is `PASS` with
evidence. The build is clean and the whole suite is green, re-run by this review.
No security-sensitive Open Decision is unresolved, and no human security decision
is required.

The reasoning behind the verdict is that this Story's security posture is
*structural* rather than enforced: it cannot leak a secret because it reads none;
it cannot write because it holds no unit of work; it cannot call Google because it
holds no port; it cannot be reached anonymously because the policy is declared and
deny-by-default backs it. Controls of that kind do not depend on a developer
remembering a check, which is the strongest form the review can find.

The one Minor finding is a known composition gap that US-012 must close, for two
pages rather than one. It does not block the commit.

Recommended next stage: `HUMAN_PR_APPROVAL`.
