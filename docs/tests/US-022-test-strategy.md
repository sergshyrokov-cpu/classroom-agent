---
artifact_type: test_strategy
story: US-022
version: 1
status: DRAFT
created_at: 2026-10-12T09:40:00Z
updated_at: 2026-10-12T09:40:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-022-teachers-and-their-courses.md
    version: null
  - path: docs/specifications/US-022-spec.md
    version: 1
  - path: docs/designs/api/US-022-api-design.md
    version: 1
  - path: docs/designs/api/US-022-openapi.yaml
    version: 1
  - path: docs/designs/database/US-022-db-design.md
    version: 1
  - path: docs/designs/database/US-022-entity-model.md
    version: 1
  - path: docs/decisions/US-022-open-decisions.md
    version: 2
supersedes: null
---

# US-022 Test Strategy — Teachers and their courses

## 1. Scope

The teacher list `GET /workspace/teachers` (spec FR-001 … FR-016): who is listed
under each status, the row (name rule, email, course count, link to the course
list), order and paging, the empty state, validation of `status` and `page`, the
address and the language switcher's return path, the home-page entry,
authorization, read-only mode, translations and logging. No schema change
(db-design §1), so there are no migration or schema tests; the existing
`HasPendingModelChanges` test keeps guarding that.

Out of scope (spec §10): a teacher card, search, export, links from names on
other pages, any change to the course list. The course list is exercised only as
the target of the row link (AC-004, AC-016).

## 2. Test levels

| Level | Class | What it proves |
|---|---|---|
| Unit (TC-1) | `Application.UseCases.TeacherListQueryTests` | the state handed to the port, row mapping, name rule, link, paging and clamping, empty state, return path, validation before any read, ≤ 2 reads |
| Unit | `Application.UseCases.TeacherListUrlTests` | the teacher-list URL rule |
| Integration (TC-2) | `Infrastructure.Persistence.TeacherListSourceTests` | T1/T2 on PostgreSQL: listing and counting rules, projection, Ukrainian order equal to the course list's teacher choice, database paging, agreement with the course list's filter, one command per call, no write |
| Integration / HTTP | `Web.Pages.TeacherListPageTests` | the page end to end: rows, statuses, link followed into the course list, paging, address, empty message, home page, 400 page, HTML encoding |
| Security (TC-5) | `Web.Security.TeacherListAuthorizationTests` | Admin and Dean allowed; anonymous and forced-password Dean redirected; `ViewCourses` declared |
| Security (TC-5) | `Web.UseCases.TeacherListReadOnlyTests` | all three BR-025 causes and normal mode: list shown, nothing written, no Google call — in `Application` and over HTTP |
| Localization (TC-8) | `Web.Localization.TeacherListTranslationTests` | new and reused keys in uk and en; page in both languages; Google data unchanged |
| Logging (SC-10) | `Web.Logging.TeacherListLoggingTests` | Information with the total and no personal data; Warning naming the parameter, never the value; ignored parameters never logged |

There is no JSON endpoint and no `POST`: the API-6 error body and antiforgery
tests do not apply (api-design §5). The endpoint is picked up by the existing
enumerating tests (anonymous closed list, no-store) without change.

## 3. Scenarios

### Positive
- Default ACTIVE lists current teachers of active courses with active counts (AC-001).
- ARCHIVED and "all" list and count by that status; "all" is the sum (AC-002, I-7).
- Row: profile name, email as stored, course count; link to the course list (AC-003, AC-004).
- 120 teachers: 50 per page in label order, total shown, paging keeps the status (AC-005, AC-015).
- Home page: "Teachers" second in the Google Workspace section, also in read-only mode (AC-007).
- Read-only mode: list works (AC-009); both languages (AC-011).

### Negative
- Teacher of only an archived course, former teacher, student: not listed under ACTIVE (AC-001).
- Off-roster teacher membership and a student membership are not counted (AC-016).
- Anonymous → sign-in; forced-password Dean → password change (AC-010).
- `teacherId`, `q` and unknown parameters ignored, never echoed or logged (AC-013).

