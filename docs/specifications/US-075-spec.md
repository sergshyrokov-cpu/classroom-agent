---
artifact_type: specification
story: US-075
version: 2
status: DRAFT
created_at: 2026-11-07T17:59:20Z
updated_at: 2026-11-07T18:16:31Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-075-data-problem-hints.md
    version: null
  - path: trebovaniya.md
    version: 113
  - path: docs/decisions/US-075-open-decisions.md
    version: 2
  - path: docs/reviews/specifications/US-075-spec-review.md
    version: 1
supersedes: 1
---

# US-075 Specification: a short hint under each kind of data problem, and banner links to each section

Grounded in `trebovaniya.md` v113: section 4 Epic 1 «Состояние данных» (v99, v100, v101, v102, v103, and the
v110 bullets «баннер» and «подсказка у каждой проблемы»), Epic 1 «Смена технического аккаунта» (v101, the
confirmation «удалены в Classroom»), section 5 «Подсказка поиска на странице» (v91, v108, v113), section 8
(NFR-078 period rule), section 9 read-only mode. Business rules BR-026, BR-045, BR-047, BR-048, BR-067;
non-functional requirements NFR-073, NFR-075, NFR-076, NFR-078; SC-4, SC-13, SC-15.

The Story was aligned with v110. v111…v113 change nothing in its scope except v113, which adds the «Стан даних»
page to the pages with the page-search hint (NFR-075). The page already renders that hint (US-062/US-070), but
the hint tests do not list it; FR-007 carries that test fix into this Story, as agreed for the first Story after
v113 (Owner, 2026-11-07).

`trebovaniya.md` section 7 has no open item this Story depends on. The Story's OD-001 is resolved (a).

**Version 2** answers the review `docs/reviews/specifications/US-075-spec-review.md` v1: M-1 (FR-005.3 and FR-005.4 —
in read-only mode the X5 hint is not shown, and every hint raised in that mode is checked against what the mode
blocks; the derived key `DataHealth.Hint.CoursesNotVisible.ReadOnly` is dropped), m-1 (I-7: the T2 text is kept as
agreed, for the Owner to confirm), m-2 (I-8: the X5 hint with archived courses in its list, now named).

## Overview

The «Стан даних» page (`/workspace/data-health`, US-062, US-061, US-063) lists technical and teaching problems.
For most kinds it names what is wrong but not what to do. This Story adds one fixed hint under each kind that
has none, from the translation files, shown at once, with no click and no script; it gives a Dean's title of
the empty-list problem without the Admin's instruction; and it turns the two parts of the banner into two
links, each to its own section of the page.

Nothing is computed anew: the counts, the kinds, who sees which problem, and the page's data stay as they are.
API design and database design are expected to be empty deltas — no endpoint, schema, DTO field or audit event
is added.

## Business Goal

A Dean or Admin reading a problem knows what it means and who fixes it where, without asking (Story; v110). The
banner takes them straight to the section they care about. Clear hints also shorten the pilot note for Deans and
Admins.

## Business Flow

1. Any installation page with problems shows the banner «N технічних проблем, M навчальних». Each non-zero part
   is a link to its section of «Стан даних»; a zero part is plain text.
2. The user follows a link and lands on the section.
3. Under each kind of problem the user reads its hint: a teaching hint (Dean and Admin) says who corrects it in
   Classroom; a technical hint (Admin only) says what to check and where. A Dean sees for a technical problem
   only its title and «Повідомте Адміністратора», as today.
4. Archived courses show their marks as before, with no new hint on fixing.

## Functional Requirements

Kind names below are the page's existing kinds (`TechnicalProblemKind`, the teaching kinds of US-062 FR-001):
X1 `LastRunFailed`, X2a `NoRecentSuccess`, X2b `NoSuccessYet`, X3 `UnknownCourseState`, X4 `CourseListEmpty`,
X5 `CoursesNotVisible`, X6 `ControlPlaneUnreachable`; teaching T1 points above the maximum, T2 items not in
Classroom, T3 skipped course, T4 course not in Classroom.

### FR-001 The banner: two links

1. The banner keeps its wording, its translation keys (`DataHealth.Banner` "{0}, {1}" and the plural part keys)
   and its visibility rule (shown while there is at least one counted problem; US-062 FR-003).
