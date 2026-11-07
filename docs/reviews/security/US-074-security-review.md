---
artifact_type: security_review
story: US-074
version: 1
status: APPROVED
created_at: 2026-11-07T06:20:51Z
updated_at: 2026-11-07T06:20:51Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-074-meet-code-organizer-by-name.md
    version: null
  - path: docs/specifications/US-074-spec.md
    version: 1
  - path: docs/decisions/US-074-open-decisions.md
    version: 1
  - path: docs/designs/api/US-074-api-design.md
    version: 1
  - path: docs/designs/api/US-074-openapi.yaml
    version: 1
  - path: docs/designs/database/US-074-db-design.md
    version: 1
  - path: docs/designs/database/US-074-entity-model.md
    version: 1
  - path: docs/tests/US-074-test-strategy.md
    version: 1
  - path: docs/tests/US-074-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-074-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-074-implementation-verification.md
    version: 1
  - path: trebovaniya.md
    version: 112
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 2
security_sensitive: true
runtime_checks: FULL
---

# US-074 Security Review

## 1. Executive Summary

**Result: PASS.** 0 Critical, 0 Major, 0 Minor, 2 Informational.

The Story changes a read-only view: the three Meet code lists show each organizer's stored Classroom name next to the email,
and the page and header searches also match that name. The review confirmed that authorization is unchanged, no Google call
or outbound flow was added, the name is rendered with Razor encoding only, the search word stays a bound SQL parameter, an
erased or excluded organizer cannot come back through a name, and the search text reaches no log or audit row. An independent
build (0 warnings) and a full test run (7759/7759, 0 skipped) are green. No package, configuration, migration or policy change.

Recommended next stage: `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

As in the front matter `inputs`. The Specification is `APPROVED` (HUMAN_SPEC_APPROVAL recorded in `history.jsonl`); designs and
test artifacts are `DRAFT` v1, the normal state of un-reviewed inputs in this variant; none is `SUPERSEDED`.

## 3. Security-Relevant Scope

- **Host:** Data Plane (`ClassroomAgent.Web`) only. Changed: `GET /workspace/meet-codes` (three lists, page search) and the
  header search `GET /search`, which reuses the same read (`GetFirstMatchesAsync`, `GetCountsAsync`). No Control Plane change.
- **Assets:** teacher names and emails (personal data already shown to Admin and Dean in the Meet reports and teacher list);
  the search text (may hold a name, SC-10).
- **Trust boundaries:** browser → Web (query parameter `q`, unchanged validation); Web → Application use case
  (`GetMeetCodesQuery`); Application → `IMeetCodesReadSource` → PostgreSQL. No Google port, no Control Plane channel.
- **Changed components:** `MeetCodesReadSource` (`OrganizerNames`, `LoadOrganizersAsync`, `CodesByOrganizer`), `GetMeetCodesQuery`
  mapping, row models and DTOs, `_MeetCodeOrganizers.cshtml`, `MeetCodes/Index.cshtml`.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker Desktop 29.8.0 running; Testcontainers `postgres:17-alpine`.
- `dotnet build ClassroomAgent.sln` — exit 0, 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln --no-build` (06:08–06:20Z) — **7759 total, 7759 passed, 0 failed, 0 skipped**, 12 min 05 s.
- `dotnet list ClassroomAgent.sln package --vulnerable` (nuget.org) — no vulnerable package in any of the seven projects.
- `git diff`, `git status`, `git check-ignore`, ripgrep over the changed files.
- Delegation: none. The change set is nine production files; reading it and running the three commands inline was cheaper than
  briefing a subagent. The previous attempt of this stage (2026-11-06 21:10Z) was stopped on Docker/memory trouble and left no
  artifact; this is the first completed review.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role, policy or seed change (`git diff` of `src/` touches no `Security` namespace). |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in, password or cookie change. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | No login path change. |
