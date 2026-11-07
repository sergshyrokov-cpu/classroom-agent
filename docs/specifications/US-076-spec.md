---
artifact_type: specification
story: US-076
version: 3
status: DRAFT
created_at: 2026-11-07T06:34:24Z
updated_at: 2026-11-07T10:54:20Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-076-page-help.md
    version: null
  - path: trebovaniya.md
    version: 112
  - path: docs/decisions/US-076-open-decisions.md
    version: 1
supersedes: docs/specifications/US-076-spec.md@v2
---

# US-076 Specification: a «?» beside the title opens short help on five pages

Grounded in `trebovaniya.md` v112: section 5 «Справка на странице» (v111, v112) and «Подсказка о поиске»
(v91, v108); section 2 permission matrix; section 4 Epic 3 (journal cells, «Выбывший ученик» v101, course
marks v100/v102, template report, export), Epic 4 (Meet linking, «не курс» v88, «Кого считать», «Состав
курса — на дату встречи», delay v47); section 3 `CourseMembership`. Non-functional requirements NFR-070,
NFR-073, NFR-075, NFR-076, NFR-078, NFR-079; SC-4, SC-13, SC-15. Business rules behind the texts:
BR-045, BR-048, BR-049, BR-060, BR-061, BR-064, BR-065, BR-068, BR-083 (and the mirror rule of
`trebovaniya.md` section 3, v83).

Section 7 has no open item this Story depends on. Item 26 («Документация для школы и для Владельца»)
asks, among other things, whether help lives inside the program and how it is kept current; v111/v112
already decided the narrow case of this Story (five pages, the texts approved at specification approval,
«если текст и программа расходятся, неверен текст»), so item 26 is not an Open Decision here and stays open
for the wider documentation.

Delegated: a read-only lookup of the existing code (layout title, page-search hint partial, the five views
and their policies, localization files, CSP, site stylesheet, the page-search hint tests and the two Meet
code tests of Scope item 5) went to an Explore subagent; its facts are cited below with paths. The texts,
requirements and interpretations were written here.

**Version 2** (loop-back from `SPEC_REVIEW`, review v1): journal line 2 rewritten after the marks the
journal really shows (F-1; facts read directly from `Web/Security/DataMarksTexts.cs`,
`Views/Journal/Index.cshtml` and both resx files); I-1 corrected; Meet codes line 2 softened (m-1); Meet
reports and meeting lines 3 tightened to the real labels and BR-064 (m-2); digest rewritten from this
version and now names I-7 (m-3); finding m-1 of section 14 reworded.

**Version 3** (loop-back from `IMPLEMENTATION`, finding US-076/IMPLEMENTATION/B-1; Owner's decision
2026-11-07, option 1): AC-006 and section 9 now allow an existing test of the five pages to read the page
text outside the help block, because the approved help lines name labels those tests look for in the whole
page (Interpretation I-8). The help texts of section 4.1 and every other part of version 2 are unchanged.

---

## 1. Overview

Five installation pages get a small «?» control beside the page title. Pressing it expands two to four
short lines of help under the title; pressing again collapses it. The help explains what the page is for,
where its data comes from and what to do if something looks wrong. It is the same for Admin and Dean, in
normal and read-only mode, in Ukrainian and English, comes from the translation files, stores nothing and
sends nothing. No other page and no Control Plane page has help. The Story also tightens one test of
US-074 (Scope item 5).

The five pages (Explore lookup):

| Page id | Page | View | Route constant | Policy |
|---|---|---|---|---|
| `journal` | Журнал / Journal | `Views/Journal/Index.cshtml` | `SignInRoutes.Journal` (`/workspace/journal`) | `ViewJournal` |
| `meet-codes` | Зустрічі Meet / Meet meetings | `Views/MeetCodes/Index.cshtml` | `SignInRoutes.MeetCodes` | `ViewMeetCodes` |
| `meet-reports` | Звіти Meet / Meet reports | `Views/MeetReports/Index.cshtml` | `SignInRoutes.MeetReports` | `ViewMeetReports` |
| `meeting` | Зустріч Meet / Meet meeting | `Views/MeetReports/Meeting.cshtml` | `SignInRoutes.MeetReportMeeting` | `ViewMeetReports` |
| `report-templates` | Шаблони звітів / Report templates | `Views/ReportTemplates/Index.cshtml` | `SignInRoutes.ReportTemplates` | `UseReportTemplates` |

