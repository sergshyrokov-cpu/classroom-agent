---
artifact_type: security_review
story: US-031
version: 2
status: APPROVED
created_at: 2026-10-10T07:45:05Z
updated_at: 2026-10-10T08:05:54Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-031-meet-events-pull.md
    version: null
  - path: docs/specifications/US-031-spec.md
    version: 1
  - path: docs/decisions/US-031-open-decisions.md
    version: 2
  - path: docs/designs/api/US-031-api-design.md
    version: 1
  - path: docs/designs/api/US-031-openapi.yaml
    version: 1
  - path: docs/designs/database/US-031-db-design.md
    version: 1
  - path: docs/designs/database/US-031-entity-model.md
    version: 1
  - path: docs/tests/US-031-test-strategy.md
    version: 1
  - path: docs/tests/US-031-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-031-implementation-report.md
    version: 2
  - path: trebovaniya.md
    version: 87
supersedes:
  path: docs/reviews/security/US-031-security-review.md
  version: 1
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 4
security_sensitive: true
runtime_checks: FULL
---

# US-031 Security Review — Pull Meet events and keep history beyond 180 days

## 1. Executive Summary

**Result: PASS.** No Critical or Major findings.

The Story adds a background read of Google Admin Reports (Meet `call_ended`
events), two tables holding personal data of minors (`meet_session`,
`meet_participation`), a watermark and a failed-step column on `sync_state`, a
purge step and two audit counts, and two lines on the Admin-only connection page.
It adds no endpoint, no request input, no setting, no package and no outbound
destination.

Principal controls verified in code and tests: the reports scope only, as the
technical account; the key resolved per call through the existing secret store;
nothing but the FR-003 values leaves the adapter; emails kept only for domain
accounts; no event value in any log line; the read-only guard covers the step;
purge counts carry integers only.

**Attempt 2.** The human rejected the first `HUMAN_PR_APPROVAL` to have M-1 of
v1 fixed. `MeetEventValidator.IsUsableEmail` now also requires exactly one `@`
with a non-empty part before it, applied to the organizer (FR-005) and the
participant (FR-008) before the domain test; four new test cases cover both
shapes on both paths. M-1 is **resolved**. The rest of the change is byte-identical
to what v1 reviewed (only `MeetEventValidator.cs` and `MeetEventValidationTests.cs`
are newer than v1). No Minor findings remain; four Informational notes
(I-1…I-3 carried, I-4 new). Recommended next stage: `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

As listed in the front matter. Specification v1 is `APPROVED`
(`HUMAN_SPEC_APPROVAL` recorded 2026-10-10T05:46:13Z in `history.jsonl`). No input
is `SUPERSEDED`. Workflow: stage `SECURITY_REVIEW`, implementation attempt 2,
security review attempt 2. Supersedes security review v1, which reviewed
implementation report v1.

## 3. Security-Relevant Scope

| Area | Change |
|---|---|
| Hosts | Data Plane only. Control Plane untouched. |
| Endpoints / pages | None new. `WorkspaceConnection/Index` gains two read-only lines (watermark, failed step). |
| Google access | New adapter `GoogleMeetReportsReader` (`activities.list`, application `meet`, event `call_ended`). |
| Persistence | `meet_session`, `meet_participation`; `sync_state.meet_loaded_up_to`, `failed_step`; `audit_event.purged_meet_sessions`, `purged_meet_participations`. Migration `20261010064711_AddMeetPull`. |
| Write use cases | `RunSynchronizationUseCase` (Meet step), `RunRetentionPurgeUseCase` (meeting step). |
| Logging | `SyncMeetStepCompleted` (5132), `SyncMeetEventsSkipped` (5133), `SyncRunFailed` gains `{Step}`. |

**Assets:** domain participants' emails and attendance times (personal data of
minors, PC-9); organizer emails; the service-account key; audit rows.

**Trust boundaries:** Application → `IMeetReportsReader` port → Google (HTTPS,
SC-13); Application → PostgreSQL; Admin browser → connection page (existing).

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker available (Testcontainers ran).
- `dotnet build ClassroomAgent.sln`: 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` (attempt 2): **3986 total, 3986 passed, 0
  failed, 0 skipped** (4 min 01 s) — an independent run, matching implementation
  report v2. The targeted run of `MeetEventValidationTests`, `SchoolDomainAccountTests`,
  `MeetPullTests`, `MeetReadOnlyTests`, `MeetPullLoggingTests`: 73 passed.
