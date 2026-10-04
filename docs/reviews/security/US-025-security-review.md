---
artifact_type: security_review
story: US-025
version: 1
status: APPROVED
created_at: 2026-10-04T13:55:00Z
updated_at: 2026-10-04T13:55:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-025-journal-view-for-period.md
    version: null
  - path: docs/specifications/US-025-spec.md
    version: 1
  - path: docs/decisions/US-025-open-decisions.md
    version: 4
  - path: docs/designs/api/US-025-api-design.md
    version: 1
  - path: docs/designs/api/US-025-openapi.yaml
    version: 1
  - path: docs/designs/database/US-025-db-design.md
    version: 1
  - path: docs/designs/database/US-025-entity-model.md
    version: 1
  - path: docs/tests/US-025-test-strategy.md
    version: 1
  - path: docs/tests/US-025-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-025-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 4
security_sensitive: true
runtime_checks: FULL
---

# US-025 Security Review — Journal view for a period

## 1. Executive Summary

**PASS.** No Critical, Major or Minor finding; four Informational notes.

US-025 adds one read-only page, `GET /workspace/journal`, that shows students'
names, emails and grades (personal data of minors) to Admin and Dean. The
principal controls hold and are proven by tests: its own `ViewJournal` policy
(Admin, Dean) under the deny-by-default fallback; no write, no guard, no Google
port in the query (constructor evidence plus a fingerprint test in all four
modes); validation before any journal read; no rejected value in the page or
the log; HTML encoding of every Google string; log lines with internal ids and
counts only.

The one security-relevant supporting change — implementation deviation D-1,
the language switcher's return path — closes a reflection of the raw query
string on this page and is safe (§17).

Limitation: this review ran in the same session that implemented the Story, so
its independence rests on the evidence recorded here (tests run, code read), not
on a separate reviewer (§21).

## 2. Reviewed Artifacts

As in the front matter. All current; none `SUPERSEDED`. `HUMAN_SPEC_APPROVAL`
recorded 2026-10-04 (history.jsonl).

## 3. Security-Relevant Scope

| Element | Host / layer | Change |
|---|---|---|
| `GET /workspace/journal` | Web, public port | new page, policy `ViewJournal` |
| `GET /` | Web | additive link |
| `_LanguageSwitcher.cshtml`, `UiLanguageController.ReturnPathViewDataKey` | Web, every signed-in page | return path overridable by the page (D-1) |
| `GetJournalQuery` | Application | new read-only query |
| `IJournalSource` / `JournalSource` | Application port / Infrastructure | four untracked reads |
| `Installation:TimeZone` | Web configuration | new required setting |
| `InstallationPolicies.ViewJournal` | Application / Web security | new policy |

Assets: students' names, emails, grades, submission states (§5 personal data of
minors); the session cookie. Not touched: passwords, the service-account key,
`AllowedAdmin`, `WorkspaceConnection`, audit rows, the Control Plane channel.

Trust boundaries: browser → installation (query string as untrusted input);
controller → use case; use case → `IJournalSource` → PostgreSQL. No new
outbound boundary.

## 4. Environment and Tools

- .NET SDK 10.0.401; Windows 10; Docker running (Testcontainers available).
- Commands: `dotnet build ClassroomAgent.sln` (0 warnings, 0 errors);
  `dotnet test ClassroomAgent.sln` — 3018 / 3018 passed, 0 skipped (run during
  IMPLEMENTATION, re-checked from the recorded output); journal set
  (`FullyQualifiedName~Journal`) 175 / 175 four times in a row;
  `dotnet list ClassroomAgent.sln package --vulnerable` — no vulnerable package
  in any of the seven projects; `git check-ignore`; ripgrep over the changed
  files.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | `ViewJournal` grants Admin and Dean exactly as §2 "Просмотр и экспорт журнала успеваемости" ✔ ✔; no Teacher/Student; no roster scoping (rows come from Classroom data, not from the viewer). |