All five policies admit Admin and Dean (`trebovaniya.md` section 2: viewing the journal, Meet statistics,
Meet code linking and using report templates are ✔/✔).

## 2. Business Goal

Deans work at school and at home, often on a phone, with nobody to ask (`trebovaniya.md` section 5,
«Справка на странице»). The four kinds of pages above raise the most questions; a few lines on the page
answer the first question without a call to the Owner. Finding information easily is a main product goal
(v107): the help says where the data comes from and where to look when something is wrong.

## 3. Business Flow

1. An Admin or Dean opens one of the five pages. The «?» stands beside the title; the help is collapsed.
2. They press «?» (mouse, touch or keyboard). The help lines appear under the title, above the page-search
   hint (where the page has one) and the rest of the page.
3. They press «?» again: the help collapses. Opening, reloading or switching the language shows the page
   with the help collapsed.

Nothing in this flow reaches the server beyond the ordinary page request.

## 4. Functional Requirements

**FR-001 Where.** The help control and text are rendered on exactly the five pages of section 1, for every
outcome of those pages that renders the page view itself (including empty states: no course chosen, no
rows, no codes). They are rendered on no other installation page — including the sub-pages
`MeetCodes/CourseChoice`, the template new/copy/change form and the template delete page (Story OD-001,
resolved) — on no error page, and on no Control Plane page. *(NFR-079; section 5 «Где»)*

**FR-002 The control.** The control stands beside the page title on the title's line: the visible glyph
«?» and an accessible name from the translation files (FR-006). It is a native disclosure — an HTML
`<details>` element whose `<summary>` is the control — so it needs no script, opens and closes by mouse,
touch, Enter and Space, and its open state is announced by assistive technology. On a narrow screen the
control may wrap below a long title; it never makes the page scroll sideways. *(Story AC-001, AC-002;
NFR-070; Story Notes hint on `<details>`)*

**FR-003 Collapsed on load, nothing stored.** The `<details>` element is rendered without the `open`
attribute on every response. No cookie, no browser storage, no query parameter and no server-side state
records whether the help was opened; no script is added for it. *(NFR-079; Story AC-003)*

**FR-004 Placement.** The title line is the layout's `<h1>` (`Views/Shared/_Layout.cshtml:26`, rendering
`ViewData["Title"]`). On the five pages the layout renders the title and the control together as one title
block, and the expanded text appears directly under the title, before the page-search hint and everything
the view renders. On the other pages the title renders exactly as today. The page-search hint's own text,
markup and place are unchanged; it remains the first element after the title block (NFR-075). *(Interpretation I-2)*

Mechanism (binding for design and implementation, chosen so the help needs no script and no CSP change):
each of the five views sets one view-data entry naming its page id (section 1); the layout reads it and,
when it is present and is one of the five ids, renders a shared partial with the title block and the
`<details>`. A missing or unknown id renders the plain title. The page id selects translation keys only; no
help text and no key is computed from request input.

**FR-005 Text.** Each page shows the lines of section 4.1 for its page id, in that order, each line its
own paragraph inside the expanded part. The lines are ordinary sentences and keep their final periods
(NFR-078 binds one-line texts only; NFR-079). Data from Google and school text are never part of the help;
the help has no placeholders. *(NFR-079; Story AC-004)*

**FR-006 Translation files.** Every help line and the control's accessible name come from
`SharedResource.uk.resx` and `SharedResource.en.resx` (`src/ClassroomAgent.Application/Localization/`),
under keys `PageHelp.<Page>.Line<n>` and `PageHelp.Control.Name`, shown in the user's language
(NFR-073). No help text is written in a view, partial, stylesheet or C# code. Key names are fixed in
API design; the values are exactly those of section 4.1 as approved at `HUMAN_SPEC_APPROVAL`.