- Attempt-2 scope check: files under `src/` and `tests/` modified after v1 =
  `MeetEventValidator.cs`, `MeetEventValidationTests.cs` only.
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive`:
  no vulnerable packages in any of the 7 projects (nuget.org source).
- `git diff` of `*.csproj` / `Directory.*.props`: empty.

**Delegated** (v1: one `cheap-worker`, read only): build, the full test run, the
vulnerability scan and the project-file diff. Attempt 2: build and both test runs
were executed inline; the vulnerability scan and project-file diff carry over from
v1 (no `*.csproj` changed since). Code reading and every judgement were done inline.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role, policy or seed changed. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in or password path changed. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Untouched. |
| SC-4 Authorization | PASS | No endpoint added (openapi delta: none); the block is rendered on the existing Admin-only `WorkspaceConnection/Index`. No `[AllowAnonymous]`, `IgnoreAntiforgeryToken`, `Html.Raw` or raw SQL in changed files (sweep empty). |
| SC-5 Read-only mode | PASS | The existing guard at the start of `ExecuteAsync` precedes both steps; `MeetReadOnlyTests` asserts `world.Meet.Calls` is empty and nothing is added per reason. The meeting purge step is part of the BR-026 retention purge. |
| SC-6 No DB UI | NOT_APPLICABLE | Nothing added. |
| SC-7 Key | PASS | `GoogleMeetReportsReader.LoadKey` resolves `GoogleServiceAccountSettings.KeyReference` through `ISecretStore` per call; the key is never stored, logged or returned; no setting, column or UI added (impl report §6). |
| SC-8 Google | PASS | Scopes = `[GoogleDelegationScopes.AdminReportsAuditReadonly]` only; subject = `WorkspaceConnection.SavedImpersonationUserEmail` (technical account, BR-015); only `Activities.List` is called. Permission failures (403 → `ClassifyApiError`, token refusals → `ClassifyTokenError`) become `Configuration` diagnoses, not retried and not swallowed. Tested by `TheTokenRequest_ImpersonatesTheTechnicalAccount_WithTheReportsScopeOnly` and `GoogleMeetReportsReaderRetryTests`. |
| SC-9 Channel | PASS | Domain matched against `connection.SavedDomain` of a usable connection (constrained to the `Installation` domain by US-009); a connection without a domain skips the run. |
| SC-10 Hygiene | PASS | Log lines carry counts, instants, enum names and the run id only; `MeetPullLoggingTests` asserts no event value appears in any line. `GuardAsync` carries no exception message, reason or body; `SyncState` stores enum names only. |
| SC-11 Audit | PASS | `AuditEvent.RetentionPurgeRun` gains two non-negative integer counts, with DB check constraints (`ck_audit_event_purge_meet_counts*`); `RetentionPurgeMeetAuditEventTests`. No update/delete path added. The pull itself is not a user action (OD-008). |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` and `ControlPlane` untouched. |
| SC-13 Outbound | PASS | The adapter's own `SocketsHttpHandler` (no redirects, no cookies) reaches Google only; test asserts every request host ends in `googleapis.com`. |

## 6. Authentication and Authorization

No endpoint, page or policy is added. The two new lines render inside the
existing connection page, whose policy (Admin only, US-017 FR-008) is unchanged.
The Meet step runs only in the background service, which reaches it through the
same `RunSynchronizationUseCase` entry as before — no bypass path is added. No
identifier from a request reaches the step (VR-004).

## 7. Credentials, Key and Google Access

- No password handling.
- Key: see SC-7. A missing or unparsable key ends the call as
  `Configuration/KeyUnavailable` before any request is sent.
- Scope and subject: see SC-8. `GZipEnabled = false`, no library back-off; every
  attempt goes through `GoogleRetryHandler` (US-017 parameters); the retry log
  line carries attempt, HTTP status and pause only.

## 8. Sensitive Data Exposure

- **Adapter boundary**: `Map` copies only the eight FR-003 values into
  `MeetCallEndedEvent`; no SDK type crosses the port (AD-4). No telemetry,
  device, location, display name, IP or `is_external` (AC-013, `GoogleMeetReportsReaderTests`).
