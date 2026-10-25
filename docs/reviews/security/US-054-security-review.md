---
artifact_type: security_review
story: US-054
version: 1
status: APPROVED
created_at: 2026-10-25T09:00:00Z
updated_at: 2026-10-25T09:00:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-054-technical-account-superadmin-texts.md
    version: null
  - path: docs/specifications/US-054-spec.md
    version: 1
  - path: docs/decisions/US-054-open-decisions.md
    version: 1
  - path: docs/designs/api/US-054-api-design.md
    version: 1
  - path: docs/designs/database/US-054-db-design.md
    version: 1
  - path: docs/tests/US-054-test-strategy.md
    version: 1
  - path: docs/tests/US-054-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-054-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-054-implementation-verification.md
    version: 1
  - path: trebovaniya.md
    version: 97
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 4
security_sensitive: true
runtime_checks: FULL
---

# US-054 Security Review

## 1. Executive Summary

**PASS.** The Story changes user-visible texts only: two translation files, one
text-key class, one Razor loop that nests the five sign-in measures, and one XML
comment. No endpoint, policy, scope, port, entity, migration, log line, audit
event or package changes. The new texts state §9 v97 faithfully — including
every mandatory measure that closes interactive sign-in under the technical
account — and contain no secret, key reference, console URL or personal data.
Rendered values stay HTML-encoded. Independent build: 0 warnings, 0 errors;
full suite 6123/6123; no vulnerable packages. 0 Critical, 0 Major, 0 Minor,
4 Informational.

The Story is security-sensitive by content, not by code: the instruction is the
school's only in-program guidance on how to harden a super-admin account. That
guidance is the security control this review checks.

## 2. Reviewed Artifacts

As listed in `inputs`. API and DB designs are `NOT_APPLICABLE` records (texts
only; no `openapi.yaml`, no entity model, by design). `HUMAN_SPEC_APPROVAL` was
recorded 2026-10-24 (`history.jsonl`). No input is `SUPERSEDED`.

## 3. Security-Relevant Scope

| Change | Host | Security relevance |
|---|---|---|
| `SharedResource.uk/en.resx`: `NoPersonBehindIt` reworded; `SuperAdminRole`, `MeasuresLead`, five `Measure.*` added; `ReadOnlyRoles`, `NoWriteAccess` removed; `ImpersonationUser.Hint`, `AccessCheck.Outcome.TechnicalAccountCannotRead` reworded | Web (Data Plane) | Content of security guidance to the school's super-admin |
| `ConnectionInstructionTextKeys.cs`: keys + two ordered arrays | Web | None beyond the above |
| `ConnectionInstruction/Index.cshtml`: nested `foreach` over `TechnicalAccountMeasures` inside `<pre>` | Web | Output encoding |
| `SaveWorkspaceConnectionRequest.cs`: XML summary only | Application | None (no attribute changed) |

Assets: the technical account (a super-admin after v97) and its sign-in path;
the instruction page (Admin-only). Trust boundary touched: none new — the page
is served on the existing authenticated route.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 (Testcontainers available).
- `dotnet build ClassroomAgent.sln` — succeeded, 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 6123 total, 6123 passed, 0 failed,
  0 skipped (7 m 59 s).
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` —
  no vulnerable packages in any of the 7 projects.
- Delegated (cheap-worker, read only): the build, test and package commands and
  `git status` / `git diff --stat`, facts reported verbatim. The diff review,
  the comparison with §9 v97 and every judgement below were done by this Skill.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role, policy or seed touched. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in code touched. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Untouched. |
| SC-4 Authorization | PASS | No controller or page attribute changed (`git diff -- src/` touches no controller); instruction stays under `ViewConnectionInstruction`; full suite incl. the endpoint/anonymous enumeration and §2 matrix tests green. |
| SC-5 Read-only mode | PASS | No write use case added; the instruction remains viewable in read-only mode (view unchanged above the `<pre>`). |
| SC-6 No DB UI | NOT_APPLICABLE | — |
| SC-7 Key | PASS | No key, key reference or client secret in any new value (resx diff read in full); the `<pre>` still prints only domain, client ID and scopes (unchanged). |
| SC-8 Google | PASS | No scope, port or impersonation code changed. The texts state the §9 v97 fact that access is bounded by the delegation scopes; the program grants nothing. |
| SC-9 Channel | NOT_APPLICABLE | — |
| SC-10 Hygiene | PASS | No log statement added; no personal data in any text; values rendered through `IStringLocalizer<SharedResource>` (`_ViewImports.cshtml:7`) → `LocalizedString`, which Razor HTML-encodes. |
| SC-11 Audit | NOT_APPLICABLE | No audited action introduced or changed. |
| SC-12 Owner | NOT_APPLICABLE | — |
| SC-13 Outbound | PASS | No new destination; texts name no external service URL. |

## 6. Authentication and Authorization

No change. The instruction (Admin-only), the connection page and the access
check keep their policies (spec §7). Allowed/forbidden-role tests for these
endpoints exist from US-009/US-010/US-052 and are green in the full run.

## 7. Credentials, Key and Google Access

Compared each new text with `trebovaniya.md` §9 "Технический аккаунт" (lines
2434–2459):

| §9 v97 mandatory measure | Instruction text (uk / en) | Faithful |
|---|---|---|
| random password stored nowhere | `Measure.RandomPassword` | yes |
| enforced 2SV, no enrolment grace, no factor, no backup codes / recovery email / phone | `Measure.TwoStepVerification` | yes — every element present |
| separate OU with these rules | `Measure.SeparateOrgUnit` | yes |
| alert on any sign-in, mailed to a domain admin | `Measure.SignInAlert` | yes |
| role granted immediately before the first sync, not earlier | `Measure.RoleGrantedLast` — "immediately before an Administrator clicks «Перевірити доступ»" | yes, per OD-002 (a), Owner-resolved; the access check is the first read under the role |

`SuperAdminRole` states the §9 rationale (lesser roles see no courses; access
bounded by scopes, not role). No text weakens a measure, makes one optional, or
suggests signing in under the account. The removed `NoWriteAccess` statement
("super-admin not acceptable") contradicted v97; its Hard-Stop meaning
(program writes nothing) survives in `SuperAdminRole` ("the program still only
reads"). No scope or OAuth change.

## 8. Sensitive Data Exposure

Responses/views: static texts only; no DTO or view-model field added. Logs,
audit rows, exceptions, exports, telemetry: untouched. Test fixtures: the
technical-account address in `WorkspaceConnectionTestData` is synthetic
(`*.example.test`).

## 9. Input Validation

No input added or changed. `SaveWorkspaceConnectionRequest` attributes
(`Required`, `StringLength`, `ServiceAccountEmail`) are unchanged — only the
`<summary>` was rewritten.

## 10. API Security

No endpoint, method, request or response field changed. `#connection-instruction`
markup hook kept; copy affordance copies the same `<pre>` (FR-005), so the
copied text carries the measures with no second, divergent copy.

