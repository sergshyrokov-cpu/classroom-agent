---
artifact_type: test_strategy
story: US-062
version: 1
status: DRAFT
created_at: 2026-10-27T20:04:09Z
updated_at: 2026-10-27T20:04:09Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-062-data-health-banner-and-page.md
    version: null
  - path: docs/specifications/US-062-spec.md
    version: 1
  - path: docs/decisions/US-062-open-decisions.md
    version: 1
  - path: docs/designs/api/US-062-api-design.md
    version: 1
  - path: docs/designs/api/US-062-openapi.yaml
    version: 1
  - path: docs/designs/database/US-062-db-design.md
    version: 1
  - path: docs/designs/database/US-062-entity-model.md
    version: 1
supersedes: null
---

# US-062 Test Strategy — Data-health banner, «Стан даних» and the Admin's confirmation

## 1. Scope

Everything the Story adds: the problems computed from stored data (FR-001), who sees what (FR-002), the
banner in the layout (FR-003), the page `GET /workspace/data-health` (FR-004, FR-005), the Admin's
confirmation `POST /workspace/data-health/course-deletion-confirmation` (FR-006), the texts (FR-007, OD-003),
the empty-list date and its migration (FR-008, OD-002 a), and the schema of `AddDataHealth` (db-design §2 …
§7). Nothing in the Story calls Google; every Google port stays substituted and fixtures are synthetic (TC-4).

## 2. Test levels

| Level | Where | What it proves |
|---|---|---|
| Domain unit | `Application.UseCases.DataHealthInvariantTests` | `Course.ConfirmDeletedInClassroom`, `SyncState.CourseListEmptySince`, the two audit factories (entity model §1) |
| Application unit (ports in memory) | `Application.UseCases.CourseListEmptySinceTests` | the run sets / keeps / clears the date (FR-008.1) through `SyncWorld` (TC-1) |
| Application over PostgreSQL | `Web.UseCases.DataHealthQueryTests`, `Web.UseCases.ConfirmCoursesDeletedUseCaseTests` | the problem rules, counting and role shaping in the query result (spec §7: enforced in Application), read-only mode on the use case (TC-5), VR-001 / VR-002, all-or-nothing, the audit rows |
| Persistence | `Infrastructure.Persistence.DataHealthPersistenceTests` + adapted schema tests | migration name and backfill, columns, CHECKs, indexes, the `FOR UPDATE` read, the read port `IDataHealthSource` (TC-2) |
| HTTP (contract + rendering) | `Web.Pages.DataHealthBannerTests`, `Web.Pages.DataHealthPageTests`, `Web.Pages.CourseDeletionConfirmationHttpTests` | every status of api-design §2.6 (TC-3), allowed and forbidden roles (TC-5), texts in both languages (NFR-073), dates in Kyiv (TC-8), the banner's presence rules |
| Security matrix | `Web.Security.PermissionMatrixTests` (adapted) | both routes declare their policy; both policies admit exactly the §2 rows 749 / 750 roles |

The Application queries are resolved from a running installation's DI over real PostgreSQL rather than built
with substituted ports: the facts come from SQL the design fixes in shape (db-design §5.1), and the counting
rules in Application are only meaningful over them. The use case's read-only test calls the use case directly,
not the page (TC-5).

## 3. Fixtures and the markup contract

### 3.1 Fixtures

`TestInfrastructure/DataHealthHostExtensions` writes the stored facts straight into their columns (the US-060
`DataMarksHostExtensions` precedent): courses with state and `last_complete_read_at`, absence marks, graded
submissions with assigned and draft grades, items "not in Classroom", `sync_skipped_course` records of both
reasons, the `sync_state` row (failed / completed / running, last success, empty-list date) and the
`legitimacy_state` instant and compatibility. Seeding starts after the first retention purge (US-037).
Instants used in texts fall at night in UTC so their Kyiv date is the next day (TC-8). Hosts come from the
existing `JournalHostExtensions`, `DataMarksHostExtensions` (English Dean) and `ReadOnlyModeHost` (the three
read-only causes). The Control Plane fake never answers, so a seeded legitimacy instant stays as written.

