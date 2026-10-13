---
artifact_type: test_strategy
story: US-046
version: 1
status: DRAFT
created_at: 2026-10-13T18:03:08Z
updated_at: 2026-10-13T18:03:08Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-046-page-search-hint.md
    version: null
  - path: docs/specifications/US-046-spec.md
    version: 1
  - path: docs/decisions/US-046-open-decisions.md
    version: 2
  - path: docs/designs/api/US-046-api-design.md
    version: 1
  - path: docs/designs/database/US-046-db-design.md
    version: 1
supersedes: null
---

# US-046 Test Strategy — Page-search hint on teaching-data pages

## 1. Scope

Presentation only (spec §1, FR-009): one hint line on the nine FR-001 pages,
absent everywhere else. No endpoint, input, entity, migration or audit event —
so there are no contract, validation-boundary or schema tests (API_DESIGN and
DB_DESIGN are NOT_APPLICABLE; no `openapi.yaml`, no entity model).

Every test finds the hint by the binding markup hook of api-design §3:
`<p id="page-search-hint" class="page-search-hint" data-variant="ordinary|course-list|paged-list">`.
Texts are compared with spec FR-003 verbatim (checked against
`trebovaniya.md` v91 §5), held once in
`TestInfrastructure/PageSearchHintTestData.cs`.

## 2. Test levels

| Level | Where | What |
|---|---|---|
| Integration (HTTP, real PostgreSQL via Testcontainers) | `Web/Pages/PageSearchHintTests.cs` | rendered pages of the installation and the Control Plane |
| Source/static | `Web/Localization/PageSearchHintTranslationTests.cs` | translation files, views, stylesheet |

No unit test: there is no use case or rule to isolate (spec §1). No Google port
is called (TC-4): the hosts use the existing fakes, seeded by SQL.

## 3. Scenarios

**Positive**
- Each of the nine pages × Admin and Dean: exactly one hint, variant per
  FR-001, Ukrainian text, first element after the title (layout `<h1>`;
  Meet codes: the view's own second `<h1>`, OD-002 a).
- The same nine pages in English after the user chooses English.
- Course list: the quoted field name equals `Courses.Form.Search` in uk («…»)
  and en ("…"), and the field label itself still follows on the page.

**State / invariance (AC-004, FR-006, FR-007)**
- Read-only mode (Installation suspended), Dean: unchanged hint on all nine.
- No rows at all (nothing seeded) on the six pages that need no course.
- Teacher list: one page (empty), page 1 and page 3 of 120 teachers — same text.
- Journal rendering a validation message (400 malformed course, 404 unknown
  course): hint still shown (spec §8).
- Phone vs desktop `User-Agent`: identical hint.
- Visiting all nine pages twice: no change in the teaching tables
  (xmin fingerprint), no new audit row, no `Set-Cookie` other than the session
  and antiforgery cookies, hint shown again on the second visit.
- Leaf element: no child markup in the hint — no close control, no script.

**Negative (AC-005, FR-005)**
- Admin: landing, Dean accounts, workspace connection, connection instruction,
  access check, template list / new / edit / deletion, Meet code course-choice
  (OD-001 a). Dean: landing, own password. Dean with temporary password: forced
  change. Error page: unknown course card (404), `/error/404`, `/error/500`,
  anonymous `/error/404`. Anonymous sign-in. Control Plane: setup, sign-in, the
  Owner's home and installation list. Each measured on a host whose course list
  does carry the hint.

**Translation / encoding (AC-006, S-03, I-3)**
- Each of the six texts is a `<value>` of `SharedResource.{uk,en}.resx`.
- No `.cshtml` of Web or Control Plane holds a fragment of the texts.
- No view that renders the hook uses `Html.Raw` (none uses it today).
- `site.css` has a `.page-search-hint` rule setting `font-size`.

**Validation, persistence, security** — no input (VR-001), no schema; the
security-relevant checks are the absence on anonymous/error/Control Plane pages,
"writes nothing / no cookie", and encoding. Authorization is unchanged (S-01)
and stays covered by the existing allowed/forbidden-role suites.

## 4. Fixtures

`JournalHostExtensions.StartAsync` (actor, legitimacy cause) +
`MeetReportsHostExtensions.SeedWorldAsync` (a course, Meet meetings and codes)
for the page catalogue; `SeedCourseListAsync`, `SeedNumberedTeachersAsync`,
`SeedJournalAsync`, template insertion as in `ReportTemplatePagesTests`;
`ControlPlaneTestHost` for the Control Plane.

## 5. Excluded, with justification

- "Small and unobtrusive" beyond a `font-size` rule — visual judgement (I-3);
  checked at HUMAN_PR_APPROVAL.
- Wrapping on a narrow screen (spec §9) — CSS layout, not observable over HTTP.
- AC-007 "nothing else changes" — evidenced by the whole existing suite staying
  green, plus the placement check that both titles remain.

## 6. Known limitations

- Absence tests pass before implementation; they are regression guards and are
  paired with a presence assertion on the same host.
- The source tests read the repository tree (`StaticFiles.RepositoryRoot`).

## 7. Open Decisions affecting testing

None. OD-001 (a) and OD-002 (a) are resolved and encoded in the tests.
