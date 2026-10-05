---
artifact_type: security_review
story: US-042
version: 1
status: APPROVED
created_at: 2026-10-05T13:27:54Z
updated_at: 2026-10-05T13:27:54Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-042-names-in-report.md
    version: null
  - path: docs/specifications/US-042-spec.md
    version: 1
  - path: docs/evidence/US-042-implementation-report.md
    version: 1
  - path: docs/designs/api/US-042-api-design.md
    version: 1
  - path: docs/designs/api/US-042-openapi.yaml
    version: 1
  - path: docs/designs/database/US-042-db-design.md
    version: 1
  - path: docs/designs/database/US-042-entity-model.md
    version: 1
  - path: docs/tests/US-042-test-strategy.md
    version: 1
  - path: docs/tests/US-042-ac-test-matrix.md
    version: 1
  - path: docs/decisions/US-042-open-decisions.md
    version: 2
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-042 Security Review — Names of students and teachers in a template report

## 1. Executive Summary

**Result: PASS.** No Critical, Major or Minor finding.

The Story adds two personal-data columns (`classroom_participant.surname`,
`given_name`), a template setting `name_source`, and a query parameter `names`
on the existing report page. It adds no endpoint, policy, Google scope, Google
call, outbound destination, audit action or package. The new input is parsed by
one closed-set parser (`NameSourceCode.TryParseSingle`) on both paths; a
rejected value is neither logged, echoed into the page, nor put into a link.
Names are rendered HTML-encoded and never reach a log, an audit row or an
address. Read-only mode is untouched: the switch writes nothing, the template
save keeps its guard-first refusal in `Application`.

Independent re-run: build 0 warnings / 0 errors; 3597/3597 tests green, 0
skipped; no vulnerable packages. Three informational notes (§18).

## 2. Reviewed Artifacts

As in the front matter `inputs`. Spec v1 `APPROVED` (HUMAN_SPEC_APPROVAL
recorded in `history.jsonl`, 2026-10-05T11:22:06Z). No input is `SUPERSEDED`.
OD-001 (compile-only skeleton) resolved (a); no security-sensitive Open
Decision.

## 3. Security-Relevant Scope

- **Host:** installation (`ClassroomAgent.Web`) only. Control Plane untouched.
- **Pages:** `GET /reports` (new parameter `names`, new switch links);
  `POST /report-templates`, `POST /report-templates/{id}` (new form field
  `names`). Policies `UseReportTemplates` / `EditReportTemplates` unchanged.
- **Assets:** surname, given name, email local part of students and teachers
  (personal data of minors); template settings; audit rows.
- **Trust boundaries:** browser → installation (query string, form body);
  Google roster profile → sync (`name.familyName`, `name.givenName`);
  Application → PostgreSQL.
- **Components:** `ClassroomParticipant`, `ReportTemplate`,
  `ReportTemplateSettings`, `GoogleClassroomReader.Entry`,
  `RunSynchronizationUseCase`, `GetReportQuery`, `ReportPersonNameRule`,
  `NameSourceCode`, template form reader/validator, `ReportController`,
  `ReportTemplateLog`, two Razor views, one migration.

## 4. Environment and Tools