- **Minimisation**: `ConnectionOf` keeps a participant email only when the
  identifier type is `email_address`, the value fits 254 characters, has exactly
  one `@` with a non-empty part before it, and its domain equals the school's
  exactly; otherwise `null` ("other participant").
  Organizers stored are always well-formed domain accounts (same check, FR-005). Non-school conferences are never
  written (held in memory only, see I-1).
- **View / DTO**: `LastSynchronizationView` gains a local `DateTime?` and a
  `SyncStep?` — no personal data; Razor encodes output.
- **Logs / audit / exceptions**: see SC-10, SC-11.
- **Exports, telemetry**: none touched.
- **Test fixtures**: synthetic (`MeetTestData`, `FakeMeetReportsReader`).

## 9. Input Validation

Google events are external input (§8, SC-10), validated in `Application` by
`MeetEventValidator.Check` before anything is written: required conference id
(≤128), meeting code (≤64), endpoint id (≤128), event time within the window ±1
day, duration 0…86 400; values trimmed. A rejected event is counted by reason and
never logged (`MeetEventValidationTests`). The database repeats the limits
(`varchar` lengths, `ck_meet_participation_duration`, `ck_meet_*_values`) —
defence in depth.

Organizer and participant addresses (VR-001/FR-005/FR-008): `IsUsableEmail` —
present, ≤254, exactly one `@`, non-empty local part — gates `IsDomainAccount` at
both call sites in `RunSynchronizationUseCase` (lines 217–218, 278–279). A failing
address is "not a domain account" / "other participant", never an invalid event.
M-1 of v1 is resolved.

## 10. API Security

No HTTP surface change; the OpenAPI delta declares none and none was found.

## 11. Persistence and Configuration

- Schema exactly as db-design: no name, telemetry or display-name column;
  `email` nullable `varchar(254)`; no FK to `classroom_participant`, `course`.
- `RESTRICT` FK; purge deletes participations then sessions in one transaction
  per 500-id batch (`TryUnitAsync`), so no half-deleted meeting.
- Schema only via migration `AddMeetPull`; no `EnsureCreated`.
- `ExecuteDeleteAsync` with LINQ — no raw SQL.
- No configuration change; no connection string or secret added.

## 12. Logging, Audit and Telemetry

Verified the message templates and arguments of 5132, 5133, 5123 and 2420 (see
SC-10). Audit: purge counts only. `docs/hooks/tool-usage.jsonl` remains
git-ignored (`.gitignore:63`).

## 13. Dependencies

No package or project reference added (`*.csproj` diff empty); the adapter uses
`Google.Apis.Admin.Reports.reports_v1`, already referenced. Vulnerability scan:
none reported. Layering: `IMeetReportsReader` in `Application/Ports`, adapter in
`Infrastructure/Google`; no `DbContext` in `Web`.

## 14. Security Test Coverage

| Requirement | Test |
|---|---|
| Reports scope only, technical account subject | `GoogleMeetReportsReaderTests.TheTokenRequest_ImpersonatesTheTechnicalAccount_WithTheReportsScopeOnly` |
| Only Google hosts contacted | `GoogleMeetReportsReaderTests` (host assertion) |
| No extra field leaves the adapter | `GoogleMeetReportsReaderTests`, `MeetSchemaTests` |
| Permission failure not retried, classified | `GoogleMeetReportsReaderRetryTests`, `MeetStepFailureTests` |
| Read-only: no reader call (each reason) | `MeetReadOnlyTests` |
| Only domain emails stored | `MeetPullTests`, `SchoolDomainAccountTests`, `MeetSessionInvariantTests` |
| Malformed email not a domain account (M-1 fix) | `MeetEventValidationTests.AMalformedOrganizer_IsNotADomainAccount_NotAnInvalidEvent`, `.AMalformedParticipant_IsAnOtherParticipant` (`@domain`, `a@b@domain`) |
| No event value in logs | `MeetPullLoggingTests` |
| Purge audit counts, no PII | `RetentionPurgeMeetAuditEventTests` |
| No live Google | all host tests substitute `IMeetReportsReader`; adapter tests use a scripted transport (TC-4) |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Sync triggered while read-only | Guard refuses before any read | `MeetReadOnlyTests` | PASS |
| Externally organized meeting a pupil joined | Nothing stored | `MeetPullTests` (AC-004) | PASS |
| Guest / subdomain participant | Stored without email | `MeetPullTests` (AC-005) | PASS |
| Malformed address ending in the school domain | Not a domain account; no email stored | `MeetEventValidationTests` (M-1 theories) | PASS |
| Oversized or out-of-window event values | Skipped, counted, not logged | `MeetEventValidationTests`, `MeetPullLoggingTests` | PASS |
| Reports scope revoked by the school | Run fails with config diagnosis, no retry, watermark kept | `MeetStepFailureTests` | PASS |
| Inspecting logs/page for personal data | None present | §8, §12 | PASS |

