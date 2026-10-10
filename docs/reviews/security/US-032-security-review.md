---
artifact_type: security_review
story: US-032
version: 1
status: APPROVED
created_at: 2026-10-10T18:50:52Z
updated_at: 2026-10-10T18:50:52Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-032-meet-code-linking.md
    version: null
  - path: docs/specifications/US-032-spec.md
    version: 1
  - path: docs/decisions/US-032-open-decisions.md
    version: 2
  - path: docs/designs/api/US-032-openapi.yaml
    version: 1
  - path: docs/designs/api/US-032-api-design.md
    version: 1
  - path: docs/designs/database/US-032-db-design.md
    version: 1
  - path: docs/designs/database/US-032-entity-model.md
    version: 1
  - path: docs/tests/US-032-test-strategy.md
    version: 1
  - path: docs/tests/US-032-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-032-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 1
informational_findings: 4
security_sensitive: true
runtime_checks: FULL
---

# US-032 Security Review — Link meeting codes to courses

## 1. Executive Summary

**Result: PASS.** No Critical or Major findings. The Story adds one page with
three lists, a course-choice form, three state-changing POST endpoints, an
automatic linking step inside the synchronization run, six audit actions and a
purge extension. All of it works on stored data only — no Google port, no new
outbound flow.

Principal controls, each verified in code and by a passing test: both new
policies require an authenticated Admin or Dean; every write runs the read-only
guard first in `Application` and writes only the refused audit row; antiforgery
is enforced on every write; the two raw-SQL queries are parameterized; Razor
output is encoded; audit rows and log lines carry codes, course ids and counts
only — no email or name.

One Minor finding (a misplaced XML doc comment in `InstallationPolicies`) and
four Informational notes. Recommended next stage: `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

As the front-matter `inputs`. Spec v1 APPROVED at `HUMAN_SPEC_APPROVAL`
(2026-10-10); OD-001 … OD-009 resolved; no input is `SUPERSEDED`.

## 3. Security-Relevant Scope

Installation host (public port) only:

| Method | Route | Policy |
|---|---|---|
| GET | `/workspace/meet-codes` | `ViewMeetCodes` (Admin, Dean) |
| GET | `/workspace/meet-codes/{meetingCode}/course-choice` | `LinkMeetCodes` (Admin, Dean) |
| POST | `/workspace/meet-codes/{meetingCode}/link` | `LinkMeetCodes` |
| POST | `/workspace/meet-codes/{meetingCode}/confirmation` | `LinkMeetCodes` |
| POST | `/workspace/meet-codes/{meetingCode}/not-a-course-mark` | `LinkMeetCodes` |

Assets: organizer and participant emails (personal data, possibly of minors),
course names, account emails of the deciding Admin/Dean, audit rows.
Boundaries: browser → installation host; controller → Application use cases;
Application → persistence ports → PostgreSQL. Background: the linking step in
`RunSynchronizationUseCase`; the retention purge.

Not touched: Control Plane, `Contracts`, authentication, passwords, the
service-account key, Google scopes.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker available (Testcontainers ran).
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 4345 total, 4345 passed, 0 failed,
  0 skipped (4 m 36 s). Independently re-run for this review; matches the
  implementation report.
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` —
  no vulnerable packages in any of the 7 projects (nuget.org source).
- Delegated to `cheap-worker` (facts only): build, test, vulnerability scan.
  Code reading and every judgement were done in this review.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | Policies name Admin and Dean only (`InstallationSecurityServices`); matrix row "Привязка кода встречи Meet к курсу" — both roles |
