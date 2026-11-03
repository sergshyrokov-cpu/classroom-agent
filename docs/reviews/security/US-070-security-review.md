---
artifact_type: security_review
story: US-070
version: 1
status: APPROVED
created_at: 2026-11-03T08:38:20Z
updated_at: 2026-11-03T08:38:20Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-070-search-in-lists-and-pickers.md
    version: null
  - path: docs/specifications/US-070-spec.md
    version: 4
  - path: docs/decisions/US-070-open-decisions.md
    version: 2
  - path: docs/designs/api/US-070-api-design.md
    version: 1
  - path: docs/designs/api/US-070-openapi.yaml
    version: 1
  - path: docs/designs/database/US-070-db-design.md
    version: 1
  - path: docs/designs/database/US-070-entity-model.md
    version: 1
  - path: docs/tests/US-070-test-strategy.md
    version: 1
  - path: docs/tests/US-070-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-070-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-070-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 2
informational_findings: 2
security_sensitive: true
runtime_checks: PARTIAL
---

# US-070 Security Review

## 1. Executive Summary

**PASS.** No Critical or Major finding. The Story adds no endpoint, role, entity, package, Google call or audit event. Its security weight is a user-typed text that now reaches three lists, the row-action forms and the address, plus one new static script. The text is validated by one rule, is a bound parameter on every path, is encoded in URLs and markup, and is in no log line or audit row.

Two Minor findings. m-1: a raw `returnQ` can reach the language switcher's hidden field on one error page, encoded and only in the requester's own response. m-2: the encoding of markup characters is tested on the teacher list only, not through the hand-built form on the Meet codes page.

Limit: `picker-narrow.js` has never run in a browser, so its behaviour under the real CSP is unobserved until the Owner's AC-009 walk (INFO-2).

## 2. Reviewed Artifacts

The inputs in the front matter at the versions shown, and the working tree against `83366fa`. None is superseded. OD-001 is resolved a).

Delegated to `cheap-worker`: the build, the vulnerability listing, the package and configuration diff, and the full test run; its results are in section 4. Read here directly: every changed source, view and script, `ListSearchAccessTests`, the logging configuration, `_LanguageSwitcher`, `ErrorController`, `ParticipantErasureStore`. Nothing else was delegated.

## 3. Security-Relevant Scope

- **Functionality (installation host only).** `q` on `GET /workspace/courses`, `/workspace/teachers`, `/workspace/meet-codes`; `returnQ` on `GET …/course-choice` and on the three Meet code POST forms; an Organizers column on two Meet tables; `wwwroot/js/picker-narrow.js` on five forms.
- **Assets.** Names and emails of teachers, organizer emails, course names, and the search text itself, which may be a person's name.
- **Trust boundaries.** Browser to host (the query and form input); Application to PostgreSQL (the word, the raw Unassigned read).
- **Untouched.** Authentication, policies, Google ports, the Control Plane channel, schema, configuration.

## 4. Environment and Tools

.NET SDK 10.0.401; Docker 29.8.0 with Testcontainers; free RAM 7.1 GB at start.

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln` | exit 0; total 7336, passed 7336, failed 0, skipped 0; 11 min 22 s; 0 lines matching `Npgsql`, `Ryuk` or `timeout` |
| `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` | no vulnerable package in any of the 7 projects |
| `git diff --stat HEAD` and `git status --short` over `*.csproj`, props, `*.json`, `*.db`, `*.xlsx`, `.env*` | empty |

Not performed: penetration testing; a browser run of the script.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role or policy change; Organizers column shown to the same Admin and Dean (OD-001 a) |
| SC-2 Authentication | NOT_APPLICABLE | Not touched; the forced-change session is still redirected (`ARestrictedDean_IsSentToTheForcedChange`) |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Not touched |
| SC-4 Authorization | PASS | No new endpoint; `[Authorize]` attributes on `MeetCodesController` unchanged in the diff; the script is under the static files entry; POST forms keep the token; search forms are GET and change nothing |
| SC-5 Read-only | PASS | Search reads only; `EnsureAllowedAsync` still first in all three use cases; `InReadOnlyMode_ARowActionIsRefused_AndNoTextIsAudited`; no Google port |
| SC-6 No DB UI | NOT_APPLICABLE | Not touched |
| SC-7 Key | NOT_APPLICABLE | Not touched |
| SC-8 Google | PASS | `ASearch_CallsNoGoogleApi_WritesNothing_AndAuditsNothing` |
| SC-9 Channel | NOT_APPLICABLE | Not touched |
| SC-10 Hygiene | PASS | Section 12; m-1 is not a log or an internals leak |
| SC-11 Audit | PASS | No new audited action; `returnQ` is not in the audit rows (tested) |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` not changed |
| SC-13 Outbound | PASS | The script has no `fetch` or `XMLHttpRequest` and no storage; no new destination |
| SC-14 No store, history | PASS | Host-wide rule untouched; all three fields and the picker field carry `autocomplete="off"`; the history trace is accepted by the Owner |
| SC-15 Headers | PASS (not run in a browser) | No inline code or `style` in the changed views and script; a same-host file; `hidden` attribute; CSP not changed. INFO-2 |