`TestInfrastructure/DataHealthTexts` pins the FR-007 / OD-003 wordings as literals in both languages (the US-054
/ US-060 precedent: keys are IMPLEMENTATION's). Texts the Specification gives in Ukrainian only — «Проблем із
даними немає», «Архівні курси (не враховуються)», «Підтвердити: видалені в Classroom» — are asserted in
Ukrainian only. The banner's plural rule is the standard one: Ukrainian one (n mod 10 = 1, n mod 100 ≠ 11),
few (2–4, not 12–14), many; English one / other; English "teaching" does not inflect (FR-003.2's example).
Typographic apostrophes are read as the Specification's ASCII one («зв'язку»).

### 3.2 Page markup the tests read (binding on IMPLEMENTATION)

| Hook | Element |
|---|---|
| `id="data-health-banner"` | the banner; its whole text is the first `<a href="/workspace/data-health">` after the hook |
| `id="data-health-read-only"` | the read-only message on the page, before the technical section (FR-004.5) |
| `id="data-health-technical"`, `id="data-health-teaching"`, `id="data-health-archived"` | the three sections, in that order (FR-004.2); a section's text runs from its hook to the next hook |
| `id="data-health-message"` | the one-shot message (`Confirmed`, `Invalid`, `ListChanged`) |
| `id="course-deletion-confirmation"` | the Admin's form; one `<input type="checkbox" name="courseIds" value="<internal id>">` per X5 course |

Course journal links are `href="/workspace/journal?courseId=<id>"`. The X1 details carry a link to
`/settings/workspace-connection` inside the technical section. The page-search hint is the existing ordinary
variant (`page-search-hint`) as the first element after the title.

## 4. Scenarios

### 4.1 Positive

- Every teaching kind with its details: T1 (assigned and draft, item, student, points, maximum), T2 (title,
  date), T3 in its three variants (date, no date, never loaded by name or by Google id), T4 (date), with hints
  H1, H2 and the Dean's H4; journal links for loaded courses only (AC-005).
- Every technical kind: X1 with the diagnosis text, X2 stale and "none yet", X3, X4 with its date, X5 with the
  Admin's list and form, X6 with both dates and the role's hint, the upgrade reason (AC-006 … AC-009, AC-013,
  AC-014).
- The banner on the landing page, a teaching page and «Стан даних», for both roles (AC-001); the counts equal
  the page totals over a fixture of every kind (api-design §2.3).
- The confirmation of two of three courses: marks, instant, audit ids, PRG message once, the third untouched;
  the confirmed courses then count as T4 unless archived (AC-010, FR-006.3).
- The run sets the empty-list date at the first empty run and keeps it (AC-008, AC-016).

### 4.2 Negative

- No problem → no banner, «Проблем із даними немає» (AC-002); archived-only problems → no banner (AC-003).
- The corrected grade removes T1 at the next request (AC-004).
- The Dean's model and page carry no technical details: no diagnosis, no X3 name, no X5 list, no form, no
  upgrade reason (AC-006, AC-009, OD-004 a); the Admin's page has no H3 and no H4.
- A Dean's confirmation is `403`; an Admin's in read-only mode `409` (AC-011); a stale or unknown id `409`
  with «Список змінився — оновіть сторінку», nothing changed, no audit (AC-017).
- No banner without a session, on the forced password change (AC-015), in an `/api/v1` response, or when the
  computation fails (I-4, with a warning naming the exception type and no course data).
- A run in progress raises no X1 (I-1); `upgrade_required` within a day raises nothing (I-9).

### 4.3 Boundary

- T1: equal to the maximum, a zero maximum, no maximum, no grade → no problem; strictly above → problem, for
  the grade and the draft grade, in the SQL fact and in the result.
- X2 and X6: 25 hours → raised, exactly 24 and 23 hours → not (FR-001.3 "strictly more").
- VR-001: 1 and 1000 distinct ids accepted, 1001 distinct refused; 1001 entries that are 1000 distinct accepted.
- Banner plural forms: 0, 1, 2, 3, 5, 11, 21, 22 in Ukrainian; 0, 1, 2, 3 in English.
- Culture order: «Алгебра», «Ґрунтознавство», «Історія» (ordinal order would put «Ґ» last).

### 4.4 Validation

- VR-001 shape: no list, an empty list, `0`, `-3`, `abc`, ` 5`, `1.5`, `9223372036854775808`, 1001 distinct →
  `Invalid` / `400` with the page re-rendered, nothing changed, no audit.
- VR-002 state: an id now "not in Classroom", unmarked, or unknown → `ListChanged` / `409`.
- VR-003: no antiforgery token → `400` with `Error.PageExpired`.
- VR-004: an unknown stored error → the `Unexpected` text.

### 4.5 Security

- TC-5: page — Dean and Admin `200`, anonymous `302 /sign-in`, restricted session `302` to the change;
  confirmation — Admin `302`, Dean `403`, anonymous and restricted `302`. Permission matrix rows for both.
- Read-only mode on the use case for all three causes, the guard before the shape check; one refused audit row
  without ids; no Google call (TC-5, AD-6, api-design §2.5).
- Audit (SC-11): the succeeded row carries the Admin, no target, the ids sorted and distinct, no course name.
- No-store on the page (SC-14). The generic anonymous, antiforgery and no-store enumerations pick up both new
  routes once they exist.
- The banner's failure log carries no course data (SC-10).

### 4.6 Persistence

- `AddDataHealth` is the last migration, after `AddCourseListMarks`; it backfills a flagged row with its
  `started_at` and leaves an unflagged one (AC-016, OD-002 a).
- `sync_state.course_list_empty_since` nullable `timestamptz`; `ck_sync_state_course_list_empty_since` refuses
  a flag without date and a date without flag.
- `audit_event.confirmed_course_ids` nullable `bigint[]`; `ck_audit_event_course_deletion_shape` and
  `ck_audit_event_confirmed_course_ids_absent` accept the two designed rows and refuse every other shape.
- `ix_submission_graded` and `ix_course_work_not_in_classroom` with their columns and predicates.
- `GetForAbsenceConfirmationAsync` returns known ids only, tracked, and holds a `FOR UPDATE` lock until the
  transaction ends (a second session's `NOWAIT` gets `55P03`).
- `IDataHealthSource`: one fact row per course with a fact, none for a plain course; the detail queries' order
  (item, then student) and one row per grade.

## 5. Adapted existing tests (requirement changes of this Story)

- `AppUserMigrationTests` — the 24th migration ends `_AddDataHealth`.
- `SyncStateSchemaTests` — 16 columns, `course_list_empty_since` nullable.
- `InstallationAuditEventSchemaTests` — the new column and the two new constraints in the exact lists.
- `PermissionMatrixTests` — the two §2 rows and the two endpoints.
- `SyncWorld.CourseRepository` — implements the new port member (test infrastructure).

## 6. Excluded scenarios

- The banner's query cost: db-design §5.2 asks IMPLEMENTATION for an `EXPLAIN ANALYZE` in its report; no time
  budget exists to assert.
- The exact wording of the X1 failed step and finish, the X2 last-success time, the X5 course state and the
  upgrade reason: FR-007 fixes no text for them; the tests assert the values in the model and the presence of
  the diagnosis text only.
- The text of the `Invalid` message: OD-003 fixes none; the `400` page is asserted, not its message.
- English texts the Specification does not give (see §3.1).
- A concurrent synchronization writing between the lock and the commit: the lock itself is proven (§4.6);
  interleaving two live transactions through the use case is not deterministic without a hook the design does
  not have.

## 7. Known limitations

- "A run loads the corrected grade" (AC-004) is simulated by updating the stored grade; the run's own grade
  writes are covered by US-055 / US-059 tests. FR-001.4 makes the result depend on stored data only.
- The banner is checked on three representative layout pages, not every route; the layout is shared, and the
  forced password change is the one exclusion (AC-015).
- `SkippedCourse` (the new DTO in `Application.Models.Dtos`) shares its simple name with the existing
  `Application.Models.SkippedCourse` of US-014; the tests never import both namespaces in one file. Noted for
  IMPLEMENTATION (non-blocking finding INFO-1).

## 8. Open Decisions affecting testing

None. OD-001 … OD-005 are resolved; the tests apply OD-001 a (AC-015), OD-002 a (backfill), OD-003 (texts),
OD-004 a (archived X3 for the Dean) and OD-005 a (zero in the banner).