- .NET SDK 10.0.401, Windows 10; Docker available (Testcontainers ran).
- `dotnet build ClassroomAgent.sln` — exit 0, 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln --no-build` — Passed: total 3597, failed 0,
  succeeded 3597, skipped 0 (3 m 22 s).
- `dotnet list ClassroomAgent.sln package --vulnerable` — every project: "no
  vulnerable packages given the current sources".
- Code, configuration and diff review against `HEAD` (`4b1eef4`).
- Not performed: penetration testing; manual browser check.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role, policy or seed change; Admin and Dean only (spec S-01). |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in, password or cookie change. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Admin sign-in untouched. |
| SC-4 Authorization | PASS | No new route; `ReportController` unchanged attributes; antiforgery on the template POST unchanged (form still posted through the US-027 view with its token); switch is a plain GET link, no state change. `ReportNameSourceAuthorizationTests`: anonymous → sign-in, restricted Dean → forced change, Admin and Dean allowed. |
| SC-5 Read-only mode | PASS | `SaveReportTemplateUseCase` calls `ReportTemplateRefusalAudit.EnsureAllowedAsync` before reading the form (unchanged); report path writes nothing and opens no Google port. Test `ReportNameSourceTests.InReadOnlyMode_TheSwitchWorks_WhileASaveIsRefused`. |
| SC-6 No DB UI | NOT_APPLICABLE | Nothing added. |
| SC-7 Key | NOT_APPLICABLE | No key or configuration change. |
| SC-8 Google | PASS | Only the mapping in `GoogleClassroomReader.Entry` changed — two fields of the profile already returned by the roster read; no new call, scope or impersonation change. |
| SC-9 Channel | NOT_APPLICABLE | Untouched. |
| SC-10 Hygiene | PASS | `ReportTemplateLog.Built` adds only the code `profile`/`email` and the origin enum; `QueryRefused` and `Rejected` log message keys / `field:key`. Rejected values: `ParsedReportTemplateForm.ToValues` returns `""` for `Names`; `GetReportQuery.ReturnPath` and the switch links use only the parsed enum. Tests `AMalformedSource_IsAWarningNamingTheRule_AndTheValueIsNeverLogged`, `ARejectedFormValue_IsNeverLogged`, `NoLogLine_CarriesAProfileNameOrAnEmailPart`. |
| SC-11 Audit | PASS | No new audit action; template change keeps the US-027 row with template id only (spec FR-007); `ReportTemplateNameSourceTests`. |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` and `ControlPlane` untouched. |
| SC-13 Outbound | PASS | No new destination. |

## 6. Authentication and Authorization

No new endpoint; the §2 rows "Использование шаблонов отчётов" and template
editing keep their policies. Anonymous list unchanged. A request identifier
cannot reach another installation's data — single database per installation
(AD-1); `courseId`/`template` handling is US-027's and unchanged.

## 7. Credentials, Key and Google Access

No credential handling. Google: the profile's `familyName`/`givenName` are read
from the same response as `fullName`; no field mask or scope change. Values are
trimmed, blank → null, cut to 750 characters (`ClassroomParticipant.Name`),
matching the column length — oversized Google data cannot fail the sync.

## 8. Sensitive Data Exposure

- **Views:** names written with `@…` (Razor HTML-encoding); no `Html.Raw` in
  `Report/Index.cshtml` or `ReportTemplates/Form.cshtml`. Switch `href` is an
  Application-built path from validated values.
- **DTOs:** `Report`, `ReportPageModel`, `ReportTemplateFormValues`,
  `JournalCourseMemberRecord` — records, no entity (AD-8).
- **Address:** only `template`, `courseId`, `from`, `to`, `names` (a source
  code, not a person) — spec S-03.
- **Logs / audit:** see SC-10, SC-11.
- **Exports:** none in this Story.
- **Tests:** synthetic names and emails only (TC-4).

## 9. Input Validation

- Query `names`: absent → template setting; exactly one occurrence of
  `profile`|`email` (case-sensitive, untrimmed) → accepted; empty, repeated or
  unknown → `NameSourceMalformed`, `400` with the report page, nothing built.
- Form `names`: the same parser; failure → field error `NameSourceInvalid`,
  `400`, nothing saved. `ToSettings` is reached only after `Validate` returned
  no errors (both create and change paths).
- Domain: `ReportTemplate.Apply` rejects an undefined enum value; database
  check constraint `ck_report_template_name_source` (defense in depth).
- Google values: trimmed and length-bounded (VR-003).

## 10. API Security

Matches the API design: no new route, one new query parameter, one new form
field; error behaviour per api-design §2.3/§2.5. No undocumented field in any
DTO.

## 11. Persistence and Configuration

Migration `20261005125917_ParticipantNamePartsAndTemplateNameSource`: adds
`surname`, `given_name` (`varchar(750)`, nullable), `name_source`
(`varchar(8)`, not null, default `profile` for existing rows then dropped —
D-5), check constraint. No `EnsureCreated`; model snapshot updated. No
configuration, connection string or `appsettings` change.

## 12. Logging, Audit and Telemetry