## 6. Authentication and Authorization

Policies as in spec section 7, unchanged: `ViewCourses`, `ViewMeetCodes`, `LinkMeetCodes`. `ListSearchAccessTests`: Admin and Dean allowed on each list; anonymous redirected to sign-in; a Dean on a temporary password redirected to the change form. No identifier is taken from the search: the text selects among rows every Admin and Dean may see.

## 7. Credentials, Key and Google Access

Not touched. No Google port is reached.

## 8. Sensitive Data Exposure

- **New on screen.** Organizer emails on the Linked and Not-a-course tables, per OD-001 a, through the same `LoadOrganizersAsync` as Unassigned.
- **Erased and excluded people.** An erased organizer's `organizer_email` is null (`ParticipantErasureStore`, `ClearOrganizer`) and an erased participant's row is removed, so neither can match or show. `InField` requires `field != null`. The accounts that linked or marked a code and the candidate courses are not searched.
- **Responses.** Only DTOs reach the views (AD-8). A refused text appears nowhere in the model; a refused `q` leaves `SearchText` null and the return path without it.
- **The exception.** m-1.
- **The address.** The text is in the query string by decision (SC-14). The program builds only what the user typed into it, never a code or email from a row; a row's code stays in the path. `Referrer-Policy: same-origin` keeps it from other sites.

## 9. Input Validation

`CourseListQueryRules.ReadSearch` is the one rule for `q` and `returnQ`: repeated, trim, empty is no search, over 100 characters, control character. A control-character refusal also keeps NUL from PostgreSQL. It runs on the server before any read. Tests: three refusals on each list, `ARefusedText_ReachesNoRead…`, `ARefusedReturnQ_IsNoSearch_AndTheActionGoesOn`, `ARepeatedReturnQ_…`. `returnQ` cannot refuse or change the action: it is read apart from the known fields.

## 10. API Security

No new endpoint. `Done` redirects to a path built from constants, a validated list, an escaped text and an integer page; a validated text holds no CR or LF. The language switcher still follows only `Url.IsLocalUrl`, up to 2048 characters. A page number up to `int.MaxValue` is clamped to the last page before any read, in long arithmetic.

## 11. Persistence and Configuration

No migration, entity or configuration change. **Injection:** `SearchMatch` reads the word from a holder object, so EF Core parameterizes it (a test asserts the word is not in the command text). The raw Unassigned read takes `hasSearch` and `matchingCodes` through the `FormattableString` of `SqlQuery`, so as parameters. `%`, `_` and `\` are literal (tests on real PostgreSQL).

## 12. Logging, Audit and Telemetry

- The refusal line of the teacher list names the parameter and the key. `MeetCodesController` adds no log line.
- Both hosts override `Microsoft` to `Warning`, so the hosting "Request starting" line, which holds the query string, is not written (`InstallationLogging`). EF Core does not log parameter values: `EnableSensitiveDataLogging` appears nowhere in `src`.
- `TheSearchedText_IsInNoLogLine` covers the accepted and refused `q` on each list. It runs against the host's real logging, and the read stops the host first.
- A POSTed `returnQ` has no log test, but no code path passes it to a logger, and a form value is not in the request scope.
- No audit event is added; `returnQ` is not in the audit rows (tested).

## 13. Dependencies

No package or project reference change. Vulnerability listing clean (section 4).

## 14. Security Test Coverage

| Requirement | Test |
|---|---|
| Allowed and refused roles (TC-5) | `ListSearchAccessTests` |
| Read-only (TC-5) | `InReadOnlyMode_TheSearchStillWorks`, `InReadOnlyMode_ARowActionIsRefused_AndNoTextIsAudited` |
| No Google, no write | `ASearch_CallsNoGoogleApi_WritesNothing_AndAuditsNothing` |
| No log line | `TheSearchedText_IsInNoLogLine` |
| Parameterization, literal matching | `SearchMatchingSourceTests` |
| Encoding | `TheText_IsEncoded_InTheAddressAndInTheField` (teacher list only, m-2) |
| Erased organizer | AC-005 tests, simulated by SQL (verification INFO-2) |

## 15. Abuse Case Review

| Scenario | Expected | Evidence | Status |
|---|---|---|---|
| `q=' OR 1=1 --`, `%`, `_` | literal text | parameter, `strpos`, tests | OK |
| `q=<script>…` or a quote in a field or link | encoded | Razor, `Uri.EscapeDataString`, `HtmlContentBuilder.Append` in `MarkForm` (read); teacher list test | OK, m-2 |
| Crafted `returnQ` to redirect elsewhere | stays local | path built from constants, value escaped | OK |
| `returnQ` over the limit with a valid action | no search, action proceeds, no echo | tests | OK |
| Well-formed code path absent: malformed code with a raw `returnQ` by GET | no echo | switcher fallback | m-1 |
| Finding an erased person by old email | not found | null column, removed row | OK |
| Row action in read-only mode with `returnQ` | `409`, nothing audited with the text | test | OK |
| Many words to slow the search | bounded | at most 50 words | INFO-1 |
| Anonymous probing through search | refused | redirect to sign-in, test | OK |

