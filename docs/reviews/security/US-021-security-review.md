---
artifact_type: security_review
story: US-021
version: 1
status: APPROVED
created_at: 2026-10-11T17:17:43Z
updated_at: 2026-10-11T17:17:43Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-021-course-detail.md
    version: null
  - path: docs/specifications/US-021-spec.md
    version: 1
  - path: docs/decisions/US-021-open-decisions.md
    version: 2
  - path: docs/designs/api/US-021-api-design.md
    version: 1
  - path: docs/designs/api/US-021-openapi.yaml
    version: 1
  - path: docs/designs/database/US-021-db-design.md
    version: 1
  - path: docs/designs/database/US-021-entity-model.md
    version: 1
  - path: docs/tests/US-021-test-strategy.md
    version: 1
  - path: docs/tests/US-021-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-021-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-021 Security Review — Course detail with roster

## 1. Executive Summary

**Result: PASS.** The course card `GET /workspace/courses/{courseId}` is a
read-only, server-rendered page behind the existing `ViewCourses` policy
(Admin, Dean). It reads two untracked queries from the installation database,
writes nothing, calls no Google port and reads no secret. The single request
value — the course id — is constrained by the route, parsed again in
`Application`, and a malformed or unknown id answers the same 404 without
echoing the value in the page or any log line. Google data (course text, names,
emails) is HTML-encoded by Razor; no Google id, Classroom link, teacher folder
or calendar id leaves Infrastructure.

No Critical, Major or Minor finding. Three Informational notes: the host-wide
`RequestPath` scope property (pre-existing, already tracked for a technical
Story), the event-id–keyed redaction's coupling, and the internal participant
id carried in the DTOs but not rendered.

Build and the full test suite were re-run independently of the implementor
(§4).

## 2. Reviewed Artifacts

As in the front matter. Specification v1 is `APPROVED` (HUMAN_SPEC_APPROVAL
recorded 2026-10-11T16:07:25Z in `history.jsonl`). Open Decisions v2: OD-001 …
OD-013 all carry a resolution by the Owner; none open. No input is
`SUPERSEDED`.

## 3. Security-Relevant Scope

| Item | Detail |
|---|---|
| Host | Installation (`ClassroomAgent.Web`), public HTTPS |
| New endpoint | `GET /workspace/courses/{courseId:regex(^[[0-9]]+$):length(1,19)}` → `CoursesController.Card` |
| Changed view | `Views/Courses/Index.cshtml` — course name becomes a link to the card (internal id only) |
| New view | `Views/Courses/Card.cshtml` |
| Personal data shown | names and emails of current teachers, current students and leavers of one course; leaver role and local date last seen |
| Assets | student/teacher personal data (possibly minors); internal course ids; log file |
| Trust boundaries | browser → installation host; controller → `GetCourseCardQuery`; Application → `ICourseCardSource` → PostgreSQL |
| Logging | two new events (5462 Information, 5463 Warning); new Serilog enricher `RefusedValueRedaction` |
| Not touched | authentication, passwords, Control Plane channel, `Contracts`, Google ports, service-account key, schema/migrations, packages, audit |

## 4. Environment and Tools

- .NET SDK 10.0.401; run results in §21-a.
- Docker / Testcontainers available (integration tests ran).
- Commands: `dotnet build ClassroomAgent.sln`; `dotnet test ClassroomAgent.sln`;
  `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive`;
  `git diff --stat HEAD -- '*.csproj'`.
- Delegated: build, test, vulnerable-package scan and csproj diff went to a
  Sonnet `cheap-worker` (read only, results verbatim). All code reading,
  pattern sweeps, judgement and this report were done by the reviewer.
- Not performed: penetration testing, DAST, manual browser testing.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | Reuses `InstallationPolicies.ViewCourses` (Admin, Dean); no new role, no roster-scoped visibility, no per-course restriction (spec S-03). |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in, password or cookie change. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | No login path touched. |
| SC-4 Authorization | PASS | Class-level `[Authorize(Policy = ViewCourses)]` on `CoursesController`; no `[AllowAnonymous]` / `IgnoreAntiforgeryToken` in changed files (sweep empty). GET only, no state change. 404 via the host's `/error/404` re-execution. `CourseCardAuthorizationTests` — allowed (Admin, Dean), anonymous → sign-in, forced-password Dean refused, endpoint enumerated as protected with `ViewCourses`. |
| SC-5 Read-only mode | PASS | Query has no guard by design (viewing is permitted, BR-026); writes nothing; `CourseCardReadOnlyTests` prove card returned, no write, no Google call (use case and page). Source uses `AsNoTracking`, no `SaveChanges`. |
| SC-6 No DB UI | PASS | No diagnostic surface added. |
| SC-7 Key | NOT_APPLICABLE | No key or reference touched. |
| SC-8 Google | PASS | No Google port in `GetCourseCardQuery` (constructor: `ICourseCardSource`, `SchoolTimeZone` only); owner comparison done in SQL so `GoogleUserId`/`OwnerGoogleId` never leave Infrastructure. |
| SC-9 Channel | NOT_APPLICABLE | No Control Plane interaction. |
| SC-10 Hygiene | PASS | Log lines carry account id, course id, counts, parameter name and outcome only (`CourseCardLog`). Refusal event drops `RequestPath`. `CourseCardLoggingTests` assert the refused value (malformed, overflow, zero, unknown) and an unmatched value are absent from the **whole** log file, and no course/person text is logged on a shown card. No exception or internal detail in responses. See INFO-1. |
| SC-11 Audit | PASS | Viewing is not an SC-11 action; no audit row (spec S-11). |
| SC-12 Owner | NOT_APPLICABLE | No `Contracts` / Control Plane change. |
| SC-13 Outbound | PASS | No new outbound destination; no `HttpClient` in changed code. |