Covered in SC-10 / SC-11. `docs/hooks/tool-usage.jsonl` is git-ignored
(`.gitignore:63`).

## 13. Dependencies

No `.csproj` changed; no package or project reference added. Vulnerability
scan clean (§4).

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| S-01/S-02 authorization | `ReportNameSourceAuthorizationTests` (anonymous, restricted Dean, Admin, Dean) |
| S-04 read-only | `ReportNameSourceTests.InReadOnlyMode_TheSwitchWorks_WhileASaveIsRefused`, `ReportTemplateNameSourceTests` |
| S-06 validation, no echo | `ReportNameSourceTests`, `ReportNameSourcePageTests`, `ReportTemplateNameSourceTests` |
| S-07 no names in logs | `ReportNameSourceLoggingTests` (5 cases) |
| S-11 audit | `ReportTemplateNameSourceTests` |
| Persistence constraints | `NamePartsPersistenceTests`, `ReportTemplateSchemaTests`, `ClassroomParticipantSchemaTests` |

The logging tests read the real Serilog file output of the test host, not a
mock — they prove the property, not only a call.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| `?names=<script>` or long junk in the address | `400`, value not echoed or logged | `GetReportQuery` VR-002, `ReturnPath` from enum; logging test | PASS |
| Repeated `names` to confuse parsing | Refused | `TryParseSingle` requires `Count == 1` | PASS |
| Tampered form `names` | Field error, nothing saved, not logged | Reader/validator; `ARejectedFormValue_IsNeverLogged` | PASS |
| Save a setting in read-only mode | `409`/refusal in Application, audited | guard first in `SaveReportTemplateUseCase` | PASS |
| Change the built-in template's setting | Refused (US-027 built-in rule) | `ReportTemplateNameSourceTests` (AC-005) | PASS |
| A Google profile name with HTML/script | Rendered encoded | Razor `@`; no `Html.Raw` | PASS |
| Inspect logs for names | None | `NoLogLine_CarriesAProfileNameOrAnEmailPart` | PASS |

## 16. Repository Hygiene

`google_credentials.json`, `dac-classroom-agent-*.json`, `classroom_cache.db`
confirmed git-ignored (not opened). No untracked or modified `.db`, `.xlsx`,
`.env`, key or secret-like file in the working tree. Untracked files are the
implementation report, two new source files and the migration pair — all in
the change set.

## 17. Deviations

None security-relevant. The implementation report's design-signature
differences (optional parameters on `Import`, removal of the two-argument
`UpdateFrom`, public `NameSourceCode`) do not widen access; removing
`UpdateFrom(email, fullName)` makes it impossible to update a participant while
leaving stale name parts.

## 18. Findings

No Critical, Major or Minor findings.

- **INFO-1 (DATA_EXPOSURE).** The email local part is now shown in the report
  as a name under "from the email". It is shown only where the email could
  already be shown (Admin and Dean, report page) — per Story Notes and spec
  S-07. US-028 (Excel) will carry these names into a file; its review should
  re-check exports against SC-10.
- **INFO-2 (INPUT_VALIDATION).** `ReportTemplateFormValidator.ToSettings`
  throws `InvalidOperationException` when `NameSource` is null. Unreachable on
  the current paths (called only after `Validate` returned no errors); it
  would surface as a `500` through the single `IExceptionHandler` with no
  internals. No correction required.
- **INFO-3 (TEST_COVERAGE).** NFR-070 layout of the switch at phone width is
  not automated (implementation report §1) — a usability check for the human
  at HUMAN_PR_APPROVAL, not a security property.

## 19. Positive Controls

One closed-set parser for both entry points; rejected values never echoed or
logged (proven against real log files); links built from validated values;
Razor encoding; domain enum check plus database check constraint; length-bounded
Google values; guard-first read-only refusal unchanged; no new endpoint, scope,
package or outbound flow.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

No penetration test or manual browser session. Static review limited to the
Story's change set and the code paths it touches.

## 22. Verdict Rationale

The implementation report's green build and tests were independently
reproduced; every touched SC item is `PASS`; required security tests exist and
pass; no blocking Open Decision. Verdict `PASS`; next stage
`HUMAN_PR_APPROVAL`.
