---
artifact_type: security_review
story: US-071
version: 1
status: APPROVED
created_at: 2026-11-03T19:18:53Z
updated_at: 2026-11-03T19:18:53Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-071-search-field-in-the-header.md
    version: null
  - path: docs/specifications/US-071-spec.md
    version: 2
  - path: docs/decisions/US-071-open-decisions.md
    version: 1
  - path: docs/designs/api/US-071-api-design.md
    version: 1
  - path: docs/designs/api/US-071-openapi.yaml
    version: 1
  - path: docs/designs/database/US-071-db-design.md
    version: 1
  - path: docs/designs/database/US-071-entity-model.md
    version: 1
  - path: docs/tests/US-071-test-strategy.md
    version: 1
  - path: docs/tests/US-071-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-071-implementation-report.md
    version: 2
  - path: docs/reviews/implementation/US-071-implementation-verification.md
    version: 2
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: PARTIAL
---

# US-071 Security Review

## 1. Executive Summary

**PASS.** No Critical, Major or Minor finding; three informational notes.

The Story adds one protected GET page that makes students searchable by name and email for the first time, with their courses, across the installation. Its controls: the `ViewCourses` policy, the shared validation of `q`, a bound parameter on every read, Razor and `Uri.EscapeDataString` encoding, no logging of the text, and removal of the language switcher from every error page, which closes the raw-query path US-070 m-1 found.

Limits: code, configuration and test review plus the automated suite, not a penetration test. A reverse proxy on the pilot server, if any, is outside the code (INFO-1). The manual walk of AC-011 has not happened; it is a look check with no security content, tracked as `US-071/IMPLEMENTATION/m-1`.

## 2. Reviewed Artifacts

The inputs in the front matter at the versions shown, and the working tree against `4c3acc6`. None is superseded. The Specification is `APPROVED`; `HUMAN_SPEC_APPROVAL` is recorded in `history.jsonl`. The Open Decisions artifact holds none open.

Delegated to `cheap-worker`: the build, the full test run, the vulnerability listing and the project and configuration diff; results in section 4. Read here directly: every changed production source and view, the `.resx` and CSS diffs, `SearchMatch`, `CourseListQueryRules.ReadSearch`, `CourseListUrl`, `TeacherListUrl`, `UiLanguageController`, `InstallationLogging`, `RequestRouteEnricher`, the policy registration, and `SearchAccessTests`, `ErrorPageHeaderTests`, the relevant parts of `SearchResultsPageTests` and `PermissionMatrixTests`.

## 3. Security-Relevant Scope

- **Functionality (installation host only).** `GET /workspace/search` with `q`; a GET form in the page header; no language switcher on the error page under any endpoint; the final hint texts; a `.btn:disabled` CSS rule on both hosts.
- **Assets.** Names and emails of students and teachers, students' course memberships with the left-roster mark, organizer emails as a match field, and the search text itself, which may be a person's name.
- **Trust boundaries.** Browser to installation host (the text in the address, the markup echo); controller to Application use case; Application to read ports; application to PostgreSQL.
- **Not touched.** Authentication, Google ports, the Control Plane channel, persistence schema, audit, exports, configuration, packages. The Control Plane gets one CSS rule only.

## 4. Environment and Tools

| Check | Result |
|---|---|
| .NET SDK | 10.0.401 |
| Docker / Testcontainers | available, server 29.8.0 |
| `dotnet build ClassroomAgent.sln -nodeReuse:false -p:UseSharedCompilation=false` | exit 0; 0 warnings, 0 errors; incremental |
| `dotnet test ClassroomAgent.sln --no-build` | exit 0; **total 7561, passed 7561, failed 0, skipped 0**; 11 min 36 s |
| `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` | exit 0; nuget.org reachable; all 7 projects report no vulnerable package |
| `git diff --stat` on `*.csproj`, `Directory.*.props`, `*.sln`, `appsettings*.json` | empty |