**FR-007 Look.** The control is easy to tap on a phone: a touch target at least 44×44 CSS pixels, with a
visible keyboard focus. The expanded text is in the page's ordinary or slightly smaller font, readable on
a phone, wraps within the page width, and is restrained — no colour alert, no border box that looks like a
warning, no animation (NFR-076, `feedback` on restrained tone). Styling is added to the existing site
stylesheet `wwwroot/css/site.css`; no inline style, no new external file, no Bootstrap script.
*(NFR-070, NFR-076; Story AC-007)*

**FR-008 Same for everyone, changes nothing.** Admin and Dean see the same help; read-only mode shows the
same help. The help is a part of the page response: it adds no endpoint, no form, no link, no request
when opened, no audit event, no log line, and no write. What the five pages show and do, their forms,
links, policies, read-only behaviour and exports stay as they are. *(NFR-079; Story AC-006)*

**FR-009 Without scripts.** Because the disclosure is native HTML, the help is reachable without scripts
exactly as with them; the collapsed help hides no page content. *(Story AC-006: the Specification decides
"reachable")*

**FR-010 Carried test fix (Scope item 5).** The test
`MeetCodeOrganizerNamePageTests.SeveralOrganizers_AreOrdered_AndEachShowsOnce` additionally asserts the
rendered markup between organizers, as
`MeetCodesSearchPageTests.LinkedAndNotACourse_ShowTheOrganizers_RightAfterTheCode` does
(`"</span>, <span"` occurs in the cell, i.e. no white space before a comma). Its existing assertions stay.
Test-only; production code of the Meet code page is not changed for this. It closes the deferred finding
US-074/IMPLEMENTATION/m-1.

### 4.1 The help texts (for the Owner's approval)

Each line names the BR- rule or section it restates, so `SPEC_REVIEW` can check that no line adds a rule.
Labels in «» are the existing UI labels from the translation files.

**Журнал / Journal** (`journal`)

| # | Українська | English | Source |
|---|---|---|---|
| 1 | Журнал показує оцінки й стан здачі завдань курсу за обраний період так, як їх повернув Google Classroom. Дані оновлює синхронізація, на цій сторінці їх не змінюють. | The journal shows the grades and submission states of a course's coursework for the chosen period, as Google Classroom returned them. Synchronization refreshes the data; it is not edited on this page. | Epic 3 «Клетка журнала»; section 3 mirror (v83) |
| 2 | «Вибув(-ла)» з датою біля імені означає, що учня зараз немає у списку курсу в Classroom; дата — коли синхронізація бачила його востаннє. Позначка «немає в Classroom» під назвою завдання стосується лише цього завдання, а рядок над таблицею «Курсу немає в Classroom з …» або «Дані не оновлювалися з …» — усього курсу. | "Left" with a date beside a name means the student is not on the course's list in Classroom now; the date is when synchronization last saw them. The mark "not in Classroom" under a coursework title concerns only that item, while a line above the table, "Course not in Classroom since …" or "Data not updated since …", concerns the whole course. | BR-068; BR-045; BR-048, BR-046, BR-049 |
| 3 | Вивантаження в Excel робиться на сторінці звіту за шаблоном («Шаблони звітів» → «Звіт»); позначка «вибув(-ла)» там стоїть у примітці до клітинки з ім'ям. | The Excel export is made on the template report page ("Report templates" → "Report"); there the "left" mark is a note on the name cell. | Epic 3 «Шаблоны и отчёт на экране» (v84), «Выбывший ученик» (v101) |
| 4 | Якщо оцінка неправильна, її виправляє викладач у Classroom, і наступна синхронізація покаже нову. Що означає кожна проблема даних і хто її вирішує, видно на сторінці «Стан даних». | If a grade is wrong, the teacher corrects it in Classroom, and the next synchronization shows the new one. What each data problem means and who solves it is on the "Data health" page. | Epic 3 «Когда сформирован» (v103); Epic 1 «Стан даних» (v99, v110) |

**Зустрічі Meet / Meet meetings** (`meet-codes`)