## 6. Authentication and Authorization

| Caller | Expected | Evidence |
|---|---|---|
| Anonymous | redirect to sign-in | `Anonymous_IsSentToSignIn` |
| Admin | 200 | `AnAdminAndADean_CanOpenTheCourseCard` |
| Dean | 200 | same |
| Dean on forced password change | refused | `ADeanWithATemporaryPassword_IsSentToTheForcedChange` |
| Endpoint metadata | protected, `ViewCourses` | `ThePath_IsAProtectedEndpoint_WithTheViewCoursesPolicy` |

No per-course authorization is required in v1 (§2, spec FR-012). Every course
id is reachable by every Admin/Dean of the installation, which is the approved
matrix; ids of another installation cannot exist in this database (AD-1).

## 7. Credentials, Key and Google Access

No password, credential, key or Google access in scope. Verified by reading
the query's dependencies and DI registration (`InstallationServices`: scoped
`ICourseCardSource` → `CourseCardSource`, scoped `GetCourseCardQuery`).

## 8. Sensitive Data Exposure

- **Views.** All values are emitted with Razor `@` (encoded). No `Html.Raw`.
  Description keeps line breaks through CSS `white-space: pre-line`, not markup
  (`GoogleData_IsHtmlEncoded`, `TheDescription_IsText_WithItsLineBreak`).
- **DTOs.** `CourseCardPageModel` and its parts are Application DTOs (AD-8);
  no entity crosses into Web. The Classroom link, teacher folder, calendar id
  and Google ids are not selected (spec S-10). See INFO-3 for participant ids.
- **URLs.** Card links and the switcher return path are built from the parsed
  `long` (invariant decimal); no name, email or Google id in any URL (S-06).
  The raw value is never echoed.
- **Logs.** See §12.
- **Exports, audit, telemetry.** None added.

## 9. Input Validation

- Route constraint: `regex(^[[0-9]]+$)` (doubled brackets are the route-template
  escape for `[0-9]`) plus `length(1,19)` — non-digit values never reach the
  controller.
- Application re-validation (`GetCourseCardQuery.Parse`): non-empty, every char
  `char.IsAsciiDigit`, `long.TryParse` with `NumberStyles.None` and invariant
  culture, `> 0`. This also catches a value the regex `$` would let through with
  a trailing newline, Unicode digits, overflow and zero.
- No read happens before the parse passes; Q2 is not run for an unknown course.
- Query string is not read by the action.
- Data from the database was validated on import (VR-002); it is only encoded
  on output.

## 10. API Security

Only the designed endpoint exists; GET; response is HTML. Malformed and unknown
ids produce the identical host 404 (OD-010). An authorised user can tell an
existing id (200) from a missing one (404); that is the approved behaviour,
since every Admin and Dean may see every course. No JSON endpoint (spec I-8).

## 11. Persistence and Configuration

No entity, schema or migration change; two untracked LINQ queries, no raw SQL.
No `appsettings` change. The only configuration change is the
`RefusedValueRedaction` enricher in the installation Serilog pipeline. No
generated database file or `.xlsx` in the change set.

## 12. Logging, Audit and Telemetry

- Event 5462 `CourseCardShown` (Information): account id, course id, three
  counts. Its `RequestPath` holds only the digits of the course id — an internal
  id, not personal data.
- Event 5463 `CourseCardRefused` (Warning): request id, parameter name,
  outcome. `RequestPath` removed by `RefusedValueRedaction`, which matches the
  event's structured `EventId.Id == 5463`.
- `Microsoft.*` is held at `Warning`, so framework request/routing lines do
  not record unmatched paths; a test asserts an unmatched value is never in the
  log file.
- No audit (not an audited action). Hook telemetry untouched.

## 13. Dependencies