2. A part whose count is above zero is a link: the technical part to `/workspace/data-health#data-health-technical`,
   the teaching part to `/workspace/data-health#data-health-teaching` (the section ids already exist). The
   separator ", " from `DataHealth.Banner` is outside both links.
3. A part whose count is zero is plain text with its usual wording and no link.
4. A link does not wrap inside itself: when the line does not fit, the second part moves to the next line whole.
   This is done by a stylesheet class in the host's own CSS (no inline style, no script; SC-15 CSP).
5. Both texts are HTML-encoded as today; the counts are numbers, the parts come from translation files.

### FR-002 Teaching hints (Dean and Admin)

1. In the teaching section, for each course that has T1, the hint `DataHealth.Hint.AboveMaximum` is shown once,
   directly under the kind's heading line «Бали вище максимуму» and before its list.
2. For each course that has T2, the hint `DataHealth.Hint.ItemsNotInClassroom` is shown once, directly under the
   heading line «Завдання або матеріали, яких немає в Classroom» and before its list.
3. Both roles see both hints (they are teaching problems; v110).
4. The existing teaching hints stay unchanged: T3 «курс не оновлюється…», T4 «курс видалено або приховано…», and
   the Dean's «Якщо з викладачем вирішити не вдалося, зверніться до Адміністратора».

### FR-003 Technical hints (Admin only)

Shown only when the viewer is an Admin, under the problem's title and after its existing details:

| Kind | Key | Link |
|---|---|---|
| X2a `NoRecentSuccess` | `DataHealth.Hint.NoRecentSuccess` | the existing connection page link (`DataHealth.Technical.ConnectionPage` → `/settings/workspace-connection`), after the hint |
| X2b `NoSuccessYet` | `DataHealth.Hint.NoSuccessYet` | the same link, after the hint |
| X4 `CourseListEmpty` | `DataHealth.Hint.CourseListEmpty` | none |
| X5 `CoursesNotVisible` | `DataHealth.Hint.CoursesNotVisible` (not shown in read-only mode, FR-005.3) | none; placed before the list and its confirmation form |
| X3 `UnknownCourseState` | `DataHealth.Hint.UnknownCourseState` | none |

1. X1 keeps its diagnosis, step, time and connection page link; X6 keeps «Повідомте Власника» and the
   upgrade-required reason. They get no new hint.
2. The X4 hint does not depend on the problem's details: today X4 carries no details object even for an Admin,
   so the view selects the hint by the viewer's role, not by the presence of details.
3. The X3 hint is shown once per X3 problem (one per course), after the course name and «Пропускається з …».

### FR-004 The Dean and technical problems

1. A Dean sees for every technical problem its title and `DataHealth.Hint.TellTheAdmin`, as today; none of the
   FR-003 hints and no connection page link (the Dean's problems carry no details; US-062 FR-004).
2. **OD-001 a):** the Dean's title of X4 ends at its date: new key `DataHealth.Technical.CourseListEmpty.Dean`
   «Google не повертає жодного курсу з {0}» / «Google has returned no course since {0}». The Admin's title
   `DataHealth.Technical.CourseListEmpty` stays whole, with «— перевірте роль технічного акаунта».

### FR-005 Read-only mode

1. Hints and banner links render the same in read-only mode; the page already works there (it reads stored data
   only; BR-026).
2. In read-only mode X2a, X2b and X6 are not raised (US-062, v103), so their hints do not arise.
3. **X5 in read-only mode: no hint.** Both sentences of the X5 hint name what read-only mode blocks (BR-026):
   restoring the previous technical account is a change of connection settings; «позначка зникне сама» needs a
   synchronization; selecting and confirming is the blocked confirmation «удалены в Classroom». Only «дайте новому
   потрібну роль» (done in the Google Admin console) remains possible, and even then the mark stays until the mode
   ends. So in read-only mode the X5 problem shows its title and list with no hint; the read-only notice at the top
   of the page (`Landing.ReadOnly.*`) states the mode. No new text is derived from the agreed one. The form and its
   button stay as they are today (outside this Story; I-4).