| SC-2 Authentication | NOT_APPLICABLE | no sign-in change |
| SC-3 AllowedAdmin | NOT_APPLICABLE | no Admin login change |
| SC-4 Authorization | PASS | each action has `[Authorize(Policy=…)]`; `MeetCodesAuthorizationTests` (both roles allowed, anonymous → sign-in, restricted Dean → forced change, per operation); host-wide `TheAnonymousList_GainsNothing` tests still green; writes are POST only; antiforgery: `MeetCodeActionsHttpTests.AWriteWithoutTheAntiforgeryToken_Is400` per operation; forms render `@Html.AntiForgeryToken()` |
| SC-5 Read-only | PASS | `MeetCodeWrite.EnsureAllowedAsync` first in each write use case; refused row via `ServiceWriteScope`/`PermittedServiceWrite.AuditEvent`; then `ReadOnlyModeException` → 409. Linking step covered by the run's guard (spec FR-006; see INFO-1). Tests `MeetCodeChangeTests.InReadOnlyMode_…`, `MeetCodeActionsHttpTests.InReadOnlyMode_…` |
| SC-6 No DB UI | PASS | no diagnostic endpoint added |
| SC-7 Key | NOT_APPLICABLE | untouched |
| SC-8 Google | PASS | no Google port in any new type (AC-018); scoring and page read stored rows |
| SC-9 Channel | NOT_APPLICABLE | untouched |
| SC-10 Hygiene | PASS | one new log line with run id and two counts (`SyncLinkingStepCompleted`), `MeetLinkingLoggingTests.NoLogLine_CarriesTheCodeAnEmailOrACourseName`; config error names the key, not the value; error pages are the translated host pages |
| SC-11 Audit | PASS | six actions; factories in `AuditEvent` carry code, course ids, actor id/role, request id — no email/name; check constraints `ck_audit_event_meet_code_*`; `MeetCodeAuditEventTests`; purge count `purged_meet_code_links` |
| SC-12 Owner | NOT_APPLICABLE | no Control Plane / Contracts change |
| SC-13 Outbound | PASS | no new destination |
| SC-14 no-store | PASS | global `NoStoreMiddleware` (US-040) covers the new pages (INFO-3) |

## 6. Authentication and Authorization

Both roles hold both policies, so the forbidden case of every endpoint is the
unauthenticated request (spec §7) — tested per operation. The actor id and role
come from the session claims (`ActorId()`, `ActorRole()`), never from the
request. No ownership or roster scoping, per the matrix (S-03 of earlier Stories).

## 7. Credentials, Key and Google Access

No passwords, key or Google access in scope. The linking step and the page read
`meet_session`, `meet_participation`, `classroom_participant`,
`course_membership`, `course` and `app_user` only.

## 8. Sensitive Data Exposure

- Views show organizer emails and the emails of the accounts that linked,
  confirmed or marked — to Admin and Dean only, as the spec approves. All text is
  Razor-encoded; the one hand-built fragment (`MarkForm`) uses `Append` (encoded)
  for the code URL and the translated label, and `AppendHtml` only for literals
  and integers.
- The code travels in the route, `Uri.EscapeDataString`-encoded; the query
  string carries only `list`, `page`, `size`, `returnPage` (I-14).
- Page models are DTOs under `Application/Models/Dtos` (AD-8); no entity crosses
  into the view.
- Audit rows: no personal data. Logs: counts only.

## 9. Input Validation

- Code: non-blank, ≤ 64 chars, no control characters (`IsWellFormedCode`).
- Ids: ASCII digits, positive `long`. `returnPage`: digits, `int`, else 0.
- Expected state: closed set, case-sensitive; `expectedCourseId` required
  exactly for `linked`.
- Known form fields at most once; unknown fields ignored — none can set a role,
  actor or state.
- Paging: `page` 0 … `int.MaxValue`, `size` 1 … 100; repeated parameter → 400.
  Offset computed as `long` in SQL; LINQ skip clamped.
- Database values encoded on output (VR-006).

## 10. API Security

Only the five operations of the OpenAPI contract exist. The success redirect is
built from server values (list enum, integer page) — no open redirect. Outcomes
map to 400/404/409 per api-design §2.7; read-only refusal 409 via the host's
exception handler. Deviation 6 of the implementation report (no `readOnly` flag
in the page model) does not weaken enforcement (AD-6).

## 11. Persistence and Configuration

- Raw SQL: `MeetCodesReadSource.GetUnassignedPageAsync` and
  `MeetCodeScoringSource.GetUnassignedCodesAsync` use `Database.SqlQuery` with
  an interpolated `FormattableString` — every value (`zoneId`, `size`,
  `offset`, `afterCode`, `batchSize`) is a parameter. No string concatenation.
- Migration `20261010162545_AddMeetingCodeLinks` (PC-2); unique code; check
  constraints on link and audit shape. No `EnsureCreated`.
- `UnitOfWork` translates the unique/concurrency conflict to
  `MeetingCodeLinkConflictException` (INFO-2).
- Config: two optional integer keys, 1 … 100, startup fails naming the key.
  No appsettings file changed; no connection string added.

## 12. Logging, Audit and Telemetry

Covered in §5 (SC-10, SC-11). Audit rows are only inserted; the purge remains
the only delete path. Hook telemetry untouched.

## 13. Dependencies