The implementation report's second attempt records one teardown timeout in `InstallationCookieTests` that its next run did not repeat; this independent run did not show it either.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role, policy or seed added. `ClassroomRole.Student` is the existing membership value of synced data, not an account. §2 row "Просмотр курсов и участников" is Admin and Dean. |
| SC-2 Authentication | NOT_APPLICABLE | Not changed. A temporary-password session is redirected before the page runs: `ARestrictedDean_IsSentToTheForcedChange`. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Not changed. |
| SC-4 Authorization | PASS | `[Authorize(Policy = ViewCourses)]` on `SearchController`, registered as Admin and Dean. The matrix cell is listed and enforced by `EveryEndpoint_DeclaresThePolicyOfItsMatrixRow_…`. Nothing anonymous and no antiforgery exemption added. GET only, reads only. |
| SC-5 Read-only mode | PASS | The page writes nothing and runs in read-only mode: `InReadOnlyMode_TheSearchStillWorks`. No Google port in the use case's dependencies. |
| SC-6 No DB UI | NOT_APPLICABLE | No diagnostic surface added. |
| SC-7 Key | NOT_APPLICABLE | Not touched. |
| SC-8 Google | PASS | Stored data only: `ASearch_CallsNoGoogleApi_WritesNothing_AndAuditsNothing`. |
| SC-9 Channel | NOT_APPLICABLE | Not touched. |
| SC-10 Hygiene | PASS | Section 8. |
| SC-11 Audit | PASS | A view is not on the audited list; the test asserts no new row. |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` and the Control Plane's code are unchanged. |
| SC-13 Outbound | PASS | No new outbound call. The form's action and all links are same-host. |
| SC-14 No cache, history | PASS | `TheResultsPage_IsNotCached`, the endpoint-enumerating `EveryEndpoint_…_CarriesNoStore`. The accepted history risk has its three measures: `autocomplete="off"`, the warning in every state, no query string in logs. |
| SC-15 Headers | PASS | No inline script, style attribute or handler in the new views; `.btn:disabled` and the header rules hold no `data:` URI or remote resource. `TheResultsPage_CarriesTheSameOriginReferrerPolicy`. |

## 6. Authentication and Authorization

| Caller | `GET /workspace/search` | Evidence |
|---|---|---|
| Anonymous | `302` to sign-in, with the text neither in the address nor the page | `Anonymous_IsSentToSignIn` |
| Dean on a temporary password | `302` to the forced change | `ARestrictedDean_IsSentToTheForcedChange` |
| Admin, Dean | `200` or `400` | `AnAdminAndADean_CanSearch` |
| Any other role | refused by the policy | `EveryRolePolicy_AdmitsExactlyTheRolesOfItsMatrixRow`; v1 has no third role to sign in with |

`ViewCourses` and `ViewMeetCodes` admit the same two roles, so the page shows no Meet code to someone who could not open that list. Links lead to pages that keep their own policies. A single installation holds one school, and both roles see all its courses, so there is no foreign-identifier case here. `ActorId` reads the session, never a request value, in the pattern of the other controllers.

## 7. Credentials, Key and Google Access

No password, key, scope or impersonation path is touched. No Google port is reachable from `GetSearchResultsQuery`.

## 8. Sensitive Data Exposure

- **Responses and views.** Row DTOs only (AD-8); `CourseState` is an Application model. `participantId` stays in the row DTOs and is not rendered: the attribute verification M-1 found is gone. The only id in a link is the teacher id the existing course list already takes, and course ids in card paths. No name, email or code is built into a query string: `NoLink_PutsARowsCodeNameOrEmail_IntoAQueryString`.
- **Logs.** `SearchResultsLog` carries the account id and four counts, or the trace id, parameter name and refusal key. Production logging sets `Microsoft` to `Warning`, so the framework's request lines, which hold the full address, are not written; the test host uses that same file sink. `RequestRouteEnricher` writes the route template, never the raw path. `QueryString` occurs in no logging code. `TheSearchedText_IsInNoLogLine` searches every event for an ASCII marker in all four states, with a control on the statuses and a non-empty log, so the percent-encoding of a Cyrillic text cannot make it vacuous.
- **Failure path.** The word is a bound parameter read from a holder object (`SearchMatch`), and neither `EnableSensitiveDataLogging` nor `LogTo` appears in `src`, so a failed command logs no value. Read from the code, not exercised by a test.
- **Audit.** None written.
- **Exports.** None added.
- **Telemetry.** `docs/hooks/tool-usage.jsonl` remains git-ignored.
- **Erased people.** `AnErasedPerson_AndAMeetingWithThemAsOrganizer_AreNotFound`; verification reviewed the excluded case through `ErasureExclusionInAdvanceSyncTests`.

## 9. Input Validation

`q` passes the one implementation, `CourseListQueryRules.ReadSearch`: repeated, trimmed, over 100 characters, control characters including NUL all refused before any read. A refusal keeps no text: the field is empty, the return path bare, the status `400`. Below 2 characters: no read. No other parameter is read: `AnUnknownParameter_IsNeverEchoed`. `%`, `_` and `\` carry no meaning, since `Contains` is translated to `strpos`.

## 10. API Security

One endpoint, matching the openapi. Echoes of the text: the header field's `value` (Razor attribute encoding), the "nothing found" line (a `LocalizedString` written by Razor, so encoded; `IStringLocalizer`, not `IViewLocalizer`), and URLs (`Uri.EscapeDataString`, with a constant prefix, so no scheme can be injected). `TheText_IsEncoded_InTheFieldAndTheReturnPath` and `NothingFound_RepeatsTheText_AndShowsTheReminder` prove it with `a&b=<c>"d'`. `TagBuilder` encodes the limit line and the whole-list link. No `Html.Raw`.

