---
artifact_type: test_strategy
story: US-027
version: 1
status: DRAFT
created_at: 2026-10-04T20:00:00Z
updated_at: 2026-10-04T20:00:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-027-report-templates.md
    version: null
  - path: docs/specifications/US-027-spec.md
    version: 2
  - path: docs/designs/api/US-027-api-design.md
    version: 1
  - path: docs/designs/api/US-027-openapi.yaml
    version: 1
  - path: docs/designs/database/US-027-db-design.md
    version: 1
  - path: docs/designs/database/US-027-entity-model.md
    version: 1
  - path: docs/decisions/US-027-open-decisions.md
    version: 3
  - path: trebovaniya.md
    version: 85
supersedes: null
---

# US-027 Test Strategy — Report templates and the on-screen report

## 1. Scope

Everything the Story adds: the template aggregate and its rules, the built-in
"Academic journal" and the 12-point preset, the six use cases (list, forms,
save, delete, report) and their evaluation order, the two read ports over
PostgreSQL, the schema and its constraints, `course_work.scheduled_time` and its
import, the audit rows, read-only mode, the eight HTTP operations and the home
page link, authorization, antiforgery, translations and logging.

Not in scope: Excel/Word export (US-028 … US-030); the US-025 journal page
(regression covered by its own tests); `Cache-Control` (US-040, v85).

## 2. Levels

| Level | Unit under test | Substitutes | Evidence for |
|---|---|---|---|
| Unit (TC-1) | the use cases and the `ReportTemplate` aggregate | `ReportTemplateWorld`: templates, journal fields, audit, unit of work, guard, clock — all in memory | rules of spec §4–§6, evaluation order, audit rows, refusals |
| Integration, PostgreSQL (TC-2) | `JournalFieldSource`, `ReportTemplateRepository`, `UnitOfWork`, the migration | Testcontainers database per class | db-design §2–§5: constraints, lesson date, cascade, author survival, uniqueness |
| HTTP integration | the Web host (`WebApplicationFactory`) | Google ports faked (`FakeClassroomReader` records any call), Control Plane channel approving | api-design §2.5 status codes and pages, policies, antiforgery, PRG, encoding |
| Security | as HTTP | — | TC-5: allowed and forbidden per endpoint; read-only in `Application` for all three BR-025 causes; no Google call |

Every date and period test runs in `Europe/Kyiv` (TC-8; `JournalTestData.Kyiv`).

## 3. Scenarios by kind

**Positive.** Create, copy (of the built-in and of a created template), change
and delete save and audit; the list orders and labels; the built-in report of
AC-004; a copy's changed marks, materials, view and hours show (AC-005); the
lesson topics part; the scale label; English rendering.

**Negative.** Change and delete of the built-in (`400`, not audited); malformed
and unknown references (`400`, `404`); a deleted template (`404`); tampered forms
(`400` error page); invalid fields (`400` form re-rendered, nothing saved); a
duplicate name, including at commit time; anonymous and restricted sessions;
missing antiforgery token; read-only mode.

**Boundary.** Name 100 / 101 characters; mark text 30 / 31; label 10 / 11; hours
1, 10, 0, 11; scale with 1 row (0–100), 101 rows, 102 rows, gap, overlap, not
starting at 0, not ending at 100; percent rounding at 8.5 % / 8.4 %, extra credit
(105 %), zero maximum, `0 / 10`; lesson date at 00:30 local on the first day,
28 September published / 2 October due, created in September and scheduled for
1 October (AC-012); a teacher who left before the period.

**Validation.** Every key of `ReportTemplateFieldErrorKey` is produced by at least
one test and carries the field (and the row number for scale rows); every
`ReportMessageKey`; a malformed value is never echoed nor logged.

**Security.** S-01 … S-14: policy per page; allowed (Admin, Dean) and forbidden
(anonymous, temporary-password Dean) per endpoint; antiforgery `400` for the three
`POST`s; author and actor from the session, never from the form; template text
HTML-encoded; no Google call and no secret read in any mode; no template text or
Google data in logs or audit rows.

**Persistence.** db-design §7 list: round trip; normalized-name uniqueness and its
translation; `updated_at` advanced by a mark-only change; scale rows replaced with
the same `from` values; cascade delete; author account deleted → template stays,
`AccountDeleted`; every check constraint; the audit shape check;
`scheduled_time` nullable and written by the import; F2/F4 by lesson date.

## 4. Fixtures (`tests/ClassroomAgent.Tests/TestInfrastructure/`)

| File | Purpose |
|---|---|
| `ReportTemplateTestData.cs` | paths, built-in key, the 12-point preset, a two-row scale, `Settings(...)`, translation key families |
| `ReportTemplateFormBuilder.cs` | the posted form as ordered name/value pairs; `Valid()`, `Set`, `Repeat`, `Remove`, `Mark`, `LateMark`, `Ranges`, `TwelvePoint`; `Input()` for use cases, `Http()` for the host |
| `ReportTemplateWorld.cs` | the use cases over in-memory ports; templates seeded through `ReportTemplate.Create`; call log for evaluation order; a unit of work that can fail with the unique-name exception |
| `FakeJournalFieldSource.cs` | the journal-field port in memory, lessons filtered by lesson date, call and interval log |
| `ReportHostExtensions.cs` | SQL inserts of items with `scheduled_time` and of templates; row readers; template fingerprint; the real `JournalFieldSource` / `ReportTemplateRepository` over the host database |

Hosts, sign-in and the September journal come from `JournalHostExtensions`
(US-025). All names, titles and emails are invented (TC-4).

## 5. Production skeleton

OD-005 (a), Owner 2026-10-04: compile-only declarations the tests reference,
members throwing `NotImplementedException`, nothing registered in DI, no
migration. Listed in the test-generation report §2.

## 6. Excluded, with reason

- **Visual layout, phone width (NFR-070)** — not observable without a browser; the
  pages are server-rendered tables in a scrolling container, checked at
  SECURITY_REVIEW / human review.
- **The scale-row script** (`wwwroot/js`) — no JavaScript runtime in the test host;
  the test asserts only that the page references a same-origin script and renders
  the preset values from `Application` (api-design §2.8).
- **The delete–save race** (db-design N-4) — a timing window; by design it ends in
  the generic `500`, already covered by the host's exception-handler tests.
- **Exact query count** — asserted as "at most eight round trips per report" with a
  command counter in `JournalFieldSourceTests` / report tests, not as an exact SQL
  text.

## 7. Known limitations

- Unit tests seed templates through `ReportTemplate.Create`; until IMPLEMENTATION
  every such test is red at seeding, which is the expected red phase.
- HTTP `POST` tests first `GET` a form to obtain the antiforgery token; until the
  pages exist they fail at that `GET` (`404`).

## 8. Open Decisions affecting testing

None open. OD-001 … OD-004 resolved before activation / approval; OD-005 (a)
resolved by the Owner on 2026-10-04 (skeleton).