| SC-4 Authorization | PASS | `ViewMeetCodes` and the header search policy unchanged; `MeetCodeOrganizerNameAccessTests`: Admin and Dean allowed on both searches, anonymous sent to sign-in, a restricted Dean sent to the forced password change; existing `PermissionMatrixTests`, `ListSearchAccessTests` green. No new `[AllowAnonymous]`, no new endpoint, both changed routes are GET reads. |
| SC-5 Read-only mode | PASS | Reads only; `InReadOnlyMode_TheSearchWorks_AndCallsNoGoogleApi` asserts both searches work under a suspended installation with `GoogleCallCount() == 0`. |
| SC-6 No DB UI | NOT_APPLICABLE | No diagnostic surface added. |
| SC-7 Key | NOT_APPLICABLE | Not touched. |
| SC-8 Google | PASS | No Google port used by the changed code; names come from stored `ClassroomParticipant` rows. |
| SC-9 Channel | NOT_APPLICABLE | Not touched. |
| SC-10 Hygiene | PASS | No logger call in the changed files; `TheSearchedName_IsInNoAuditRow_AndNoLogLine` checks every log event and audit row after both searches. No error path added. |
| SC-11 Audit | NOT_APPLICABLE | Viewing is not audited; no audited action added. `ReadingAndSearchingNames_WritesNothing` proves no write. |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` and Control Plane untouched. |
| SC-13 Outbound | PASS | No new destination; no HTTP client or port added. |

## 6. Authentication and Authorization

No policy, endpoint or controller change. Allowed roles Admin and Dean (`trebovaniya.md` §2, Meet codes view) and forbidden
callers are each proved by a test (§5, SC-4). The read takes no identifier from the request other than the existing validated
`q`, list and page parameters, so no foreign record can be addressed. An installation has one school's data (AD-1), so the
participants consulted for a name cannot belong to another school.

## 7. Credentials, Key and Google Access

Not touched. No scope, impersonation or key handling changed.

## 8. Sensitive Data Exposure

- **Views:** `_MeetCodeOrganizers.cshtml` writes `@organizer.FullName` and `@organizer.Email` through Razor encoding; no
  `Html.Raw` in `Views/MeetCodes/`. `TheName_IsEncoded` seeds `Zz<b>&"Ім'я` and asserts the encoded output and the absence of the
  raw tag.
- **DTOs:** the DTOs carry `PersonRef` (name, email); no domain entity crosses into the view (AD-8). `OrganizerNameRow` is private
  to Infrastructure; `MeetOrganizerRecord` is an Application model.
- **Data minimisation:** only `FullName` and `Email` are read from participants; `Surname`, `GivenName`, Google ids are not
  projected. The data shown is already shown to the same roles in the Meet reports (spec §7).
- **Erasure and exclusion:** a name is joined only through a non-null `MeetSession.OrganizerEmail`. `ParticipantErasureStore`
  clears the organizer email of every session of the erased email (`ClearOrganizer`) and deletes the participant row, so neither
  branch of `CodesByOrganizer` nor `LoadOrganizersAsync` can reach the person. Proved by
  `AClearedOrganizer_IsNotFoundByName_AndNotListed_ASurvivorStillIs` and `AnErasedOrganizer_IsNotFound_AndNotShown_TheSurvivorStays`.
- **Logs, audit, exceptions, exports, telemetry:** no change; see SC-10. Exports untouched.

## 9. Input Validation

No new input. `q` keeps US-070 VR-001…VR-004. The name read from storage is output only (VR-002).

## 10. API Security

No new endpoint or response field outside the approved design: the three DTOs replace `OrganizerEmails` with
`Organizers : IReadOnlyList<PersonRef>` as the OpenAPI `MeetCodesPageModelDelta` records. Methods remain GET for reads. Errors
unchanged.

## 11. Persistence and Configuration

- **SQL:** all queries are LINQ; the search word passes through `SearchMatch.InField` (bound parameter, VR-003,
  `ANameWord_IsABoundParameter`); the page's email list is a parameter array in `lowered.Contains(...)`. No `FromSql` or string
  concatenation added. The pre-existing raw unassigned-page query is unchanged.
- **Schema:** none; no migration needed (db-design §8). No configuration file changed.
- **Volume:** the name branch is a correlated subquery; the measurement test (1000 students, 300 teachers, 300 codes) shows
  ~100 ms per search — no resource-exhaustion concern at the expected volume.