| # | Українська | English | Source |
|---|---|---|---|
| 1 | Тут коди зустрічей Meet із даних Google. Classroom не повідомляє, до якого курсу належить зустріч, тому код прив'язують до курсу, і тоді його зустрічі входять у звіти Meet цього курсу. | These are the Meet meeting codes from Google's data. Classroom does not tell which course a meeting belongs to, so a code is linked to a course, and then its meetings count in that course's Meet reports. | BR-065; Epic 4 «Привязка встречи к курсу» |
| 2 | Програма прив'язує код сама, коли організатор — викладач курсу, а учасники здебільшого збігаються зі списком учнів; інакше код чекає в «Непризначені», і курс обирає Декан або Адмін. | The program links a code itself when the organizer is a teacher of the course and the participants mostly match its list of students; otherwise the code waits under "Unassigned", and a Dean or Admin picks the course. | BR-065 |
| 3 | Зустрічі коду без прив'язки не входять у звіти жодного курсу. Код педради, наради чи консультації для кількох груп позначають «не курс». | Meetings of a code with no link count in no course's reports. A code of a staff meeting or a consultation for several groups is marked "not a course". | BR-065; BR-083 |
| 4 | Помилкову прив'язку виправляють кнопкою «Прив'язати до іншого курсу»: усі зустрічі коду перейдуть у вибраний курс. У режимі лише для читання прив'язки змінювати не можна. | A wrong link is corrected with "Link to another course": all the code's meetings move to the chosen course. In read-only mode links cannot be changed. | BR-065; BR-026 |

**Звіти Meet / Meet reports** (`meet-reports`)

| # | Українська | English | Source |
|---|---|---|---|
| 1 | Звіти показують, як фактично проходять зустрічі Meet курсу за період. Це не журнал відвідування: заняття могло пройти в аудиторії або з матеріалами в Classroom. | The reports show how a course's Meet meetings actually went over the period. This is not an attendance register: a lesson may have been held in class or with materials in Classroom. | BR-060 |
| 2 | Присутність — це підключення до зустрічі з особистого акаунта домену, який програма знає з Classroom, і хвилини підключення. Дані надходять від Google із затримкою до 24 годин і оновлюються синхронізацією. | Presence is a connection to the meeting from a personal domain account the program knows from Classroom, and the minutes connected. The data arrives from Google with a delay of up to 24 hours and is refreshed by synchronization. | BR-064; BR-061; Epic 4 report 3 |
| 3 | Людини може не бути серед учнів курсу, якщо на дату зустрічі її не було у списку курсу (тоді вона в стовпці «Не в списку курсу»), якщо вона підключилася з акаунта, якого програма не знає з Classroom (тоді вона серед «Інші учасники»), або якщо код зустрічі не прив'язаний до цього курсу — це видно на сторінці «Зустрічі Meet». | A person may be missing from the course's students if they were not on the course list on the meeting's date (then they are in the "Not on the course list" column), if they connected from an account the program does not know from Classroom (then they are among "Other participants"), or if the meeting code is not linked to this course — the "Meet meetings" page shows that. | BR-064; Epic 4 «Состав курса — на дату встречи»; BR-065 |

**Зустріч Meet / Meet meeting** (`meeting`)

| # | Українська | English | Source |
|---|---|---|---|
| 1 | Тут одна зустріч Meet: хто підключився, скільки хвилин був і ким був для курсу на дату зустрічі. | This is one Meet meeting: who connected, how many minutes they stayed and who they were for the course on the meeting's date. | Epic 4 report 4; BR-064 (v58 marks) |
| 2 | Присутність — це підключення з особистого акаунта домену, який програма знає з Classroom. Дані надходять від Google із затримкою до 24 годин і оновлюються синхронізацією. | Presence is a connection from a personal domain account the program knows from Classroom. The data arrives from Google with a delay of up to 24 hours and is refreshed by synchronization. | BR-064; BR-061 |
| 3 | Якщо учня немає серед учнів курсу, можливо, на дату зустрічі його не було у списку курсу (тоді біля нього позначка «не в списку курсу») або він підключився з акаунта, якого програма не знає з Classroom (тоді він серед «Інші учасники»). Це не журнал відвідування: заняття могло пройти й без Meet. | If a student is not among the course's students, they may not have been on the course list on the meeting's date (then they are marked "not on the course list") or connected from an account the program does not know from Classroom (then they are among "Other participants"). This is not an attendance register: a lesson may have been held without Meet. | BR-064; Epic 4 «Состав курса»; BR-060 |