4. **The other hints raised in read-only mode** (FR-005.2 leaves T1, T2, X3, X4, X5) were checked against BR-026:

   | Hint | What it asks or promises | Blocked in read-only mode? | Shown |
   |---|---|---|---|
   | T1 `AboveMaximum` | the teacher corrects it in Classroom; the problem goes after the next synchronization | no: the teacher acts in Classroom; it names no time, and the next synchronization runs once the mode ends | yes |
   | T2 `ItemsNotInClassroom` | contact the teacher | no | yes |
   | X3 `UnknownCourseState` | tell the Owner | no | yes |
   | X4 `CourseListEmpty` | the super-admin changes the role in the Google Admin console | no: done in Google; it promises no result in the program | yes |
   | X5 `CoursesNotVisible` | restore the account; the mark goes by itself; select and confirm | yes (FR-005.3) | no |

### FR-006 Archived courses

1. The archived section shows none of the FR-002 or FR-003 hints: Google does not allow changing an archived
   course (v110). Its marks and existing texts (including the archived X3 line, «Повідомте Адміністратора» for a
   Dean, and the T4 / T3 texts) are as before.
2. An archived course in the X5 list is part of the technical X5 problem, which has one hint for the whole
   problem (FR-003); no per-course hint is added.

### FR-007 Carried test fix: the page-search hint on «Стан даних» (v113, NFR-075)

`PageSearchHintTests` lists the pages with the ordinary page-search hint; «Стан даних» joins that list, for both
roles, with the ordinary variant. No production change is expected: the page already renders the hint
(`_PageSearchHint`, ordinary variant). If the test shows otherwise, the page is fixed to NFR-075.

### FR-008 Translation texts

New keys, in both `SharedResource.uk.resx` and `SharedResource.en.resx` (NFR-073). Texts as agreed in the Story;
one-sentence texts end without a period, texts of several sentences keep them (NFR-078).

| Key | Ukrainian | English |
|---|---|---|
| `DataHealth.Hint.AboveMaximum` | Зазвичай це помилка введення: викладач виправляє оцінку або максимум завдання в Classroom, і після наступної синхронізації проблема зникає | Usually an entry error: the teacher corrects the grade or the item's maximum in Classroom, and the problem goes after the next synchronization |
| `DataHealth.Hint.ItemsNotInClassroom` | Завдання видалено або повернуто в чернетку в Classroom, оцінки в програмі збережено. Якщо це помилка, зверніться до викладача. | The item was deleted or returned to draft in Classroom; its grades stay in the program. If that was a mistake, contact the teacher. |
| `DataHealth.Hint.NoRecentSuccess` | Перегляньте стан синхронізації на сторінці підключення. Якщо запуски не починаються, повідомте Власника. | Look at the synchronization state on the connection page. If runs do not start, tell the Owner. |
| `DataHealth.Hint.NoSuccessYet` | Перевірте налаштування на сторінці підключення. Перша синхронізація великої школи може тривати понад добу. | Check the settings on the connection page. A large school's first synchronization can take over a day. |
| `DataHealth.Hint.CourseListEmpty` | З меншими ролями Google курсів не показує. Роль технічного акаунта змінює супер-адміністратор школи в консолі адміністратора Google. | With lesser roles Google shows no courses. The school's super-admin changes the technical account's role in the Google Admin console. |
| `DataHealth.Hint.CoursesNotVisible` | Якщо акаунт змінено помилково, поверніть попередній або дайте новому потрібну роль, і позначка зникне сама. Якщо курси справді видалено, позначте їх і підтвердьте. | If the account was changed by mistake, restore the previous one or give the new one the needed role, and the mark goes by itself. If the courses really were deleted, select them and confirm. |
| `DataHealth.Hint.UnknownCourseState` | Google повернув стан курсу, якого ця версія програми не знає, і курс не оновлюється. Повідомте Власника. | Google returned a course state this version of the program does not know, and the course is not updated. Tell the Owner. |
| `DataHealth.Technical.CourseListEmpty.Dean` | Google не повертає жодного курсу з {0} | Google has returned no course since {0} |

Key names are a technical choice and may be adjusted at design; the texts are not.

### FR-009 Presentation