| SC-2 Authentication | NOT_APPLICABLE | No credential, cookie or sign-in change. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Admin login unchanged. |
| SC-4 Authorization | PASS | `[Authorize(Policy = ViewJournal)]` on `JournalController`; policy registered in `InstallationSecurityServices`; fallback policy unchanged; no `[AllowAnonymous]`/`IgnoreAntiforgery` in new code; GET changes nothing; existing endpoint enumeration test green with the page routed; `JournalAuthorizationTests` (anonymous → `/sign-in`, temporary password → `/sign-in/change-password`, Admin and Dean → `200`). |
| SC-5 Read-only mode | PASS | `GetJournalQuery(IJournalSource, SchoolTimeZone, TimeProvider)` — no `IUnitOfWork`, `IReadOnlyModeGuard` or Google port is injectable; `JournalSource` uses `AsNoTracking` and never saves; `JournalReadOnlyTests` × 4 causes: journal returned, teaching-table fingerprint unchanged, `ImpersonatedAs` empty. Viewing is permitted by BR-026. |
| SC-6 No DB UI | PASS | No diagnostic endpoint added. |
| SC-7 Key | NOT_APPLICABLE | Key and Data Protection untouched; the time zone setting is not a secret. |
| SC-8 Google | PASS | No Google call (above); no scope change. |
| SC-9 Channel | NOT_APPLICABLE | Untouched. |
| SC-10 Hygiene | PASS | Validation before reads; malformed values never redisplayed (`From`/`To` null, course unselected) and unknown parameters never bound; the switcher no longer reflects the raw query on this page (D-1); no `Html.Raw`, Razor encodes titles, names, emails and raw states (`TitlesAndRawStates_AreHtmlEncoded`); logs: `JournalLog.Built` (course id, dates, view, account id, counts) and `JournalLog.Refused` (message keys only) — `JournalLoggingTests` prove no name, email, title, grade or rejected value reaches the log file. Unexpected failures go to the single exception handler. |
| SC-11 Audit | PASS | Viewing is not an audited action (spec S-11); no audit row written (fingerprint and audit tables unchanged). |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` unchanged. |
| SC-13 Outbound | PASS | No new destination. |

## 6. Authentication and Authorization

Requirement: §2 row ✔ Admin ✔ Dean; deny by default (SC-4); evaluation order
api-design §3. Evidence: controller attribute, policy registration, the
restricted-session middleware runs before routing (temporary-password test).
No identifier in the request grants access to another installation's data
(one installation, one database — AD-1); `courseId` is any stored course, which
is correct because §2 gives Admin and Dean every course in v1 (spec S-03). The
actor of the log line comes from the session claim (VR-007). No findings.

## 7. Credentials, Key and Google Access

Not touched. The query cannot reach a Google port by construction. No findings.

## 8. Sensitive Data Exposure

- **View model**: `JournalPageModel` from `Application`; rows carry no
  participant id, columns no item id (api-design §2.5); no entity crosses
  (AD-8). The DTO `JournalColumn` carries the `CourseWorkKind` enum from
  `Domain.Enums` — an enum, not an entity.
- **Query string**: course id, dates, view only (S-05); nothing personal is
  put in a URL by the page, the switch links or the switcher's return path.
- **Logs**: as SC-10. **Audit**: none. **Exports**: none. **Exceptions**: the
  single handler, no detail.
- **Caching**: no `Cache-Control: no-store` on this page — I-1 below.

## 9. Input Validation

All four parameters are handled in `GetJournalQuery` with every occurrence
(repeat → malformed): `courseId` ASCII digits only, ≤ 19, positive `long`
(Arabic-Indic digits, signs, spaces, overflow refused); dates exact
`yyyy-MM-dd`, ASCII, 2000–2100; `view` `full`/`short`; pair checked after
defaults. 45 unit cases plus HTTP tests for `400` / `404` / no echo. Data
Annotations / `[ApiController]` do not apply: this is an MVC GET page with no
body, validated in `Application` as §8 permits for rules that need the raw
occurrences (decided at TEST_WRITING, test-generation report §2). Google data is
read from the database where US-014/US-015 validated it on import, and is
encoded on output. Oversized input: an over-long `courseId` is malformed by the
19-digit rule; any other parameter is ignored. No findings.

## 10. API Security

Only the approved path exists; GET only; status codes `200`/`302`/`400`/`404`
as the contract; `400` and `404` render the journal page, not the error page
(tests assert the form and the message). No `/api/v1` endpoint, no response
field beyond the contract. No findings.

## 11. Persistence and Configuration

No schema change (`TheModelHasNoPendingChanges_…` green). Reads untracked;
period bounds checked UTC and non-empty (`ArgumentException`, tested). New
required setting `Installation:TimeZone`: absent, blank, unknown or Windows id
refuses the start naming the key (`TimeZoneConfigurationTests`); OD-010 alias
limited to the Kyiv pair. No committed configuration with secrets; no generated
database or `.xlsx` file in the change set. No findings.

## 12. Logging, Audit and Telemetry

Event ids 5250 / 5251 are new and unique. Both carry no personal data (§5
SC-10). `docs/hooks/tool-usage.jsonl` is git-ignored (`.gitignore:63`). No
findings.

## 13. Dependencies

No `.csproj` or `Directory.*` change; no new package or project reference;
`Application` gained no reference to `Infrastructure`. Vulnerability scan:
none reported for the configured NuGet source.

## 14. Security Test Coverage

| Requirement | Test |
|---|---|
| S-01/S-02 allowed and forbidden roles | `JournalAuthorizationTests` (4) |
| Endpoint enumeration (TC-5) | existing `InstallationEndpointTests`, green |
| S-04 read-only, no write, no Google (TC-5, Application level) | `JournalReadOnlyTests` (8) |
| S-06 rejected before reads, value not echoed | `JournalRequestValidationTests`, `JournalPageTests` 400/404/unknown-parameter |
| S-07 no personal data in logs | `JournalLoggingTests` (2, each paired with a positive wait) |
| S-08 HTML encoding | `JournalPageTests.TitlesAndRawStates_AreHtmlEncoded` |
| AC-013 synthetic data, no live Google | fixtures invented (TC-4); `FakeClassroomReader` |

Fixture change D-2 (wait for the start-up purge) alters timing only; I read the
diff — no assertion was removed or weakened.

## 15. Abuse Case Review

| Scenario | Expected | Evidence | Status |
|---|---|---|---|
| Anonymous user opens a bookmarked journal URL | redirect, nothing read | AUTH anonymous test, fingerprint | PASS |
| Dean on forced password change opens the journal | redirect to change form | AUTH test | PASS |
| Reflected markup via `from=<script>` or an unknown parameter | not echoed anywhere in the page | PAGE no-echo tests; D-1 | PASS |
| Stored markup in a Google title or raw state | encoded | PAGE encoding test | PASS |
| Probing course ids | `404` with no course data; ids of existing courses are already listed to the same roles | VAL/PAGE tests | PASS |
| Huge period (2000–2100) on a large course | bounded by one course; ≤ 4 round trips | db-design §4, SRC one-command tests | PASS (I-3) |
| Read-only installation | journal shown, nothing written, no Google | RO tests | PASS |

## 16. Repository Hygiene

`google_credentials.json` git-ignored (not opened). No secret-like, database,
`.xlsx` or IDE-local file among the changed or untracked files (git status
reviewed). No findings.

## 17. Deviations

- **D-1 (implementation report §7) — reviewed, safe.** Before the change, every
  signed-in page wrote `Request.Path + Request.QueryString` into the switcher's
  hidden `returnPath`; on the journal that reflected rejected values, against
  VR-005. Now a page may set `ViewData[ReturnPathViewDataKey]`; only the journal
  does, with a URL built from validated values (`SelectedCourseId`, ISO dates,
  `full`/`short`) — no user-controlled string. Razor still attribute-encodes the
  value; `UiLanguageController.LocalReturnPath` still requires a local path of at
  most 2048 characters on the way back, so the override cannot become an open
  redirect. Other pages behave as before (US-039 tests green). The general
  behaviour on other pages is the known US-039 INFO-1 — see I-2.
- **D-2** — test timing only (§14).

## 18. Findings

No Critical, Major or Minor finding.

| ID | Severity | Category | Ref | Observation | Recommendation | Loop-back |
|---|---|---|---|---|---|---|
| I-1 | Informational | DATA_EXPOSURE | SC-10 | The journal page (students' names and grades) is sent without `Cache-Control: no-store`; a shared computer's browser cache may keep it. No convention requires the header (api-design §2.8). | Owner decides whether pages with personal data send `no-store` — a security-conventions change, not this Story. | none |
| I-2 | Informational | DATA_EXPOSURE | SC-10 | The switcher still reflects the raw query string on every page that does not set the override (US-039 INFO-1). No other current page carries personal data or rejected values in its query. | When a later Story adds a query-string page (US-020 list filters), it should set the override as the journal does. | none |
| I-3 | Informational | OTHER | NFR-003 | A period may span 2000–2100; cost is bounded by one course (Q4 ≤ rows × columns) and no rate limit is defined. | None now; revisit if export Stories reuse the query at larger scale. | none |
| I-4 | Informational | CONFIGURATION | VR-006 | Rejection of a Windows id relies on `TimeZoneInfo.HasIanaId`, verified on Windows only; the Linux runtime was not exercised. | Confirm on the first Linux deployment that `FLE Standard Time` refuses the start. | none |

## 19. Positive Controls

Own policy for the matrix row; deny-by-default fallback intact; restricted
session respected; constructor-level absence of write and Google dependencies
plus fingerprint tests in all BR-025 causes; validation before any journal read
with every occurrence considered; no reflection of rejected input (page and
switcher); Razor encoding of Google data with no `Html.Raw`; personal-data-free
logs proven by reading the log file; no new package; no schema change; required
time zone setting fails closed.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- **Independence**: the reviewer and the implementor ran in the same session;
  conclusions rest on the tests and code cited, which a human may re-check at
  `HUMAN_PR_APPROVAL`.
- No penetration test, no browser-level check of cache behaviour, no Linux run
  (I-4).
- Vulnerability scan is only as current as the NuGet advisory data.

## 22. Verdict Rationale

Build and tests are green with recorded output; every touched SC item is PASS;
the security-sensitive criteria AC-009, AC-010, AC-011, AC-012, AC-013 are
proven by passing tests; no blocking Open Decision; the security-relevant
deviation D-1 narrows exposure and introduces no redirect or injection path.
The four Informational notes need no correction in this Story. **PASS →
HUMAN_PR_APPROVAL.**