**Шаблони звітів / Report templates** (`report-templates`)

| # | Українська | English | Source |
|---|---|---|---|
| 1 | Шаблон задає, як із даних курсу виходить звіт: вигляд, шкала оцінювання, позначки станів клітинки і звідки брати імена. | A template sets how a report is made from a course's data: the view, the grading scale, the marks of the cell states and where names are taken from. | Epic 3 «Шаблоны и отчёт на экране», «Перевод в шкалу школы» (v84), «Имена» (v86); section 3 `ReportTemplate` |
| 2 | Щоб отримати звіт, натисніть «Звіт» біля шаблону й оберіть курс і період; там же є «Експорт в Excel». | To get a report, press "Report" beside a template and choose the course and period; "Export to Excel" is there too. | Epic 3 (v84) |
| 3 | Оцінки у звіті — з даних Google, оновлених синхронізацією; шаблон лише перераховує їх у шкалу школи. Вбудований «Академічний журнал» не змінюють: щоб налаштувати свій, натисніть «Копіювати». | The grades in a report come from Google's data refreshed by synchronization; a template only converts them to the school's scale. The built-in "Academic journal" is not changed: to set up your own, press "Copy". | Epic 3 (v84), «Встроенный шаблон» |

The English labels in the English lines ("Unassigned", "Link to another course", "Other participants",
"Not on the course list", "not on the course list", "not in Classroom", "Course not in Classroom since …",
"Data not updated since …", "left", "Report", "Copy", "Export to Excel", "Data health", "Report templates", "Meet
meetings", "Academic journal") must equal the current English values of the same UI keys; where API design
finds a different current value, the line uses the current value and the difference is reported, not
silently chosen. The same holds for the Ukrainian labels.

Control accessible name: «Довідка про сторінку» / "Help about this page".

## 5. Acceptance Criteria

Each Story AC is kept with its id and refined into checkable statements.

**AC-001 The control is on the five pages.** For Admin and Dean, in `uk` and `en`, in normal and
read-only mode, each of the five pages renders exactly one help `<details>` in the title block beside the
`<h1>`, without the `open` attribute, whose `<summary>` shows «?» and carries the accessible name of FR-006
in the user's language. *(FR-001, FR-002, FR-003, FR-006, FR-008)*

**AC-002 Pressing expands and collapses.** The control is a native `<details>`/`<summary>`, so mouse,
touch, Enter and Space open and close it with no pop-up; the expanded text is inside the `<details>`
directly under the title, before the page-search hint (where present) and the view's content; the
page-search hint markup and text are unchanged and remain the first element after the title block.
*(FR-002, FR-004)*

**AC-003 Collapsed again on every page load.** Every response of the five pages renders the help without
`open`, including after a language change; the response sets no new cookie because of the help, the pages
add no script for it, and no request parameter or server state about the help exists. *(FR-003)*

**AC-004 The text follows the scheme, in both languages.** Each page shows exactly the lines of section
4.1 for its page id, in order, in the user's language, from the translation files; no help text appears in
any `.cshtml`, `.cs` or `.css` file. *(FR-005, FR-006)*

**AC-005 No help elsewhere.** No other installation page reachable by Admin, Dean or anonymously —
including `MeetCodes/CourseChoice`, the template forms and delete page, the course list and card, the
teacher list, the template report, the database page, the data-health page, the landing page, the
settings and account pages, sign-in, the forced password change and the error pages — and no Control Plane
page renders a help control or help text. *(FR-001)*

**AC-006 Nothing else changes.** On the five pages the content, forms, links, policies, read-only
behaviour and exports are unchanged (the existing tests of these pages stay green without edits other than
the title-block position the page-search hint tests assert and, where a test searches the whole page text
for a phrase the help also contains, reading the page text outside the help block — Interpretation I-8; the
assertion itself is not weakened); opening the help sends no request; the CSP and
other security headers are unchanged (SC-15); without scripts the help is reachable and hides no content.
*(FR-008, FR-009)*

**AC-007 A person checks the look.** On a computer and on a phone, in both languages, the Owner opens and
closes the help on each of the five pages: the control is easy to tap, the text is easy to read, nothing
pushes the page sideways, the look is restrained. The walk is recorded in the implementation report.
*(FR-002, FR-007)*

The carried test fix (FR-010) has no AC; the `ac_test_matrix` lists it as a supporting change.

## 6. Validation Rules

The Story adds no input: no form, no query or route parameter, no request body.

- **VR-001** The page id of FR-004 is set by the view in code, never from request input. The layout
  accepts only the five ids of section 1; any other value renders the plain title (no help, no error).
- **VR-002** Help lines and the accessible name are HTML-encoded by Razor like every translated string;
  no line is rendered as raw HTML.

## 7. Security Requirements

- **Authorization unchanged (SC-3, SC-4).** The help is part of five already-protected pages; it adds no
  endpoint and changes no policy. It is never rendered on an anonymous page (FR-001).
- **Read-only mode (AD-6, BR-026).** The help writes nothing, so read-only mode shows it as is; nothing in
  Application changes.
- **No data leaves (SC-13).** The help sends no request and loads nothing from another site.
- **Headers not loosened (SC-15).** No script, no inline style, no inline event handler; CSP
  `script-src 'self'; style-src 'self'` (`Web/Security/SecurityHeadersMiddleware.cs:25-28`) stays as it is.
- **No personal data (SC-10, BR-071).** The help holds no Google data and no school text; it writes no log
  line and no audit event (not an SC-11 action).
- **Nothing stored client-side (NFR-072).** No cookie, no browser storage (FR-003).

## 8. Error Handling

The help has no failure of its own. A missing translation key would show the key name, as for every
translated string; tests (AC-004) prove every key exists in both files. Error pages, 400/404 outcomes that
render the error page, and any page outside section 1 show no help (FR-001). An outcome of one of the five
pages that renders its own view (for example the journal with no course chosen, or a validation message on
the same view) shows the help as usual.

## 9. Non-Functional Requirements

- NFR-070 phone use; NFR-076 restrained Bootstrap-based look, CSS in `site.css`, no inline style.
- NFR-073 both languages from translation files; NFR-078 does not bind these multi-sentence lines.
- NFR-075 the page-search hint stays one line under the title block, unchanged.
- Test conventions TC-2 (real PostgreSQL), TC-5 (both roles; read-only mode); page tests read rendered
  pages of both roles and both languages, as `tests/.../Web/Pages/PageSearchHintTests.cs` does.
- An existing page test that looks for a phrase in the whole page text and is broken only because a help
  line names the same label reads the text outside the help block instead (AC-006, I-8); finding every such
  test is the work of `TEST_WRITING`.

## 10. Out of Scope

- Help on any other installation page or anywhere in the Control Plane (NFR-079).
- Any change to what the five pages show or do; any new rule (section 5: «если текст и программа
  расходятся, неверен текст»).
- Remembering whether a person opened the help.
- Links inside the help text: none is needed — the texts name pages by their menu labels (Interpretation
  I-5).
- The wider school documentation of `trebovaniya.md` section 7 item 26.
- Any change to the Meet code page's production code (Scope item 5 is a test change).

## 11. Open Decisions

None open. The Story's OD-001 (sub-pages) was resolved by the Owner on 2026-11-06 as option (b) and is
carried in `docs/decisions/US-076-open-decisions.md`. The exact texts of section 4.1 and the
interpretations of section 13 are for the Owner's approval at `HUMAN_SPEC_APPROVAL`, not Open Decisions.

## 12. Traceability

| AC | Functional requirements | Validation | Requirement source |
|---|---|---|---|
| AC-001 | FR-001, FR-002, FR-003, FR-006, FR-008 | VR-001 | NFR-079; section 5 «Где», «Как» |
| AC-002 | FR-002, FR-004 | — | section 5 «Как»; NFR-075 |
| AC-003 | FR-003 | — | NFR-079 |
| AC-004 | FR-005, FR-006 | VR-002 | section 5 «Что в ней»; NFR-073, NFR-078 |
| AC-005 | FR-001 | VR-001 | NFR-079; Story OD-001 |
| AC-006 | FR-008, FR-009 | — | NFR-079; SC-15 |
| AC-007 | FR-002, FR-007 | — | NFR-070, NFR-076 |
| — (supporting) | FR-010 | — | US-074/IMPLEMENTATION/m-1 |

## 13. Interpretations (for confirmation at HUMAN_SPEC_APPROVAL)

- **I-1 The journal's marks as the journal shows them.** Section 5 lists «выбыл», «не виден» as
  *student* marks of the journal. In the requirements and the program the only student mark is
  «вибув(-ла)» (BR-068, `DataMarks.Left.On`); «не виден техническому аккаунту» marks a *course* and is shown
  only in the course list and course card (BR-049, `DataMarks.Course.NotVisibleSince`) — the journal of such
  a course shows «Дані не оновлювалися з …» above the table instead (BR-049, BR-046,
  `DataMarksTexts.Notice`). So journal line 2 names what the journal really shows: «вибув(-ла)» beside a
  student, «немає в Classroom» under one coursework title (BR-045, `DataMarks.Item.NotInClassroom`), and a
  course line above the table, «Курсу немає в Classroom з …» (BR-048) or «Дані не оновлювалися з …». The
  section 5 wording is a small inaccuracy for the next `trebovaniya.md` version (finding m-1).
- **I-2 The text opens above the page-search hint.** Section 5 places the help «под заголовком», NFR-075
  places the search hint «directly under the page title». The control sits on the title line, so while
  collapsed the hint is still directly under the title; when opened, the help text sits between the title
  and the hint, where the person pressed. Alternative: open below the hint — the text then appears one line
  further from the control.
- **I-3 The journal page has no export.** The Excel export is on the template report page
  (`Views/Report/Index.cshtml`; the journal view has no export form — Explore lookup), so journal line 3
  says where the export is made instead of describing an export on the journal itself.
- **I-4 «Lines» are sentences.** «Две–четыре строки» is read as two to four short lines of text, each one
  paragraph of one or two sentences; on a phone a line wraps. Journal and Meet codes have four lines, the
  other three pages three.
- **I-5 No links in the help.** The Story allows a link only if the Specification shows one is needed.
  The texts name the «Шаблони звітів», «Зустрічі Meet» and «Стан даних» pages by their menu labels, which
  are reachable from the header; a link would add a navigation element to the help and is not needed.
- **I-6 Same idea, two Meet texts.** The meeting list and the one-meeting page share the meaning of
  section 5 (presence, why a person is missing) but have their own wording: the one-meeting page omits
  «code not linked» (a meeting reached from a course report is already in that course) and adds what the
  page lists.
- **I-7 Template report export named on the template list.** Template line 2 names «Експорт в Excel» on
  the report page, since the list itself has no export.
- **I-8 Existing tests read the page outside the help** (Owner's decision 2026-11-07, finding
  US-076/IMPLEMENTATION/B-1, option 1). The approved help lines name labels the page itself shows — journal
  line 2 names «Курсу немає в Classroom з …» and «Дані не оновлювалися з …» — and an existing test such as
  `JournalMarksPageTests.AssertHeaderLine` checks that the whole page holds such a line once and no other.
  The page is right; the test's scope no longer fits. Such a test may read the page text without the help
  block (the help's `<details>`); what it asserts about the page stays the same. Alternative not taken:
  rewording the approved help so it names no label — it would explain the marks less clearly.

## 14. Findings for later

- **m-1 (for the next `trebovaniya.md` version).** Section 5 «Справка на странице» calls «не виден» a
  student mark of the journal, although it is a course mark that the journal does not show (see I-1), and
  its «Проверка» bullet still says «на этих четырёх страницах» although v112 made them five. Neither blocks
  this Story: NFR-079 says five, and the texts follow BR-045, BR-049 and BR-068.
- **m-2 (for TEST_WRITING).** `DataHealth/Index.cshtml:15` renders the page-search hint, but the data-health
  page is in neither `PageSearchHintTests.TeachingPageNames` nor its "no other page" list (Explore lookup;
  NFR-075 does not list the data-health page among pages with the hint). Not this Story's scope to change;
  AC-005's "no help" list includes the data-health page so that page is at least covered for help.
