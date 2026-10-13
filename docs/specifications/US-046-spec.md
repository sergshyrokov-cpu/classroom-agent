---
artifact_type: specification
story: US-046
version: 1
status: APPROVED
created_at: 2026-10-13T17:42:15Z
updated_at: 2026-10-13T17:44:49Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-046-page-search-hint.md
    version: null
  - path: trebovaniya.md
    version: 91
  - path: docs/decisions/US-046-open-decisions.md
    version: 2
supersedes: null
---

# US-046 Specification — Page-search hint on teaching-data pages

## 1. Overview

This Story adds one line of small text directly under the page title of every
installation page that shows teaching data, telling the user how to search the
page with the browser — on a computer and on a phone (`trebovaniya.md` §5, v91,
"Подсказка поиска на странице"; NFR-075).

The rule in `trebovaniya.md` §5 (v91):

- **Where** — only on teaching-data pages: «список курсов, карточка курса,
  список преподавателей, журнал, отчёт по шаблону, отчёты Meet, коды встреч
  Meet, «База данных»». Not on sign-in, password and settings forms, the
  Admin's service pages (Dean accounts, report templates, the connection, the
  access check), the error page and the Control Plane.
- **How** — «Одна строка мелким неброским текстом вверху, сразу под заголовком
  страницы … Видна всегда, закрыть её нельзя, ничего не запоминается.
  Устройство не определяется … Текст — из файлов переводов, на языке
  пользователя.»
- **Text** — three variants (ordinary page; the course list, which has its own
  search field; the paged lists without a search field), quoted verbatim in
  FR-003. «Текст одинаков, сколько бы страниц ни было в списке.»

The change is presentation only: no endpoint, request parameter, status code,
use case, port, entity, table or audit event is added or changed.

Writing this Specification was done here; existing-code facts (the list of
installation views, how the page title is rendered, the course-list search
label key) were gathered by direct read-only lookups.

## 2. Business Goal

Many staff members do not know Ctrl+F, ⌘F or the phone browser's "Find in
page". Long journals and lists are hard to use without it. On paged lists the
browser sees only the rows shown, which silently hides a teacher or a Meet code
that sits on another page. The hint teaches the search, warns about paged lists,
and on the course list points to the list's own search field, which searches the
whole list.

## 3. Business Flow

1. A signed-in Dean or Admin opens a teaching-data page (§4 FR-001).
2. The page renders as today; directly under its title one line of small text
   shows the hint variant assigned to that page, in the user's interface
   language.
3. The user reads it and uses the browser search (or, on the course list, the
   list's search field). Nothing is stored; the next visit shows the same line.

Any other page (FR-005) renders exactly as today, with no hint.

## 4. Functional Requirements

**FR-001 Pages that show the hint.** The hint is shown on exactly these
installation pages, each with the variant given:

| Page | View today | Variant |
|---|---|---|
| Course list | `Courses/Index` | course list (FR-003 b) |
| Course card | `Courses/Card` | ordinary (FR-003 a) |
| Teacher list | `Teachers/Index` | paged list (FR-003 c) |
| Journal | `Journal/Index` | ordinary |
| Template report | `Report/Index` | ordinary |
| Meet reports | `MeetReports/Index` | ordinary |
| A single meeting's page | `MeetReports/Meeting` | ordinary |
| Meet codes | `MeetCodes/Index` | paged list |
| Database | `Database/Index` | ordinary |

Source: §5 v91 "Где" and "Текст — три вида"; a single meeting's page is part of
"отчёты Meet" (Story Notes, US-033). Traces to AC-001, AC-002, AC-003.

**FR-002 Placement and look.** The hint is one line of small, unobtrusive text,
placed directly under the page title, above every other content of the page
(messages, forms, tables). "The page title" is the title the shared layout
renders at the top of the page content. On the Meet codes page placement
follows OD-002 (§11). Traces to AC-001 … AC-003.

**FR-003 The three texts.** Verbatim from `trebovaniya.md` §5 (v91), in the
user's interface language:

a) Ordinary page:
- uk: «Пошук на сторінці: Ctrl+F (на Mac — ⌘F), на телефоні — меню браузера →
  «Знайти на сторінці».»
- en: «Search this page: Ctrl+F (⌘F on Mac); on a phone — browser menu →
  "Find in page".»

b) Course list:
- uk: «Курс шукайте в полі «Пошук за назвою або розділом» нижче — воно шукає в
  усьому списку; Ctrl+F (на Mac — ⌘F) знаходить лише показані рядки.»
