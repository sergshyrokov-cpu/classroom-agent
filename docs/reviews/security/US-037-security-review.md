---
artifact_type: security_review
story: US-037
version: 2
status: APPROVED
created_at: 2026-10-03T20:08:23Z
updated_at: 2026-10-03T20:46:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-037-retention-purge.md
    version: null
  - path: docs/specifications/US-037-spec.md
    version: 1
  - path: docs/decisions/US-037-open-decisions.md
    version: 4
  - path: docs/designs/api/US-037-api-design.md
    version: 1
  - path: docs/designs/database/US-037-db-design.md
    version: 1
  - path: docs/designs/database/US-037-entity-model.md
    version: 1
  - path: docs/tests/US-037-test-strategy.md
    version: 1
  - path: docs/tests/US-037-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-037-implementation-report.md
    version: 2
  - path: trebovaniya.md
    version: 81
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 5
informational_findings: 7
security_sensitive: true
runtime_checks: FULL
---

# US-037 Security Review — Retention purge

> **Version 2 (attempt 2).** Sections 1–22 below are the first review (v1), kept as
> written. Section 23 holds the independent re-review: the v1 findings it added,
> the fixes made at IMPLEMENTATION attempt 2, and the current verdict. Where
> section 23 and the sections above disagree, section 23 wins.

## 1. Executive Summary

**Verdict: PASS** — no Critical or Major finding.