## 12. Logging, Audit and Telemetry

No log statement, audit event or telemetry change. `docs/hooks/tool-usage.jsonl` and `phase-timing.jsonl` remain git-ignored
(`.gitignore` lines 66, 68).

## 13. Dependencies

No `*.csproj`, `Directory.*` or package change. Vulnerability scan: none reported (§4).

## 14. Security Test Coverage

| Requirement | Test |
|---|---|
| Allowed roles on both searches (TC-5) | `MeetCodeOrganizerNameAccessTests.AnAdminAndADean_FindTheCode_ByTheOrganizersName` |
| Forbidden callers (TC-5) | `Anonymous_IsSentToSignIn`, `ARestrictedDean_IsSentToTheForcedChange`; `PermissionMatrixTests` |
| Read-only mode, no Google call | `InReadOnlyMode_TheSearchWorks_AndCallsNoGoogleApi` |
| Search text not in logs or audit (SC-10) | `TheSearchedName_IsInNoAuditRow_AndNoLogLine` |
| Encoding | `MeetCodeOrganizerNamePageTests.TheName_IsEncoded` |
| Bound parameter | `MeetCodeOrganizerNameSourceTests.ANameWord_IsABoundParameter` |
| Erased organizer invisible | the two AC-004 tests in §8 |
| No write | `ReadingAndSearchingNames_WritesNothing` |

All green in the full run.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Name with HTML/script stored in Google | Razor encoding | `TheName_IsEncoded` | PASS |
| SQL metacharacters in the search word | bound parameter | `ANameWord_IsABoundParameter` | PASS |
| Search for an erased teacher's former name | no match through a cleared email | AC-004 tests | PASS |
| Anonymous or restricted session calls the search | sign-in / forced change | access tests | PASS |
| Search in read-only mode triggers Google | no port call | read-only test | PASS |
| Search text leaks to logs | not logged | log test | PASS |

## 16. Repository Hygiene

Working tree: the Story's code, tests and artifacts, workflow state files. No secret-like file, database file or `.xlsx` in the
change set; `google_credentials.json` and `classroom_cache.db` remain ignored (`.gitignore` lines 3, 15). Credential files not
opened.

## 17. Deviations

D-1…D-3 (implementation verification) are presentation and EF Core shape choices with no security effect; D-2 keeps Razor
encoding. No security deviation.

## 18. Findings

- **INFO-1** (DATA_EXPOSURE, SC-10) — `trebovaniya.md` §5 / memory note on `?q=`: the search text, now possibly a teacher's name, is
  in the query string of `/workspace/meet-codes` and `/search`. This is unchanged since US-070/US-071 (v108 decision) and the
  application does not log it (test above); the open pilot check that a reverse proxy, if any, does not log `?q=` now covers names
  of teachers too. No correction required in this Story.
- **INFO-2** (DATA_EXPOSURE) — the chosen name is a participant's name of *any* role with that email (lowest id with a name), the
  rule the Meet reports already use (AC-009). If a student's account were an organizer, the student's stored name would show to
  Admin and Dean — data those roles already see in the Meet reports and journals. No new exposure; recorded for completeness.

## 19. Positive Controls

Razor encoding of the name and email; bound SQL parameters; deny-by-default policies unchanged and proved; read-only mode with
no Google call proved; erasure clears the join key and deletes the participant; no logging of the search text proved; no new
package; green build and full suite.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

Code, configuration and test review plus the full automated suite; no penetration testing, no browser run. The Docker test
database only; no live data.

## 22. Verdict Rationale

The implementation report records a green build and suite, independently reproduced here (7759/7759). Every touched SC item is
PASS with test evidence, no Critical, Major or Minor finding, no unresolved security decision. **PASS.**

The Story's OPEN findings `US-074/IMPLEMENTATION/m-1` (test tightening), `INFO-1`, `D-1`, `D-2`, `D-3` of the implementation
stages are outside security-review responsibility and stay OPEN for the human gate.