### Boundary
- Page past the last → last page; page 100 000 accepted; 100 001 refused (VR-002).
- Exactly one page → no paging links; zero teachers → empty state, page 1, no page read.
- Names: one part only, empty string as absent, email only, neither (FR-004).
- Equal labels ordered by id; nameless last (AC-014).

### Validation
- `status`: each of six tokens in any case; empty = ACTIVE; unknown word, number,
  leading/trailing/inner spaces, repeated → `StatusMalformed` (VR-001).
- `page`: `0`, sign, spaces, letters, decimal point, overflow, non-ASCII digit,
  repeated → `PageMalformed`; empty = 1 (VR-002).
- Both malformed → both messages in order; rejected value not in model, form,
  links, switcher, log (FR-010, S-07).

### Security
- S-01/S-02 authorization; S-04 read-only and no Google call; S-05/S-06 address
  and return path; S-07/S-08 logging; S-09 HTML encoding; S-11 no `@`, name or
  Google id in any URL; S-12 nothing written (fingerprint of the three US-014
  tables, audit and sync-state counts).

### Persistence
- T1/T2 rules and order on PostgreSQL; the teacher list's order equals
  `CourseListSource.GetTeacherChoiceAsync`; for every listed (participant,
  status) the count equals `CountCoursesAsync` of the course list's filter
  (AC-016); one command per call (AC-015).

## 4. Markup hooks (binding on IMPLEMENTATION)

Defined in `TeacherListTestData.Markup`:

| Hook | Markup |
|---|---|
| form | `<form id="teacher-list-form" method="get" action="/workspace/teachers">` with `<select name="status">` |
| row | `<tr data-participant-id="{id}">`, the course-list link inside the row |
| total | element `id="teacher-list-total"` |
| paging | `<nav id="teacher-list-paging">` |
| empty | element `id="teacher-list-empty"`; no `<table>` with it |
| validation message | element with class `form-error`, as the course list |
| home page | the link inside the existing `<nav id="workspace-section">`, second |

## 5. Translation keys (binding on IMPLEMENTATION)

New, in both resource files: `Teachers.NavigationEntry`, `Teachers.Title`,
`Teachers.Column.Name`, `Teachers.Column.Email`, `Teachers.Column.Courses`,
`Teachers.Column.Link`, `Teachers.CoursesLink`, `Teachers.Paging.Total`,
`Teachers.Empty` — the names api-design §2.8 gave as indicative, fixed here.
Reused: `Courses.Form.Status`, `Courses.Form.Submit`, `Courses.Status.*`,
`Courses.Teacher.NoName`, `Courses.Paging.Previous`, `Courses.Paging.Next`,
`Courses.Validation.StatusMalformed`, `Courses.Validation.PageMalformed`.

## 6. Fixtures

- `TeacherListTestData` — path, parameters, hooks, keys, the literal course-list
  link (api-design §2.6) so the link test does not trust `CourseListUrl`.
- `TeacherListHostExtensions` — the seeded school (5 courses, 5 people covering
  current, former, archived-only teacher, student, and the AC-016 mixed case),
  120 numbered teachers inserted in reverse label order (id order ≠ label
  order), row readers. Rows are inserted by SQL through the US-020 helpers.
- `FakeTeacherListSource` — in-memory port with per-state counts, label order,
  call recording.
- Existing: Testcontainers PostgreSQL fixture, `JournalHostExtensions` actors,
  `ReadOnlyModeHost`, `CourseListFingerprintAsync`, substituted Google ports with
  a call counter (TC-4).

## 7. Excluded scenarios

- Query plan / index use (db-design §4): no new index; plans are not asserted —
  the one-command and bounded-read tests cover FR-014.
- Phone layout (NFR-070): visual, not automated, as for US-020.
- A `404` case: the page has no identifier (api-design §2.4).
- Time zone (TC-8): the page shows no time (spec §9).

## 8. Known limitations

- The "nothing synchronized" case is the same message as "no match" (I-2), so
  one empty-state test covers both.
- `TeacherListQueryTests` proves the rejected value is absent from the model by
  `ToString()` of the records plus explicit field checks; the HTTP tests prove it
  for the rendered page.

## 9. Open Decisions affecting testing

OD-010 (compile-only skeleton) — resolved (a) by the Owner, 2026-10-12. None
open.
