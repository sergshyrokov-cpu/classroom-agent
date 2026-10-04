---
artifact_type: security_review
story: US-027
version: 1
status: APPROVED
created_at: 2026-10-04T22:05:00Z
updated_at: 2026-10-04T22:05:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-027-report-templates.md
    version: null
  - path: docs/specifications/US-027-spec.md
    version: 2
  - path: docs/evidence/US-027-implementation-report.md
    version: 1
  - path: docs/designs/api/US-027-api-design.md
    version: 1
  - path: docs/designs/api/US-027-openapi.yaml
    version: 1
  - path: docs/designs/database/US-027-db-design.md
    version: 1
  - path: docs/designs/database/US-027-entity-model.md
    version: 1
  - path: docs/tests/US-027-test-strategy.md
    version: 1
  - path: docs/tests/US-027-ac-test-matrix.md
    version: 1
  - path: docs/decisions/US-027-open-decisions.md
    version: 3
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-027 Security Review — Report templates and the on-screen report

## 1. Executive Summary

**PASS.** No Critical, Major or Minor finding. The Story adds eight
server-rendered operations on the installation's public port, all behind two
role policies (Admin, Dean), with global antiforgery on the three writing
`POST`s, the read-only guard first in `Application` with a `Refused` audit row,
audit rows that carry the template id only, and logs with ids and counts only.
No Google port, secret or new outbound destination is reachable from any use
case of the Story. Three informational notes (§18), one of which —
`Cache-Control: no-store` on a page with personal data — is already scheduled
as US-040.

Limitation: code, configuration and test review plus the full test run; no
penetration test.

## 2. Reviewed Artifacts

As in `inputs`. None `SUPERSEDED`. `HUMAN_SPEC_APPROVAL` recorded (spec v2).
Implementation report v1 records build 0/0, tests 3404/3404, format exit 0 —
re-confirmed independently (§4).

## 3. Security-Relevant Scope

| Operation (public port) | Policy | Writes |
|---|---|---|
| `GET /reports/templates` | `UseReportTemplates` | — |
| `GET /reports/templates/new`, `…/{ref}/copy`, `…/{ref}/edit`, `…/{ref}/deletion` | `EditReportTemplates` | — |
| `POST /reports/templates`, `POST /reports/templates/{ref}`, `POST …/{ref}/deletion` | `EditReportTemplates` + antiforgery | template + audit |
| `GET /reports` | `UseReportTemplates` | — |
| `GET /` | unchanged | — (link added) |

Assets: students' and teachers' names, emails, grades and submission states shown
on the report page (personal data of minors); author emails of templates (staff);
school-own template text; audit rows. Boundaries: browser → installation (public
HTTPS); controller → use case; use case → `IJournalFieldSource` /
`IReportTemplateRepository` → PostgreSQL. No Control Plane, Google or private-port
change.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker running (Testcontainers PostgreSQL).
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 3404 total, 3404 passed, 0 skipped.
- `dotnet list ClassroomAgent.sln package --vulnerable` — no vulnerable package in
  any of the seven projects (nuget.org source).