1. A hint is a separate paragraph with a hint style class: secondary, restrained text (v99 «сдержанный»; the
   Owner's restrained-tone rule), never a banner, popup, icon-only or expandable element (Story Out of Scope).
2. No script is added; the hints and links work with scripts off (v110 «без скриптов»).
3. The layout works at phone width: no horizontal scroll caused by a hint or the banner (NFR-076).

## Acceptance Criteria

- **AC-001 Two links in the banner.** Given N > 0 and M > 0, when any installation page opens, then each part is a
  link to its own section (`#data-health-technical`, `#data-health-teaching`) of `/workspace/data-health`, the
  comma is outside both links, and each link carries the no-wrap class. *(FR-001.2, FR-001.4)*
- **AC-002 A zero part is not a link.** Given N = 0 or M = 0 (not both), then that part is plain text with its usual
  plural wording and the other part is a link. *(FR-001.3)*
- **AC-003 Teaching hints.** Given a current course with T1 and one with T2, when a Dean or an Admin opens the page,
  in both languages, then each kind's hint appears once per course that has it, with the FR-008 text, under the
  kind's heading. *(FR-002, FR-008)*
- **AC-004 Technical hints for the Admin.** Given each of X2a, X2b, X3, X4, X5, when an Admin opens the page, in both
  languages, then the kind's hint appears with the kind's existing details, and the connection page link follows
  the X2a and X2b hints. *(FR-003, FR-008)*
- **AC-005 The Dean sees no technical details.** Given the same technical problems, when a Dean opens the page, then
  none of the FR-003 hints or the connection page link appears; each technical problem shows «Повідомте
  Адміністратора»; the X4 title ends at its date with no instruction to check the role, while the Admin's X4
  title stays whole. *(FR-004, OD-001 a)*
- **AC-006 Archived courses.** Given an archived course with T1, T2 or X3, then the archived section shows none of
  the new hints, and its marks and existing texts are as before. *(FR-006)*
- **AC-007 Works without scripts and in read-only mode.** Given scripts off, or read-only mode, then hints and banner
  links render the same, except that in read-only mode the X5 problem shows no hint; the T1, T2, X3 and X4 hints
  are shown in read-only mode as outside it. *(FR-005, FR-009.2)*
- **AC-008 Nothing else changed.** Given the counts, the kinds and the rights, then they are as before; the page
  calls no Google API and writes no audit event; every new key exists in both translation files; the page-search
  hint test covers «Стан даних» for both roles; a person checks the page and the banner on a computer and a phone.
  *(FR-001.1, FR-007, FR-008, FR-009.3)*

## Validation Rules

- **VR-001** The page and the banner take no new input: no request field, query parameter or route value is added.
  The existing confirmation form keeps its validation (US-061).
- **VR-002** A hint is chosen only from the problem's kind, the viewer's role, the section (current or archived)
  and the read-only flag. No hint text is built from Google data or user input.
- **VR-003** Every new key exists in both translation files with a non-empty value; a missing key is a test
  failure, not a fallback to the key name.

## Security Requirements

- **SEC-001** Authorization is unchanged: the page and the banner keep their policies (Admin, Dean; SC-4 deny by
  default). No new endpoint.
- **SEC-002** A Dean receives no technical details: the FR-003 hints and links are rendered only for an Admin, and
  the Dean's model still carries no details (US-062 FR-004; v110 «Декан … без подробностей»).
- **SEC-003** Hints are fixed texts with no personal data; course names and Google data around them stay
  HTML-encoded as today.
- **SEC-004** No script, inline style or external resource is added (SC-15 CSP); the no-wrap rule lives in the
  host's stylesheet.
- **SEC-005** No new outbound call; the page reads stored data only (SC-13, BR-026). No audit event is added
  (SC-11 unchanged).

## Error Handling

No new failure path. A problem kind with no hint (X1, X6, T3, T4) renders as today. A translation key missing at
runtime is prevented by VR-003 tests.

## Non-Functional Requirements

- NFR-073 — both languages; the UI language of the viewer.
- NFR-075 — the page-search hint on «Стан даних» (FR-007).
- NFR-076 — phone width; no horizontal scroll.
- NFR-078 — period rule as in FR-008.
- The banner stays restrained (v99): colour allowed; no sound, popup or blinking.

## Out of Scope

- Changing or removing the hints already in place (X1, X6, T3, T4, the Dean's «Повідомте Адміністратора» and
  «Якщо з викладачем…»).
- An expandable, pop-up or scripted hint form.
- A diagnosis of the particular case.
- Any new data, right, endpoint, DTO field, schema change or audit event.
- A way to dismiss or mark a problem as read; an item deliberately deleted in Classroom stays counted (Owner's
  decision 2026-11-04: look again after two pilot weeks).
- Hiding or disabling the X5 confirmation form in read-only mode (I-4).

## Open Decisions

None open. OD-001 (the Dean's X4 title) is carried from the Story, resolved by the Owner on 2026-11-04: option a)
(FR-004.2). See `docs/decisions/US-075-open-decisions.md`.

## Interpretations for confirmation at HUMAN_SPEC_APPROVAL

- **I-1** Teaching hints sit directly under the kind's heading line, before its list (a long list would push a
  hint below it out of sight). *(FR-002)*
- **I-2** The X2a/X2b connection page link reuses the existing link text «Підключення до Google Workspace», placed
  after the hint. *(FR-003)*
- **I-3** «No hint on fixing» for archived courses is read as: none of the new hints; the existing texts in the
  archived section stay (the Story keeps hints already in place). *(FR-006)*
- **I-4** In read-only mode the X5 problem shows no hint: each of its sentences names something the mode blocks
  (restoring the account, the mark clearing by a synchronization, the confirmation). The alternative — a separate
  read-only text — would need the Owner's wording in both languages; the read-only notice on the same page already
  states the mode. The T1 hint stays in read-only mode: the teacher's correction is not blocked, and «після наступної
  синхронізації» names no time. The form and button stay as today — the Story's scope is the hints; submitting is
  already refused in read-only mode. *(FR-005.3, FR-005.4)*
- **I-5** The X4 hint is chosen by the viewer's role, not by details: X4 carries no details object even for the
  Admin today. *(FR-003.2)*
- **I-6** The v113 page-search hint test fix for «Стан даних» rides in this Story (FR-007), as agreed when v113
  was drafted; it is not in the Story text. *(FR-007)*
- **I-7** The T2 hint keeps the Story's agreed text «Завдання видалено або повернуто в чернетку…, оцінки в програмі
  збережено», although the kind also covers materials, which have no grades. The Specification does not change an
  agreed text; the Owner confirms it or gives a new wording at `HUMAN_SPEC_APPROVAL`. *(FR-008)*
- **I-8** When the X5 list holds archived courses (or only archived ones), the X5 hint is still shown once for the
  whole problem: the fix concerns the technical account, not changing a course, so v110 «Архивному курсу подсказка
  об исправлении не показывается» does not remove it. *(FR-006.2)*

## Traceability

| AC | Story | Requirement | FR / VR / SEC |
|---|---|---|---|
| AC-001 | AC-001, Scope 1 | v110 «баннер» | FR-001.2, FR-001.4, FR-001.5 |
| AC-002 | AC-002, Scope 1 | v110 «Часть с нулём» | FR-001.3 |
| AC-003 | AC-003, Scope 2 | v110 «подсказка … учебных» | FR-002, FR-008, FR-009 |
| AC-004 | AC-004, Scope 2 | v110 «у технических её видит Админ» | FR-003, FR-008 |
| AC-005 | AC-005, Scope 3, OD-001 | v110 «Декан … только подсказку» | FR-004, SEC-002 |
| AC-006 | AC-006, Scope 4 | v110 «Архивному курсу» | FR-006 |
| AC-007 | AC-007 | v110 «без скриптов»; BR-026 | FR-005.1…FR-005.4, FR-009.2, SEC-004 |
| AC-008 | AC-008 | v110 «новых данных и прав не добавляет»; v113 NFR-075 | FR-001.1, FR-007, FR-008, VR-001…VR-003, SEC-001, SEC-005 |

## Delegation

No lookups were delegated; the requirements sections, the page view, the banner, the data-health rules and the
translation files were read directly. Version 2: the review v1, the Story's AC-007, the page's read-only notice and
its translation texts were read directly.