No package or project reference added (see §21-a for the csproj diff and the
vulnerability scan output).

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| S-01, S-02 allowed/forbidden roles, endpoint enumeration | `CourseCardAuthorizationTests` (4) |
| S-04 read-only, no Google, no write | `CourseCardReadOnlyTests` (use case + page), `CourseCardQueryTests.TheQuery_DependsOnTheReadPortAndTheTimeZoneOnly`, `CourseCardSourceTests.Reading_WritesNothing` |
| S-05 value not echoed or logged | `CourseCardLoggingTests` (3), `CourseCardPageTests.AMalformedOrUnknownId_IsTheOrdinaryNotFoundPage` |
| S-06 URLs carry ids only | `CourseCardPageTests` link/address tests |
| S-07 no personal data in logs | `CourseCardLoggingTests.AShownCard_LogsTheCounts_AndNoCourseOrPersonData` |
| S-08 encoding | `CourseCardPageTests.GoogleData_IsHtmlEncoded` |
| S-10 no Google link/ids | `CourseCardSourceTests` displayable-values tests |
| TC-4 synthetic data, no live Google | fixtures in `CourseCardTestData`; fake source |

## 15. Abuse Case Review

| Scenario | Expected | Evidence | Status |
|---|---|---|---|
| Anonymous fetch of a card by id | sign-in redirect | authorization test | PASS |
| Dean on forced password change opens a card | refused | authorization test | PASS |
| Script/markup in course description or names (Google data) | rendered as text | encoding test | PASS |
| Probing ids with `<script>`, `../`, `%0A`, very long or huge values | 404, value not echoed or logged | route constraint + parse; logging tests | PASS |
| Using the switcher return path to reflect input | return path rebuilt from parsed id | `TheReturnPath_IsTheCardUrl` | PASS |
| Opening a card in read-only mode | shown, nothing written | read-only tests | PASS |

## 16. Repository Hygiene

Change set (git status) contains only US-021 code, tests, artifacts and
workflow state. No secret-like file, connection string, `.env`, database file
or `.xlsx`. Live credential files were not opened.

## 17. Deviations

- **D-1** (route constraint spelling) — security-neutral: same accepted set,
  and Application re-parses.
- **D-2** (`RequestPath` redacted only for event 5463) — implements spec S-05
  for this Story; the host-wide behaviour is pre-existing (INFO-1).

No undocumented endpoint, field or permissive default.

## 18. Findings

### INFO-1 — Host-wide `RequestPath` still carries other path values (LOGGING)

- Evidence: `InstallationLogging` enriches from the ASP.NET request scope;
  `RefusedValueRedaction` removes `RequestPath` from event 5463 only.
- Observation: other routes with path values (e.g. Meet codes, meeting ids)
  still log their raw path on any in-request event. Pre-existing, outside
  US-021; the Owner has already chosen a technical Story (route template instead
  of raw path) to follow US-021.
- Correction: none in this Story. Loop-back: none.

### INFO-2 — Redaction keyed on a numeric event id (LOGGING)

- Evidence: `RefusedValueRedaction` matches `EventId.Id == 5463` and relies on
  the structure Serilog.Extensions.Logging gives `EventId`.
- Observation: a future event reusing 5463 would also lose `RequestPath`
  (harmless), and a change in the EventId representation would silently stop
  the redaction — but `CourseCardLoggingTests` would then fail, so the coupling
  is guarded. The technical Story in INFO-1 should replace this enricher.
- Correction: none. Loop-back: none.

### INFO-3 — Participant id carried in the card DTOs (DATA_EXPOSURE)

- Evidence: `CourseCardPerson`, `CourseCardTeacher`, `CourseCardLeaver` carry
  `ParticipantId`; `Card.cshtml` does not render it.
- Observation: an internal database id, not a Google id or personal data; not
  in any markup or URL. No action needed.

## 19. Positive Controls

- Deny-by-default with class-level `ViewCourses` policy, proven by allowed and
  forbidden tests and endpoint enumeration.
- Two-layer id validation (route constraint + ASCII-digit parse with overflow
  and zero handling); no read before validation.
- Identical 404 for malformed and unknown ids, value never echoed or logged
  (whole-file log assertions).
- Read-only by construction: no guard needed, no tracking, no write, no Google
  dependency — proven in Application.
- Google ids compared in SQL; never projected out of Infrastructure.
- Razor encoding for all Google data; CSS, not markup, for line breaks.
- URLs built from the parsed internal id only.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- No penetration test or dynamic scan; review is code, configuration and test
  based.
- Vulnerability scan limited to the NuGet advisory database used by
  `dotnet list package --vulnerable`.

### 21-a. Runtime evidence (delegated run)

| Command | Result |
|---|---|
| `dotnet --version` | 10.0.401 |
| `dotnet build ClassroomAgent.sln` | succeeded; 0 warnings, 0 errors; 7 projects |
| `dotnet test ClassroomAgent.sln` | Passed — 5 201 total, 5 201 passed, 0 failed, 0 skipped; 5 m 57 s |
| `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` | every project: no vulnerable packages (nuget.org + local SDK feed) |
| `git diff --stat HEAD -- '*.csproj'` | empty — no project file changed |

## 22. Verdict Rationale

The implementation report records a green build and suite, and the reviewer's
independent run confirms it (§21-a). Every SC item the Story touches is PASS;
no Critical, Major or Minor finding; the security requirements S-01 … S-12 are
implemented and covered by passing tests; no Open Decision is open. The three
Informational notes need no change in this Story. Verdict: **PASS** →
`HUMAN_PR_APPROVAL`.