The Story adds a background job and no surface. The job physically deletes
personal data of students (submissions with grades, participant names and
emails), staff accounts (including the Dean's password hash) and old audit
rows. It runs in read-only mode as a BR-026 write. The security questions are
therefore:

- does it delete **exactly** what §5 allows, and nothing more;
- can its unguarded write path be reused by anything else;
- does it leak what it deletes, into the audit row or the log;
- is the audit trail still unchangeable by anyone else.

Each was verified against code and running tests (2780/2780 green; the key
properties re-checked in §14).

The two Minor findings are defence-in-depth gaps in **tests**, not in behaviour.

This is an engineering review of code, configuration and tests, not a
penetration test.

## 2. Reviewed Artifacts

As in the front matter. All current, none `SUPERSEDED`. `HUMAN_SPEC_APPROVAL`
was recorded at 2026-10-03T16:55:47Z.

## 3. Security-Relevant Scope

- **Exposed functionality:** none. No endpoint, page, policy or route (API
  design NOT_APPLICABLE, spec §7).
- **Background:**
  - `RetentionPurgeBackgroundService` (Web);
  - `RunRetentionPurgeUseCase` (Application);
  - `RetentionPurgeStore` (Infrastructure);
  - the shared gate in `SyncRunCoordinator`.
- **Assets:**
  - student personal data and grades (deleted);
  - `app_user` rows with Dean password hashes and security stamps (deleted);
  - `audit_event` rows (deleted by age; one written per run).
- **Trust boundaries:**
  - application ↔ PostgreSQL only;
  - no Google port, no Control Plane call, no browser.
- **Changed security components:**
  - the BR-026 registry (`PermittedServiceWrites`);
  - the first set-based deletes of the installation, which bypass the commit
    backstop (db-design §6);
  - `audit_event` schema and constraints.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker running; Testcontainers PostgreSQL.
- `dotnet build ClassroomAgent.sln`: 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln`: 2780 passed, 0 failed, 0 skipped (evidence
  in the implementation report §5; the US-037 classes were re-run here, 149
  passed).
- `dotnet list ClassroomAgent.sln package --vulnerable`: no vulnerable packages
  in any project.
- `git diff -- '*.csproj'`: no change.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | no role, policy or permission cell added |
| SC-2 Authentication | PASS | Deleting an `app_user` removes its hash, stamp and lockout counters in one row; no other table holds credentials (no Identity tables in the installation). A live cookie of a deleted account fails `AccountSessionService` (`state is { IsDisabled: false }` is false for a missing account) and is signed out by `InstallationSession.ValidatePrincipalAsync`. See M-1 |
| SC-3 AllowedAdmin | PASS | A purged Admin comes back only through the unchanged `CompleteGoogleSignInUseCase`, which asks the Control Plane every time (`AdminReturnsAfterPurgeTests`) |
| SC-4 Authorization | NOT_APPLICABLE | no endpoint |
| SC-5 Read-only mode | PASS | The purge is on the BR-026 list as `RetentionPurge` (BR-075) and registered in `PermittedServiceWrites` — the only addition (`PermittedServiceWriteTests`, `WorkspaceConnectionReadOnlyTests`). Its audit commit goes through the backstop under `ServiceWriteScope`. No Google port is in the use case's constructor, and none is called in read-only (`RetentionPurgeReadOnlyTests`, 3 causes). `ReadOnlyEnforcementTests` is green with the new write path registered |
| SC-6 No DB UI | NOT_APPLICABLE | none added |
| SC-7 Key | NOT_APPLICABLE | key not touched |
| SC-8 Google | PASS | no Google call; nothing deleted in Google (read-only scopes unchanged) |
| SC-9 Channel | NOT_APPLICABLE | not touched |
| SC-10 Hygiene | PASS | Log lines carry the cutoff, counts, step names, internal course ids and exception **type** names only (`RetentionPurgeBackgroundService` messages 5201–5205). No exception message, no Google id, name or email (`RetentionPurgeLoggingTests`, 3 tests) |
| SC-11 Audit | PASS | Exactly one `system` row per run with integer counts only (`ck_audit_event_purge_*`, `AuditEvent.RetentionPurgeRun`, `RetentionPurgeTests` AC-008 group). Audit rows deleted only by age, nothing newer (`ExactlyTheAuditRowsOlderThanTheCutoff_AreDeleted`). Repository add-only, entity immutable, no other delete or update (`AuditDeletionConfinementTests`, `RetentionPurgeAuditEventTests.TheEntity_StaysImmutable`). Control Plane audit untouched (§5 v45) |
| SC-12 Owner | PASS | `Contracts` unchanged; no count leaves the installation |
| SC-13 Outbound | PASS | no outbound call at all |

## 6. Authentication and Authorization

No endpoint, and therefore no allowed-role or forbidden-role tests are due.

The one authentication consequence is account deletion:

- the session of a deleted account dies at its next request (SC-2 row above);
- a deleted Admin is re-provisioned only after a fresh `AllowedAdmin` check;
- the old id survives only in audit rows, with no foreign key.

## 7. Credentials, Key and Google Access

- The Dean's password hash is deleted with the row; nothing copies it first.
- The key and the Google ports are not involved.

## 8. Sensitive Data Exposure

- **Audit row:** integers only, enforced in the database (`ck_audit_event_purge_counts_absent`, `_non_negative`, `_purge_actor`) and in the factory. `TheAuditEvent_CarriesNoPersonalData` greps the whole audit table as JSON for the purged ids, names, emails and grade.
- **Logs:** verified above.
- **`RetentionPurgeOutcome`:** carries internal course ids and type names. It never leaves the process (no endpoint, no DTO).
- **No exports or views** are touched.

## 9. Input Validation

The only external input is configuration `Retention:Years`. It is validated at
start-up (`InstallationSettingsReader`: whole number, 1 or more). `0` or a
negative value, which would purge everything, is refused before the host
starts. No request input exists.

## 10. API Security

No endpoint added. Readiness is unchanged: `IsRunning` still means "a
synchronization run" (`ThePurge_IsNotReportedAsASynchronizationRun`); the
readiness tests are green.

## 11. Persistence and Configuration

- **Migration `AddRetentionPurge`:**
  - five nullable `integer` columns;
  - four check constraints;
  - `ix_audit_event_occurred_at`;
  - the partial `ix_course_membership_off_roster_last_seen`;
  - no FK changed — all stay `Restrict` (`EveryForeignKeyThePurgeCrosses_StaysRestrict`).
- **No `EnsureCreated`.**
- **Deletes are child-first** in per-unit transactions. Rollback was proved
  with a real PostgreSQL trigger failure, not a mock (`AFailingCourse_*`,
  `AFailingLeaverDelete_*`, `AFailingStep_*`).
- **The purge deletes precisely §5's sets:**
  - leavers only in kept courses and only `on_roster = false`;
  - roster members never;
  - submissions elsewhere never;
  - the boundary is strict in all four cases.

  This is the main *integrity* risk of the Story, and each case has a test.
- **Down migration** fails while purge rows exist (I-2).
- **No configuration change.**

## 12. Logging, Audit and Telemetry

Covered in §5 (SC-10, SC-11). The hook telemetry file is not touched.

## 13. Dependencies

No package or project reference added. `Application` still does not reference
`Infrastructure` (`ProjectReferenceTests` green). No vulnerable packages
reported.

## 14. Security Test Coverage

| Property | Test |
|---|---|
| deletes only expired data, strict boundary | `RetentionPurgeTests` (24) |
| read-only: runs, no Google call | `RetentionPurgeReadOnlyTests` |
| BR-026 registry exact | `PermittedServiceWriteTests`, `RetentionPurgeReadOnlyTests.ThePurge_IsDeclared*` |
| audit row shape enforced by DB | `RetentionPurgeSchemaTests` (27) |
| audit trail not deletable elsewhere | `AuditDeletionConfinementTests` |
| no personal data in audit / log | `TheAuditEvent_CarriesNoPersonalData`, `RetentionPurgeLoggingTests` |
| deleted Admin needs a fresh `AllowedAdmin` check | `AdminReturnsAfterPurgeTests` |
| synchronization never deletes | `SynchronizationNeverDeletesTests` |

Gaps: M-1, M-2.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| A misconfigured N (0 or negative) wipes the school | start-up refuses | `InstallationSettingsReader` | PASS |
| New code reuses the purge's unguarded set-based delete to write in read-only mode | set-based deletes confined to the store | `SetBasedDeletes_ExistOnlyInThePurgeStore` | PASS (see M-2) |
| Someone deletes or rewrites audit rows to hide an export | only the age-based purge deletes; no update path | `AuditDeletionConfinementTests`, `ck_audit_event_immutable` | PASS |
| A revoked Admin's account lingers or comes back without a check | deleted after N; re-creation re-asks the Control Plane | `UnusedAccounts_*`, `AdminReturnsAfterPurgeTests` | PASS |
| Purge and synchronization race, so a course is half-updated or re-imported | shared gate; synchronization refuses unimported expired courses | `RetentionPurgeCoordinationTests`, US-015 `CourseAgeRuleTests` | PASS |
| Partial delete leaves orphaned grades | per-unit transaction, `Restrict` FKs | failure-injection tests | PASS |

## 16. Repository Hygiene

`git status` shows source, tests, docs and workflow files only. There is no
`.db`, `.xlsx`, `.env` or credential file. `google_credentials.json` is
git-ignored (confirmed with `git check-ignore`; the file was not opened).

## 17. Deviations

Implementation report §7 lists them:

1. The synchronization loop was changed to wait for the purge's end. This is
   security-neutral: it restores FR-014.
2. Six existing tests were updated to the approved schema and registry. Each
   was checked:
   - none was weakened;
   - the personal-data check in `AdminSignInAuditTests` still covers every row;
   - the registry tests now name exactly eight entries.

No undocumented security behaviour was found.

## 18. Findings

### M-1 — Minor — TEST_COVERAGE (SC-2)

- **Affected:** `tests/` (no file).
- **Evidence:** no test asserts that a session cookie issued before the purge
  deleted its account is refused afterwards. The behaviour holds by code
  inspection: `AccountSessionService` returns false for a missing account, and
  `ValidatePrincipalAsync` rejects the principal and signs it out.
- **Expected:** a regression guard for the property spec §7 states.
- **Risk:** low. Within N years a session cookie has long expired, and the
  generic stamp check already applies.
- **Correction:** optional. A host test that signs a Dean in, deletes the row
  through the purge and asserts the next request is redirected to sign-in.
- **Loop-back:** none required.

### M-2 — Minor — TEST_COVERAGE (SC-5, SC-11)

- **Affected:** `tests/ClassroomAgent.Tests/Architecture/AuditDeletionConfinementTests.cs`.
- **Evidence:** the confinement test is textual. It catches `ExecuteDelete`,
  `ExecuteUpdate`, `AuditEvents.Remove/Update` and raw
  `DELETE FROM`/`UPDATE audit_event`. It would not catch other raw-SQL deletes
  (`ExecuteSqlRaw` against another table) outside the store, which would also
  bypass the backstop.
- **Expected:** set-based or raw writes confined to the purge store.
- **Risk:** low. No such code exists today.
- **Correction:** optional. Add `ExecuteSqlRaw`, `ExecuteSqlInterpolated` and
  `ExecuteSql` to the forbidden list outside migrations.
- **Loop-back:** none required.

### I-1 — Informational — PERSISTENCE

A system clock set far in the future would make the purge delete data early.
This is inherent in "N years from now" (§5). It is noted for deployment
(DC-13 backups cover 30 days).

### I-2 — Informational — PERSISTENCE

The `Down` migration refuses to drop the purge action while purge rows exist.
This is intended, so that a rollback never deletes audit rows (implementation
report §7.4).

### I-3 — Informational — AUDIT

The run's audit outcome is "succeeded" even when a unit failed (spec I-7,
approved). The failure is visible only in the log, which rotates after 30
days. This is acceptable as approved; the next run retries anyway.

## 19. Positive Controls

- **Database-level enforcement of the purge row's shape.** The counts belong
  only to the purge row and cannot be negative; the purge row is `system`, has
  no target and no request id.
- **Strict, single cutoff per run**, shared with synchronization.
- **Per-unit transactions with child-first deletes** and `Restrict` FKs, proved
  by real database failures.
- **Narrow read-only exemption.** It is limited to one registered use case, and
  its audit commit still passes the backstop.
- **Log lines with type names only.**
- **Start-up refusal of N < 1.**

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- No penetration test.
- The confinement and registry checks are structural or textual (M-2).
- Clock-tampering scenarios were not exercised (I-1).

## 22. Verdict Rationale

No Critical or Major finding. Every SC item the Story touches is PASS with
evidence. The build and the full suite are green. The security-sensitive
Acceptance Criteria are verified:

- AC-005, AC-006, AC-007, AC-008 and AC-009;
- AC-010 for the "left whole" integrity.

M-1 and M-2 are non-blocking test hardenings. **PASS** → `HUMAN_PR_APPROVAL`.

## 23. Independent review and attempt 2

v1 was written by the agent that implemented the Story. At the Owner's request, a
separate reviewer (an independent agent) re-checked it from code and tests. That
reviewer was not given v1's conclusions as facts. It ran the build and the full
suite twice: 2780/2780 on attempt 1 and 2782/2782 on attempt 2.

### 23.1 Findings v1 missed

| Id | Severity | Category | Finding | Disposition |
|---|---|---|---|---|
| N-1 | Minor (requirements gap) | DATA_EXPOSURE | A leaver deleted by the purge is re-imported by the next synchronization if Google still returns their old submissions (`RunSynchronizationUseCase.ResolveSubmitterAsync`). Their N years then restart. §5 forbids re-import for courses only | **Not code.** Moved to `trebovaniya.md` §7 item 27 (v81, `2963455`) for the Owner to decide. v1's abuse case "re-imported" covered courses only and missed this |
| N-2 | Minor | OTHER (availability) | A purge ending between a failed `TryStartScheduledRun` and the `IsPurging` check pushed the due synchronization run back a whole interval | **Fixed** in attempt 2. `SynchronizationBackgroundService` decides again on `IsPurging \|\| changed.IsCompleted`, using the signal captured before the attempt. Re-review: no missed wake-up and no busy loop, because each extra pass needs a real state change. Shutdown is unchanged |
| N-3 | Minor | CONFIGURATION | No upper bound on `Retention:Years`; the cutoff computed outside `try` could throw and stop the host | **Fixed.** The cutoff is computed inside the handler, and N is capped at 1 … 100 at start-up (`InstallationSettingsReader.MaxRetentionYears`). Recorded as OD-008, `trebovaniya.md` v81 §5 and DC-3. `RetentionConfigurationTests`: 101 refused, 100 accepted. Re-review: no remaining path from the purge to a faulted service |
| N-4 | Minor | TEST_COVERAGE | The confinement test does not catch `Remove`, `RemoveRange` or `Set<AuditEvent>()` | Open, non-blocking; extends M-2 |
| N-5 | Minor | TEST_COVERAGE | No test exercises the N-2 interleaving; the fix is verified by reading the code | Open, non-blocking |
| I-a | Info | — | The purge decides expiry from a snapshot, then deletes by id. This is safe while synchronization is gated out. Meet linking (US-031) must keep that writer gated and add Meet dates to the rule | For US-031 |
| I-b | Info | — | A submission whose participant has no membership would fail the orphan step every day; the only alert is the daily `Error` line | Accepted by spec FR-006 |
| I-c | Info | — | If an expired course fails to delete, its expired leavers are still deleted by the leaver step. This is within §5, but the wording departs from spec I-3 | Accepted |
| I-d | Info | — | `AuditRowsAsync` filters purge rows suite-wide. The personal-data check still scans every row | Accepted (OD-007) |

### 23.2 v1 claims re-checked

- **Confirmed:**
  - M-1, M-2;
  - the SC-5, SC-10 and SC-11 analysis;
  - the start-up refusal of N < 1;
  - per-unit rollback (the real `UnitOfWork`, no retrying strategy, triggers
    prove a mid-unit rollback);
  - the `PermittedServiceWrites` gaining exactly one entry, with the enum
    unchanged;
  - no FK to `app_user`;
  - a deleted account's cookie is refused;
  - none of the six modified tests was weakened.
- **Refuted or incomplete:** the abuse-case table (§15) missed N-1; the
  coordination claim missed N-2; §9 missed the upper bound (N-3).

### 23.3 Current verdict

**PASS.** 0 Critical, 0 Major.

Open and non-blocking:

- Minor: M-1, M-2, N-4, N-5 (test hardening) and N-1 (requirements item 27,
  owned by the Owner);
- Informational: I-1 … I-3, I-a … I-d.

Every SC item the Story touches is PASS. Build is clean and 2782/2782 tests
pass.