## 16. Repository Hygiene

`google_credentials.json`, `dac-classroom-agent-*.json` and `classroom_cache.db`
remain git-ignored (`.gitignore` lines 3, 4, 12); not opened. No secret-like,
database or `.xlsx` file among the changed or untracked files.

## 17. Deviations

- Implementation report §7.1 (undecided conferences held across pages) — follows
  FR-005 "read in this run"; no security impact beyond I-1.
- M-1 of v1 resolved in attempt 2; no remaining deviation from VR-001/FR-005.
- No undocumented security behaviour found.

## 18. Findings

### M-1 — RESOLVED in attempt 2 (v1: "malformed" email checked by length only)
- **Resolution verified:** `IsUsableEmail` rejects an empty local part and a second
  `@`; used before `IsDomainAccount` for organizer and participant; four test cases
  pass. Kept below for the record.
- **Severity (v1):** Minor · **Category:** INPUT_VALIDATION · **SC:** SC-10 (external input)
- **Where:** `Application/Validation/MeetEventValidator.IsUsableEmail`;
  `Domain/Rules/SchoolDomainAccount.IsDomainAccount`.
- **Observed:** an address counts as a domain account when it is 1…254 characters
  and the text after its **last** `@` equals the domain. `@school.example` or
  `a@b@school.example` would qualify.
- **Expected:** spec FR-005 and VR-001 say a malformed email counts as "not a
  domain account".
- **Risk:** low — the values come from Google's own audit log, which records real
  account addresses; a crafted value would need Google to emit it. Effect would
  be one odd email stored for a meeting, not exposure.
- **Correction:** reject an address with an empty local part or more than one
  `@` (or reuse the existing email validator of US-009), plus a test. May be
  done now or tracked; does not block.
- **Loop-back:** IMPLEMENTATION if fixed now.
- **Verify:** a unit test with both shapes above.

### I-1 — Undecided conferences held in memory for the whole run
Events of not-yet-decided conferences (`MeetProgress._undecided`) are kept until
the run ends. They include non-domain participant identifiers, in memory only,
never written or logged — acceptable. On a large first pull the set grows with
the number of externally organized meetings; a memory concern, not a disclosure.

### I-2 — No guard against a repeating page token
`ReadCallEndedAsync` loops while Google returns a next-page token. A buggy
response repeating the same token would loop until cancellation. Same pattern as
the Classroom reader; noted only.

### I-3 — Field names and `email_address` unverified (OD-009, §7 item 28)
If the live identifier type differs, every participant becomes an "other
participant" — the mapping fails closed (no email stored), which is the safe
direction. The Owner's live check at first deployment stays required.

### I-4 — The four M-1 test cases are not yet in `ac-test-matrix` v1
The new theories in `MeetEventValidationTests` trace to VR-001/FR-005 and are
recorded in implementation report v2, but the AC→test matrix was not updated.
Traceability only; no security impact.

## 19. Positive Controls

Read-only reports scope and technical-account subject (tested); per-call key
resolution through the secret store; an adapter-local transport with no
redirects or cookies; mapping to eight values at the port; well-formed, domain-only email
retention; Application-layer validation mirrored by DB constraints; integer-only
audit counts with DB checks; logs with counts and enum names only (tested);
read-only guard covering the new step (tested); purge batches atomic per meeting.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001…OD-011 are resolved;
OD-009 leaves `trebovaniya.md` §7 item 28 open by design (live check at
deployment), which fails closed (I-3).

## 21. Review Limitations

- No live Google domain: field names (I-3) and real error shapes are checked only
  against synthetic recorded-shape responses.
- Code, configuration and test review — no penetration test.
- Vulnerability scan limited to the nuget.org advisory data at run time.

## 22. Verdict Rationale

Green build and an independent green full test run (3986/3986); M-1 of v1 fixed
and tested; no Critical, Major or Minor finding; every touched SC item is PASS with
code and test evidence; no blocking Open Decision. I-1…I-4 are non-blocking. Verdict **PASS** →
`HUMAN_PR_APPROVAL`.