The return path is at most 900 encoded characters against the `UiLanguageController` limit of 2048, and passes `Url.IsLocalUrl`.

Error pages: `PageHeaderRules` decides by the model type, and `ErrorController` is the only place that builds that model, so a future caller cannot forget a flag. `ErrorPageHeaderTests` covers `400` on a malformed code, the `409` read-only refusal, the antiforgery refusal and `404`, with a marker in the query.

## 11. Persistence and Configuration

No entity, column, migration, index or configuration change. New reads are untracked and write nothing. No raw SQL; `FromSql` and `ExecuteSql` do not occur in the new code. The student read has an explicit collation.

## 12. Logging, Audit and Telemetry

See section 8. `Shown` at `Information` records that an account searched and how many matched, never what for. Counts cannot name a person except through the text, which is absent.

## 13. Dependencies

No package or project reference changed. Vulnerability listing as in section 4; it speaks for the current sources only.

## 14. Security Test Coverage

| Requirement | Test |
|---|---|
| Allowed and forbidden roles, TC-5 | `SearchAccessTests`, `PermissionMatrixTests` |
| Anonymous list | endpoint enumeration, matrix |
| Read-only mode, no Google | `InReadOnlyMode_TheSearchStillWorks`, `ASearch_CallsNoGoogleApi_…` |
| Text absent from logs | `TheSearchedText_IsInNoLogLine` |
| No raw query on the error page | `ErrorPageHeaderTests` |
| Encoding | `TheText_IsEncoded_…`, `NothingFound_…`, `MarkupTextSurvivesActionsTests` |
| No cache, headers | `NoStoreResponseTests`, `SecurityHeadersResponseTests`, `SearchAccessTests` |
| Fixtures | synthetic `zz…` values; TC-4 |

## 15. Abuse Case Review

| Scenario | Protection | Status |
|---|---|---|
| Anonymous request with a name in `q` | sign-in redirect with no return URL | PASS |
| Temporary-password session uses search to read data before changing the password | middleware redirects first | PASS |
| Crafted link with script in `q` | encoded everywhere | PASS |
| Crafted link with a misleading sentence in `q` | up to 100 characters shown as plain text in quotes, no link | INFO-3 |
| `q` with NUL or control characters, to fail the query | refused before any read | PASS |
| `q` with `%`, `_` or SQL | `strpos` on a bound parameter | PASS |
| Very many words | at most 50 words in 100 characters, for a signed-in user | PASS by reading; not timed, section 21 |
| Personal data left in the error page's hidden field | no switcher there | PASS |
| Reading the address from another site | `Referrer-Policy: same-origin` | PASS |
| Reading it from a proxy log on the school's server | outside the code | INFO-1 |
| Searching for an erased person | rows do not exist | PASS |