No `.csproj` changed; no package or project reference added. Vulnerability scan
clean (§4).

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| Allowed / forbidden per endpoint (TC-5) | `MeetCodesAuthorizationTests` (3 theories over every operation) |
| No new anonymous endpoint | host-wide anonymous-list tests |
| Antiforgery | `MeetCodeActionsHttpTests.AWriteWithoutTheAntiforgeryToken_Is400` |
| Read-only in Application | `MeetCodeChangeTests.InReadOnlyMode_…` per write |
| Audit rows | `MeetCodeAuditEventTests`, `MeetingCodeLinkSchemaTests` |
| No PII in logs | `MeetLinkingLoggingTests` |
| No live Google | AC-018; substituted ports, synthetic `MeetLinkingTestData` |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Direct POST in read-only mode | 409, refused row, no change | `EnsureAllowedAsync` first; tests | PASS |
| POST without antiforgery token | 400, nothing stored | antiforgery test | PASS |
| Anonymous request to any operation | sign-in redirect | authorization tests | PASS |
| Actor id / role injected in the form | ignored | actor from claims; unknown fields ignored | PASS |
| SQL injection via code or paging | parameterized | §11 | PASS |
| XSS via code, email or course name from Google | encoded | §8 | PASS |
| Repeated known field / oversized code | 400 | `Fields()` returns null; length check | PASS |
| Concurrent change racing a person's decision | 409 stale, no overwrite | expected-state check + unique/concurrency translation | PASS |

## 16. Repository Hygiene

No new `.db`, `.xlsx` or credential-like file in the change set; `classroom_cache.db`
remains ignored; credential files not opened.

## 17. Deviations

Implementation report §7 items reviewed:
1. Unit of work passed as argument to `LinkMeetCodesStep` — acceptable, see INFO-1.
2. Detaching failed entries — acceptable, see INFO-2.
3. Timestamp interceptor — no security effect.
4. Paging offset and SQL alias fixes — verified the result is still parameterized.
5. Test fixture changes — no assertion weakened in the security tests reviewed.
6. No `readOnly` flag — enforcement remains in Application.
7. Unique constraint instead of unique index — same guarantee.

## 18. Findings

**MIN-1** — Minor — CONFIGURATION — `src/ClassroomAgent.Application/Authorization/InstallationPolicies.cs`.
Observed: `ViewMeetCodes` and `LinkMeetCodes` were inserted between the XML doc
comment of `UseReportTemplates` and its constant, so that comment now documents
`ViewMeetCodes`; `UseReportTemplates` and `LinkMeetCodes` have none. Expected:
each policy constant documents its matrix row. Risk: a reader of the policy list
gets the wrong matrix row for a policy — no runtime effect. Correction: move the
two constants after `UseReportTemplates` with their own doc comments. Loop-back:
none required (may be fixed before the final commit). Verify: build.

**INFO-1** — READ_ONLY_MODE — `LinkMeetCodesStep` is public and DI-registered and
takes the unit of work as an argument, so the `WritePathRule` architecture test
does not see it as a write path. Today its only caller is the guarded
`RunSynchronizationUseCase` (verified by search). A future caller would bypass
the read-only guard silently. Suggestion for a later Story: an architecture test
pinning its only consumer. Mid-run read-only changes are not re-checked — as spec
FR-006 approves.

**INFO-2** — AUDIT — `UnitOfWork.DetachFailedMeetCodeEntries` detaches every
`Added` `AuditEvent` in the tracker, not only the link's. In the two scopes that
reach it (a link write, the linking step) the only pending audit row is the
link's own, which was never saved; no committed audit row can be lost.

**INFO-3** — DATA_EXPOSURE — `no-store` (spec §7) comes from the global
`NoStoreMiddleware` (US-040), whose host-wide tests cover every response; no
US-032-specific assertion exists.

**INFO-4** — OTHER — `CodeNotFound` (404) distinguishes known from unknown
codes; both roles may view every code anyway, so nothing is disclosed.

## 19. Positive Controls

Policies on every action; claims-derived actor; read-only guard first with
refused audit row; antiforgery on all writes; parameterized raw SQL; encoded
output; DTO-only views; PII-free audit and logs; DB check constraints on audit
shape; no new package; no outbound flow.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

Code, configuration and test review plus a full test run — no penetration
testing. Vulnerability scan relies on the nuget.org advisory database.

## 22. Verdict Rationale

Build and the full suite are green (re-run independently); every touched SC
item is PASS; required security tests exist and pass; no Critical or Major
finding; no open security decision. MIN-1 and INFO-1 … INFO-4 are
non-blocking. Verdict **PASS** → `HUMAN_PR_APPROVAL`.