- en: «Find a course in the "Search by name or section" field below — it
  searches the whole list; Ctrl+F (⌘F on Mac) finds only the rows shown.»

c) Paged list without a search field (teachers, Meet codes):
- uk: «Ctrl+F (на Mac — ⌘F; на телефоні — меню браузера → «Знайти на
  сторінці») шукає лише серед показаних рядків; решта — на наступних сторінках
  списку (кнопки внизу).»
- en: «Ctrl+F (⌘F on Mac; on a phone — browser menu → "Find in page") searches
  only the rows shown; the rest are on the next pages of the list (buttons
  below).»

The outer «» of each item above delimit the text and are not part of it; the
inner «» and "" are. Traces to AC-001, AC-002, AC-003.

**FR-004 The quoted field name equals the field label.** In variant b) the
quoted field name is the same text as the label of the course list's search
field in the same language — today the `Courses.Form.Search` translation entry
(uk «Пошук за назвою або розділом», en "Search by name or section"). Whether the
hint repeats the text or composes it from that entry is a design choice; the
equality is the requirement. Traces to AC-002.

**FR-005 Pages without the hint.** Every other page has no hint, in particular:
sign-in; the landing page; the Dean's own and forced password forms; the
Admin's service pages — Dean accounts, report templates (list, form, delete),
the workspace connection, the connection instructions, the access check; the
Meet code course-choice page (OD-001, resolved a); the error page, whichever
status it reports; every Control Plane page. Source: §5 v91 "Нет на страницах…".
Traces to AC-005.

**FR-006 Always shown, same text.** On an FR-001 page the hint is shown for
every user who can open the page (Admin and Dean), in every state of the page
the page itself renders: one page of rows or many, no rows, no data stored
yet, validation messages shown, read-only mode. Its text does not depend on the
number of pages, the role, the mode or the device. It has no close or hide
control. Source: §5 v91 "Видна всегда, закрыть её нельзя"; «Текст одинаков,
сколько бы страниц ни было». Traces to AC-004.

**FR-007 Stores nothing, detects nothing.** Showing the hint sets no cookie,
writes no setting, account field or database row, writes no audit event, and
reads no device or user-agent information; no script is added for it. Source:
§5 v91 "ничего не запоминается. Устройство не определяется". Traces to AC-004.

**FR-008 Texts from translation files.** The three texts are entries in the
installation's translation files (`Application.Localization`), in Ukrainian and
English; no view holds hint text as a literal. Source: §5 v91 "Текст — из файлов
переводов"; NFR-073. Traces to AC-006.

**FR-009 Nothing else changes.** No page title, form, filter, paging control,
table, message, route, query parameter, status code or authorization policy
changes. The only additions are the hint line, its translation entries and the
style that makes it small and unobtrusive. Traces to AC-007.

## 5. Acceptance Criteria

From the Story, unchanged:

- **AC-001** Ordinary teaching-data pages (course card, journal, template
  report, Meet reports, single meeting, database) show the ordinary hint
  directly under the page title, in the user's language. → FR-001, FR-002,
  FR-003 a.
- **AC-002** The course list shows the course-list hint; the quoted field name
  equals the search field's label in both languages. → FR-001, FR-002,
  FR-003 b, FR-004.
- **AC-003** The teacher list and the Meet codes page show the paged-list hint.
  → FR-001, FR-002, FR-003 c.
- **AC-004** The hint is always there with the same text — one page or many, no
  rows, read-only, Dean or Admin — has no close control and writes nothing. →
  FR-006, FR-007.
- **AC-005** No hint on any other page, including the Meet code course-choice
  page and the whole Control Plane. → FR-005.
- **AC-006** Texts come from the translation files in both languages; none is
  hard-coded in a view. → FR-008.
- **AC-007** The existing suite stays green; nothing else on the pages changes.
  → FR-009.