- Static review of every new/changed file in `Web`, `Application` (use cases,
  validation), `Domain` (`AuditEvent`, `ReportTemplate`), `Infrastructure`
  (repositories, configurations, migration, `UnitOfWork`).

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | Both policies `RequireRole(Admin, Dean)` (`InstallationSecurityServices`); no new role; matches §2 rows "Использование…" / "Создание и редактирование шаблонов отчётов" ✔ ✔. No per-template ownership, no roster scoping (S-03). |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in, password or cookie change. Restricted Dean session refused on every new endpoint (`ReportTemplateAuthorizationTests.ARestrictedDean_IsSentToTheForcedChange_OnEveryEndpoint`). |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Login path unchanged. |
| SC-4 Authorization | PASS | Every action declares a policy (`ReportTemplatesController` per action, `ReportController` at class level); anonymous → `/sign-in` on all endpoints (`Anonymous_IsSentToSignIn_OnEveryEndpoint`); endpoint-enumeration test green; no `[AllowAnonymous]`, no antiforgery exemption added; `POST` without token → `400` (`APostWithoutTheAntiforgeryToken_Is400`); both Razor forms emit `@Html.AntiForgeryToken()`; no state change on `GET` (report and forms only read). |
| SC-5 Read-only mode | PASS | `ReportTemplateRefusalAudit.EnsureAllowedAsync` is the first statement of create, change and delete; refusal writes `Refused`/`ReadOnlyMode` under `ServiceWriteScope.Declare(AuditEvent)` and rethrows → `409`; viewing unguarded. Proven for all three BR-025 causes in Application and over PostgreSQL (`ReportTemplateReadOnlyTests`, `ReportTemplateSaveTests`, `ReportTemplateDeleteTests`). |
| SC-6 No DB UI | PASS | No diagnostic endpoint added. |
| SC-7 Key | NOT_APPLICABLE | No key, reference or secret touched; no configuration change. |
| SC-8 Google | PASS | No use case constructor takes a Google port, secret store or Control Plane client (`NoUseCaseOfThisStory_TakesAGooglePort`); `GoogleClassroomReader` only passes the already-read `scheduledTime`; no scope change. |
| SC-9 Channel | NOT_APPLICABLE | No channel or private-port change. |
| SC-10 Hygiene | PASS | `ReportTemplateLog`: ids, counts, action names, field-rule pairs and enum keys only; field names are constructed in `ReportTemplateFormValidator` (regex-bounded indexes, enum state names), never the posted value; logging tests assert the name/value are absent. Razor encodes all output (no `Html.Raw` in the new views); scale script uses `textContent`/`value` only. Tampered form → generic `Error.PageExpired` page. |
| SC-11 Audit | PASS | `AuditEvent.ReportTemplateWritten` / `ReportTemplateWriteRefused`: actor, role, action, target type `report_template`, target id; no name, mark, label or setting. Guarded by `ck_audit_event_report_template_shape`; written in the same transaction as the change; no update/delete path added. |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` and Control Plane unchanged. |
| SC-13 Outbound | PASS | No HTTP client, no new destination; script contacts nothing. |

## 6. Authentication and Authorization

Auth matrix of api-design §4 verified by `ReportTemplateAuthorizationTests`
(39 cases: anonymous, restricted Dean, Admin, Dean, antiforgery). The actor and
author come from `ClaimTypes.NameIdentifier` / `ClaimTypes.Role` of the session,
never from the form (VR-008); a posted `authorId` or similar field is ignored by
the reader (unknown names skipped). Template identifiers in the path are parsed
in `Application` (`ReportTemplateReference.Parse`: exact key or ≤19 ASCII digits,
positive) — no cross-installation data exists to reach (AD-1).

## 7. Credentials, Key and Google Access

No password, key or Google call involved. The report reads the mirror only
through `IJournalFieldSource` (four bounded queries, `AsNoTracking`).

## 8. Sensitive Data Exposure

- Views receive DTOs only (`ReportPageModel`, `ReportTemplate*PageModel`); no
  entity reaches `Web` (AD-8).
- Rows and columns carry no internal participant or item id (api-design §2.9).
- The query string carries template reference, course id and dates only; the
  switcher return path is rebuilt from validated values with
  `Uri.EscapeDataString` (S-05).
- The template list shows the author's email to Admin and Dean (I-10, already
  visible to Admins; spec-approved).
- Audit rows and logs: §5 SC-10, SC-11.
- Report page caching: Informational INFO-1.

## 9. Input Validation

Form: every field bound as a string and parsed in `Application`
(`ReportTemplateFormReader`, `ReportTemplateFormValidator`): exact enum
vocabularies, repeated or unknown-shaped keys → malformed; scale index regex
`[0-9]{1,9}` (no overflow); 1–101 rows; names 1–100, marks 1–30, labels 1–10
after trim; control characters refused; hours 1–10. Domain invariants re-check
everything (`ReportTemplate.Apply`), and the database checks bound view, kind,
hours, percents and the mark/text pairing. Report query: VR-007, malformed values
not echoed (`AMalformedQuery_Is400_AndIsNotEchoed`). Kestrel's default form limits
bound the number of posted fields.

## 10. API Security

Exactly the eight operations of the openapi; methods as designed; no JSON
endpoint (I-12); no `/api/v1` path. Error behaviour: reference refusals re-render
the list (`400`/`404`), field errors the form (`400`), tampered forms the generic
error page (`400`), read-only `409` via the single exception handler, unexpected
`500` page without detail.

## 11. Persistence and Configuration

One generated migration `20261004202609_ReportTemplates` (PC-2): three template
tables with explicit lengths, checks, unique indexes and cascades of db-design §2;
`course_work.scheduled_time`; widened audit checks. `author_id` is a bare id (no
personal data copied; S-14). Unique name translated to a field error, not `500`.
No `EnsureCreated`, no configuration file changed, no generated database file or
export in the change set.

## 12. Logging, Audit and Telemetry

Covered in §5. `ReportTemplateLoggingTests` (3) prove the validation failure,
create and report lines carry no value, name or Google data. Hook telemetry
untouched.

## 13. Dependencies

No package or project reference added (no `.csproj` change). Vulnerability scan
clean (§4).

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| S-01, S-02, S-07 | `ReportTemplateAuthorizationTests` |
| S-04, S-13 | `ReportTemplateInvariantTests.NoUseCaseOfThisStory_TakesAGooglePort`, `ReportTemplateReadOnlyTests` |
| S-05 | `ReportRequestValidationTests`, `ReportPageTests.AMalformedQuery_Is400_AndIsNotEchoed` |
| S-06 | `ReportTemplateSaveTests`, `ReportTemplateDeleteTests`, `ReportTemplateReadOnlyTests` (3 causes) |
| S-08, S-10 | `ReportTemplateFormValidationTests`, `ReportTemplateLoggingTests` |
| S-09 | `ReportTemplateSaveTests`, `ReportTemplateDeleteTests`, `ReportTemplateSchemaTests` (shape check) |
| S-11 | `ReportTemplatePagesTests.ATemplateName_IsHtmlEncoded`, `ReportPageTests.TheReport_RendersInEnglish_…` |
| S-12, S-14 | type design (DTO-only views); `ReportTemplateRepositoryTests` (AccountDeleted after purge) |

## 15. Abuse Case Review

| Scenario | Expected | Evidence | Status |
|---|---|---|---|
| Direct `POST` save/delete while read-only | `409`, nothing stored, refused audit row | `InReadOnlyMode_ASaveIs409_AndNothingIsStored`, `ReportTemplateReadOnlyTests` | PASS |
| Change/delete the built-in via crafted path | `400` `BuiltInNotChangeable`, nothing changed | `TheBuiltIn_CannotBeEditedChangedOrDeleted_OverHttp` | PASS |
| Forged author id in the form | ignored; author = session | `ReportTemplateSaveTests` (author = actor) | PASS |
| Cross-site `POST` without token | `400` error page | `APostWithoutTheAntiforgeryToken_Is400` | PASS |
| Script/markup in a template name or mark | rendered encoded | `ATemplateName_IsHtmlEncoded` | PASS |
| Huge / sparse scale indexes, repeated fields | malformed, no overflow | `ReportTemplateFormValidationTests`; regex `{1,9}` | PASS |
| Personal data in logs | absent | `ReportTemplateLoggingTests` | PASS |

## 16. Repository Hygiene

No secret-like, `.db`, `.xlsx`, `.env` or IDE-local file in the working tree's
changes. Live credential files not opened.

## 17. Deviations

The implementation report's D-1 … D-8 were reviewed; none weakens a control. D-4
(refused audit target type) was a defect caught before this stage and corrected
to satisfy `ck_audit_event_report_template_shape`; verified in code.

## 18. Findings

- **INFO-1 — DATA_EXPOSURE (SC-10).** `GET /reports` renders students' personal
  data without `Cache-Control: no-store`. `trebovaniya.md` v85 requires it
  host-wide; it is delivered by US-040 (Story already authored, next in line), not
  per page (spec F-2 v2 / workflow finding F-2). No correction in US-027; verify
  in US-040 that `/reports` and `/reports/templates*` are covered.
- **INFO-2 — INPUT_VALIDATION.** The form-key regexes end in `$`, which in .NET
  also matches before a final newline (`scale[0].from\n`); such a key maps to the
  same member. No security effect (members are a closed set); noted only.
- **INFO-3 — OTHER.** `NameNotUnique` discloses that a template name exists — school
  text visible on the list to the same roles anyway; no personal data.

## 19. Positive Controls

Role policies on every action; deny-by-default fallback unchanged; global
antiforgery with tokens in both forms; guard-first read-only refusal with audit;
id-only audit rows guarded by a check constraint; DTO-only views; Razor encoding,
no inline script; parameter parsing in `Application`; bounded, untracked reads;
no Google port in any constructor; unique-name race translated, not leaked.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

No penetration test or browser-based manual testing; the HTTP behaviour is
evidenced by the integration tests over the real host. Vulnerability data only
from the nuget.org advisory feed.

## 22. Verdict Rationale

Green build and suite; every touched SC item PASS; security tests for every
endpoint and all three read-only causes pass; no Critical, Major or Minor
finding; no open security decision. → `PASS`, next `HUMAN_PR_APPROVAL`.