## 16. Repository Hygiene

`google_credentials.json`, `dac-classroom-agent-*.json`, `classroom_cache.db` and the root `.xlsx` files are git-ignored and were not opened. No new file is secret-like; no generated database or export is untracked or staged.

## 17. Deviations

None from the security requirements other than m-1. Verification's D-3 (a teacher list with an invalid `status` or `page` keeps a validated text) is safe: the text kept is the validated one.

## 18. Findings

**Critical, Major:** none.

### m-1 (Minor, DATA_EXPOSURE, SC-10; spec FR-005.5)

- **Affected.** `MeetCodesController.ChoiceOrPageAsync` (`result.Malformed`), `ErrorController.Page`, `Views/Shared/_LanguageSwitcher.cshtml`.
- **Evidence (read, not run).** `GET /workspace/meet-codes/{code}/course-choice` with a code failing `IsWellFormedCode` (white space, control character, over length) answers the error page by a `ViewResult` under the protected endpoint. For a signed-in user the layout renders the switcher, and with no `ReturnPathViewDataKey` it falls back to the raw path and query, so a raw `returnQ`, also one failing VR-001, is written into the hidden `returnPath`. `AMalformedForm_AnswersTheErrorPage_NotTheSearch` covers only the POST, whose query is empty.
- **Expected.** FR-005.5: not echoed. Api-design: the fallback is never reached on these pages, though the error page is not named among them.
- **Risk.** Low. The value is HTML-encoded, goes only to the requester's own response, is not logged, and the switcher follows only a local path. No row link produces a malformed code, so the address must be typed or crafted.
- **Correction, if the Owner wants it.** `ErrorController.Page` sets the return path to the back link or the landing page. Loop-back: `IMPLEMENTATION`, with a test on this GET.

### m-2 (Minor, TEST_COVERAGE)

- **Affected.** `Views/MeetCodes/Index.cshtml` `MarkForm`, which builds the `returnQ` hidden field by hand with `AppendHtml` around `Append`.
- **Evidence.** `TheText_IsEncoded_InTheAddressAndInTheField` uses `a&b=<c>"d'` on the teacher list only. The Meet tests use plain text. The code is right as read: `Append` encodes.
- **Risk.** A later edit to the hand-built markup could open an attribute injection with no test noticing.
- **Correction, if the Owner wants it.** The same text through the Meet list, `MarkForm`, the confirm form and the choice form. Loop-back: test work.

### INFO-1 (Informational, OTHER)

A search of up to 50 words builds a query with subqueries per word, three counts and a page read; there is no rate limit, and the spec's section 9 accepts a scan at this size. Reachable only by a signed-in Admin or Dean.

### INFO-2 (Informational, CONFIGURATION)

`picker-narrow.js` has not run in a browser, so no one has seen it load under the real CSP. The walk in implementation report section 3.1 should also open the browser console (F12) and confirm no CSP error on those five pages.

## 19. Positive Controls

One validation rule; the word always a bound parameter; literal matching; refused text reads nothing and appears nowhere on the list pages; no log line and no audit text; erased data unmatchable; a script with no network, no storage and no HTML insertion (`textContent` only); no submitted picker field; no state change by GET; read-only guard first in every write.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- Code and test review, not a penetration test.
- m-1 is established by reading, not by a request: this stage changes no test.
- The script was read, not run (INFO-2).
- A reverse proxy's log on the pilot server is outside the code (spec section 7; a deployment note exists).

## 22. Verdict Rationale

**PASS.** Green build and a clean full test run independently repeated here; no Critical or Major finding; every touched SC item passes, with the limit in INFO-2; no blocking Open Decision. Next stage: `HUMAN_PR_APPROVAL`.

The Story's OPEN findings are not closed here. `TEST_WRITING/INFO-1` and `INFO-2`: this run again had no timeout, but neither cause was diagnosed. The verification's findings are not in this stage's responsibility.