## 11. Persistence and Configuration

No entity, migration, configuration or `appsettings` change. No generated
database or `.xlsx` in the change set.

## 12. Logging, Audit and Telemetry

No logger call or audit event added. `docs/hooks/tool-usage.jsonl` is
git-ignored (`.gitignore:66`).

## 13. Dependencies

No `.csproj` changed; no package or project reference added. Vulnerability scan:
none reported (§4).

## 14. Security Test Coverage

| Security property | Test evidence |
|---|---|
| All §4.1 wording present, both languages, verbatim | `TechnicalAccountSuperAdminTextsTests` (34 cases), green |
| Old "not a super-admin / read-only roles" wording absent | same class, literal-absence checks (AC-003) |
| No Workspace admin-role name other than super-admin on the page | `ForbiddenRoleNames` guard (VR-002), green |
| No secret / key reference on the page | existing US-010 instruction tests (AC-004), green |
| Admin-only access, forbidden roles refused | existing endpoint tests + §2 matrix test, green |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| A school follows the instruction literally and grants super-admin before hardening | Text orders the role last, after all measures | `Measure.RoleGrantedLast`, OD-002 (a) | PASS |
| A school reads the instruction as "make it super-admin" and skips 2SV/alerts | Measures introduced as mandatory, nested under the lead | `MeasuresLead` "обов'язкові заходи", nested list | PASS (the program cannot enforce — INFO-1) |
| Dean or anonymous user opens the instruction to learn the domain hardening | Admin-only policy | unchanged policy; tests green | PASS |
| Injected markup via a translation value | Razor encodes `LocalizedString` | `_ViewImports.cshtml:7` | PASS |

## 16. Repository Hygiene

`google_credentials.json`, `classroom_cache.db`, `.claude/settings.local.json`
and the hook telemetry are git-ignored (`git check-ignore -v`). The working tree
holds only the Story's files (7 modified, 2 untracked artifacts). Live
credential files were not opened.

## 17. Deviations

None security-relevant. The implementation's split of statements and measures
into two arrays (accepted at IMPLEMENTATION_VERIFICATION) changes rendering
structure only.

## 18. Findings

No Critical, Major or Minor findings.

- **INFO-1 (OTHER).** The program does not and cannot verify the account's
  role, 2SV, OU or alert rule (spec §10); the measures are the school's
  responsibility. The hint's "sign-in under it is blocked" is a statement of the
  requirement, not a checked fact. Whether DWD keeps working under 2SV with no
  factor is `trebovaniya.md` §7 item 29, verified at the first live access check
  on the pilot server (OD-001 a). No correction required.
- **INFO-2 (OTHER).** `RoleGrantedLast` ties the timing to the "Check access"
  click, while §9 says "before the first synchronization". Equivalent in effect
  (the check is the first read) and Owner-resolved by OD-002 (a). No correction.
- **INFO-3 (TEST_COVERAGE).** = IMPL_VERIFICATION N-2: old-wording absence is
  checked against exact pre-v97 literals only (spec I-5, by design).
- **INFO-4 (REPOSITORY_HYGIENE).** Keep `.claude/settings.local.json` and other
  IDE-local files out of the Story commit (Definition of Done item 8); it is
  ignored today.

## 19. Positive Controls

Razor output encoding of localized values; unchanged Admin-only policy on the
instruction; deny-by-default enumeration and §2 matrix tests green; no key or key
reference in any page text; texts reproduce every §9 v97 hardening measure.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001…OD-004 are resolved.
§7 item 29 is open and explicitly non-blocking (OD-001 a).

## 21. Review Limitations

Code, configuration and test review only — no penetration test. The real effect
of the measures in a Google Workspace domain is outside the program and is
checked manually on the pilot server (§7 item 29).

## 22. Verdict Rationale

Green build and full suite independently reproduced; no Critical or Major
finding; every touched SC item PASS; no blocking Open Decision. → **PASS**,
next stage `HUMAN_PR_APPROVAL`.