Derived (test obligations that make the Story's criteria checkable):

- **AC-008** Exactly one hint per FR-001 page, and it is the variant FR-001
  assigns — a page never shows two variants or two copies. → FR-001, FR-002.
- **AC-009** On each FR-001 page the hint precedes every other content element
  of the page body (messages, forms, tables, headings below the title). →
  FR-002.

## 6. Validation Rules

The Story adds no input: no request body, query or route parameter, form field
or uploaded file. Existing validation of the FR-001 pages is unchanged
(FR-009). **VR-001**: none to add — stated so that no design invents one.

## 7. Security Requirements

- **S-01 Authorization unchanged.** Every FR-001 page keeps its current policy
  (deny by default, SC-4); the hint is rendered only inside a page the user is
  already allowed to see. No new endpoint, no anonymous access.
- **S-02 No personal data.** The hint is static text from translation files; it
  contains no Google data, no user name or email and no query value.
- **S-03 Output encoding.** The hint is rendered with the view engine's normal
  HTML encoding; the quotation marks in the texts (« » " ") are displayed as
  text, never as markup. No `Html.Raw` for the hint.
- **S-04 No storage, no outbound data.** Per FR-007 nothing is written and no
  request leaves the installation (SC-13 unchanged); no cookie (SC per
  `security-conventions.md` cookie rules unchanged).
- **S-05 Read-only mode** is not involved: the hint is not an action, so AD-6 /
  BR-026 need no new rule (§5 v91; Story scope).

## 8. Error Handling

No new failure path. A request that today ends on the error page (unknown
course, failed query, 404, 500) still shows the error page, which has no hint
(FR-005). A page that today renders validation messages instead of data still
renders as the FR-001 page and shows the hint (FR-006).

## 9. Non-Functional Requirements

- **NFR-075** — the whole of this Story.
- **NFR-073** — Ukrainian and English, the user's chosen language.
- Responsive: the hint wraps on a narrow (phone) screen like other text; no
  horizontal scrolling is introduced.
- No measurable cost: no extra query, no extra round trip per page.

## 10. Out of Scope

- A search field on the teacher list or the Meet codes page.
- Any change to paging, filters, page titles or other page content (FR-009) —
  including the duplicated title on the Meet codes pages (see OD-002).
- Remembering that a user has seen the hint, or hiding it.
- Device detection or device-specific wording ("Find on Page" in Safari is
  covered by the one shared name, Story Notes).
- The Control Plane (FR-005).

## 11. Open Decisions

Full text in `docs/decisions/US-046-open-decisions.md`.

- **OD-001 The Meet code course-choice page — resolved (a)** by the Owner on
  2026-10-13: no hint. Impact: FR-005, AC-005.
- **OD-002 Where the hint goes on the Meet codes page — resolved (a)** by
  the Owner at HUMAN_SPEC_APPROVAL on 2026-10-13: directly under the page's own
  (second) title. The shared layout renders the page title at the top of every
  page, and the Meet codes page (`MeetCodes/Index`) renders the same title a
  second time as its own heading (the course-choice page does too, but has no
  hint). "Directly under the page title" can therefore mean two places, and
  FR-009 forbids changing titles. Options:
  - **a) (Recommended)** Under the page's own second title — the hint sits
    directly above the page content, as on every other page; titles untouched.
  - b) Under the layout title, as on every other page — the result reads
    title / hint / the same title again.
  - c) Remove the duplicate title from the Meet codes page and place the hint as
    on every other page — a title change outside the Story's scope; needs the
    Owner's explicit approval, and the course-choice page would then differ.
  Impact: FR-002, AC-003, AC-009 for the Meet codes page only.

`trebovaniya.md` §7 (v91) has no open item that this Story depends on.

Interpretations for the gate:

- **I-1** "Page title" = the title the shared layout renders (FR-002), except as
  OD-002 settles for Meet codes.
- **I-2** A single meeting's page counts as "отчёты Meet" (Story Notes) and gets
  the ordinary variant.
- **I-3** "Small, unobtrusive" is met by a style class on the hint line in the
  installation stylesheet; no exact size is specified.
- **I-4** The hint appears in every state an FR-001 page renders, including
  "no data yet" and validation messages; the error page is not an FR-001 page.
- **I-5** The ⌘ character is part of the translation text as written in §5; no
  icon or image.

## 12. Traceability

| AC | Functional requirements | Other |
|---|---|---|
| AC-001 | FR-001, FR-002, FR-003 a | I-1, I-2 |
| AC-002 | FR-001, FR-002, FR-003 b, FR-004 | — |
| AC-003 | FR-001, FR-002, FR-003 c | OD-002 |
| AC-004 | FR-006, FR-007 | S-04, S-05, I-4 |
| AC-005 | FR-005 | OD-001 |
| AC-006 | FR-008 | S-03 |
| AC-007 | FR-009 | VR-001, S-01 |
| AC-008 | FR-001, FR-002 | — |
| AC-009 | FR-002 | OD-002 |