## 16. Repository Hygiene

No new `.json`, `.db`, `.xlsx`, `.env`, key or certificate file in the change set; a pattern sweep of the changed files for private keys, `Password=` and `client_secret` found none. `google_credentials.json` and `classroom_cache.db` remain ignored. The credential files were not opened. The temporary look-check stand is not in the tree.

## 17. Deviations

None with security content. The report's deviations (section 7, items 1 to 11) were reviewed by verification; items 3 to 5 touch markup or names only, and M-1, which rendered an internal id, is removed.

## 18. Findings

No Critical, Major or Minor finding.

### INFO-1: the text also travels in the same-origin `Referer`

- **Category:** DATA_EXPOSURE. **SC:** SC-14. **Affected:** the deployment check, not code.
- **Evidence:** `Referrer-Policy: same-origin` sends the full address, with `?q=`, to the same host on every link followed and stylesheet loaded from the results page. The spec's server check and the project note speak of addresses with `?q=`.
- **Risk:** a reverse proxy in the common combined log format writes the `Referer` field too, so a proxy that hides query strings in the request line could still store the searched name.
- **Required correction:** none in code. At pilot deployment, the check should also cover the `Referer` field. Kestrel and the application do not log it.
- **Loop-back:** none.

### INFO-2: `US-071/API_DESIGN/m-1` assessed for risk

- **Category:** DATA_EXPOSURE. **SC:** SC-10. **Affected:** `ClassroomAgent.ControlPlane/Views/Shared/_LanguageSwitcher.cshtml`.
- **Evidence:** the Control Plane switcher copies the raw path and query, encoded, into a hidden field of the Owner's own page, and has no error-page rule like the installation's.
- **Risk:** low. The Control Plane has no search text and no teaching data in its query strings, the value is encoded, and it is served only to the Owner on a private network. This matches the finding's severity; it is outside FR-010 and AC-012, so it stays OPEN for the Owner.
- **Loop-back:** none.

### INFO-3: the echo can show a stranger's sentence

- **Category:** OTHER. **Affected:** `Views/Search/Index.cshtml`, "nothing found".
- **Evidence:** a crafted link shows up to 100 characters of text in the "nothing found" line, as AC-005 requires.
- **Risk:** minimal: plain text in quotes after a fixed phrase, no link, no markup, and only for someone already signed in who follows the link. The field repeats it the same way.
- **Required correction:** none.
- **Loop-back:** none.

## 19. Positive Controls

Deny by default and the `ViewCourses` policy; the forced password change ahead of the page; validation before any read; bound parameter in every search term; encoding in URLs and markup; `no-store` and the security headers by host-wide rule; no text in logs, with the route template in place of the raw path; no audit noise; the error page without switcher or field; `participantId` kept out of the markup.

## 20. Open Decisions

No blocking security Open Decisions were identified.

`trebovaniya.md` section 7 item 30 (privacy policy) does not block this Story; the pilot runs at the Owner's school (SC-14).

## 21. Review Limitations

- No penetration test and no browser run; CSP behaviour of the new markup is by reading, and the page has no script of its own.
- The Failure path of section 8 is by reading.
- A text of 50 one-letter words was not timed; AC-010's measurement covers common texts only.
- A reverse proxy on the pilot server cannot be checked until deployment.
- The vulnerability listing reflects the feeds' current knowledge.

## 22. Verdict Rationale

`PASS` → `HUMAN_PR_APPROVAL`. The build and suite are green in an independent run, no Critical or Major finding exists, every touched SC item is `PASS`, and no security decision is open. The Story's OPEN findings (`API_DESIGN/m-1`, `DB_DESIGN/m-1`, `IMPLEMENTATION/m-1`, `IMPLEMENTATION_VERIFICATION/m-5`) are not this stage's to close; INFO-2 gives the security view of the first. The other three have no security content.
