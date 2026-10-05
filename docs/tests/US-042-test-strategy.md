---
artifact_type: test_strategy
story: US-042
version: 1
status: DRAFT
created_at: 2026-10-05T12:49:19Z
updated_at: 2026-10-05T12:49:19Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-042-names-in-report.md
    version: null
  - path: docs/specifications/US-042-spec.md
    version: 1
  - path: docs/designs/api/US-042-api-design.md
    version: 1
  - path: docs/designs/api/US-042-openapi.yaml
    version: 1
  - path: docs/designs/database/US-042-db-design.md
    version: 1
  - path: docs/designs/database/US-042-entity-model.md
    version: 1
  - path: docs/decisions/US-042-open-decisions.md
    version: 2
supersedes: null
---

# US-042 Test Strategy — Names of students and teachers in a template report

Traceability: `docs/tests/US-042-ac-test-matrix.md`. Evidence:
`docs/evidence/US-042-test-generation-report.md`.

## 1. Scope

- The name parts of a participant: entity normalisation, synchronization
  insert/replace, the Google adapter mapping, the columns (FR-001).
- The template setting "names": aggregate, forms, saves, field validation,
  audit, the column, the migration filling `profile` (FR-002, FR-007, FR-009).
- The FR-003 name rule for students and teachers, ordering (FR-003, FR-006).
- The effective source, the switch, the return path, the malformed parameter,
  read-only mode (FR-004, FR-005, FR-008, VR-002).
- Logging without names or rejected values; translations (FR-010, FR-011).
- Out: export (US-028), the US-025 journal and the Meet pages (spec §10) — only
  guarded by their existing tests, which keep passing.

## 2. Levels

| Level | Where | What |
|---|---|---|
| Unit (Application, ports in memory — TC-1) | `ReportTemplateWorld`, `SyncWorld` | name rule, ordering, effective source, switch, return path, validation, saves, audit, read-only, sync upsert |
| Domain | same classes | `ClassroomParticipant` parts, `ReportTemplate.NameSource`, built-in value |
| Integration, PostgreSQL via Testcontainers (TC-2) | `NamePartsPersistenceTests`, schema tests | columns, check, no default, no index, migration data effect, EF round trips, `JournalFieldSource` members |
| Adapter (scripted HTTP, no live Google — TC-4) | `GoogleClassroomReaderTests` | `familyName` / `givenName` → `RosterEntry` |
| HTTP (`WebApplicationFactory`) | `ReportNameSourcePageTests`, `ReportNameSourceAuthorizationTests` | 200/400/302, switch links, not echoed, form round trip, roles (TC-5) |
| Logging, localization | `ReportNameSourceLoggingTests`, `ReportNameSourceTranslationTests` | FR-011, SC-10; FR-010 |

## 3. Scenarios by kind

- **Positive:** both sources for students and teachers; one profile part; the
  switch both ways; template setting stored and changed both ways; copy takes
  the source; new form defaults to profile; parts imported and replaced.
- **Negative:** malformed `names` in the address (10 shapes) and in the form (8
  shapes, missing/empty/repeated/unknown); built-in change refused; check
  constraint refuses other codes; NOT NULL refuses a missing source.
- **Boundary:** 750-character parts (kept / cut); email with no `@`, nothing
  before `@`, two `@`; empty and whitespace parts; equal shown names (id tie).
- **Validation:** VR-001 field error with other errors at once; VR-002 key order
  after the period pair; validated without a course.
- **Security:** allowed Admin/Dean and forbidden anonymous/restricted Dean on
  every changed endpoint; no value echoed in page, return path or log; no
  personal data in logs; read-only: report and switch work with the guard not
  asked, save refused (control in the same world); no audit for viewing.
- **Persistence:** column types/bounds/nullability, check, no default, no index,
  migration fills existing templates with `profile` and invents no parts.

## 4. Fixtures (`tests/ClassroomAgent.Tests/TestInfrastructure/`)

- `FakeJournalFieldSource.AddMember` takes `surname` / `givenName` instead of a
  full name.
- `ReportTemplateTestData`: `NamesField`, `ReportUrl(…, names)`,
  `Settings(…, nameSource)`, key `Report.Validation.NameSourceMalformed`.
- `ReportTemplateFormBuilder.Valid()` sends `names=profile` (a legitimate form
  always does).
- `ReportHostExtensions.InsertTemplateAsync` writes `name_source` when the
  column exists; `ColumnExistsAsync`, `NameSourceOfAsync`.
- `CourseRows.InsertParticipantAsync` takes optional `surname` / `givenName`.
- `SyncWorld.ClassroomReader.WithRoster` replaces a seeded roster for a second
  run.

## 5. Production skeleton (OD-001 a)

Compile-only: listed in the test-generation report §2. IMPLEMENTATION owns it.

## 6. Excluded, with reason

- Export from the page (US-028) — out of scope (spec §10).
- Visual layout of the switch at phone width (NFR-070) — not automatable here;
  checked at HUMAN_PR_APPROVAL.
- A live Google profile — TC-4.

## 7. Known limitations

- The two fixture branches that depend on whether a column exists
  (`InsertTemplateAsync`, `ReportTemplateSchemaTests.InsertRootAsync`) exist only
  so US-027 tests stay green during the red phase; after the migration lands
  they always take the US-042 branch.
- Existing US-027 tests that encode the changed requirement (names in the
  report, the template table's column list, the member query) are rewritten
  and are red until IMPLEMENTATION — listed in the report.

## 8. Open Decisions affecting testing

OD-001 (skeleton) — resolved (a), Owner 2026-10-05. No other.
