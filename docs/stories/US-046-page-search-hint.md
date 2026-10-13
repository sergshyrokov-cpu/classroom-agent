---
id: US-046
epic: EPIC-6
title: Page-search hint on teaching-data pages
slug: page-search-hint
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v91.
---

# User Story

As a **Dean** or **Admin** who does not know that a browser can search the text
of a page

I want a short line at the top of every page with teaching data telling me how
to find a name or a title on it — on a computer and on a phone

So that I find a student, a course or a teacher in a long journal or list
without scrolling through it, and do not miss rows that sit on the next page of
a list.

---

# Business Value

`trebovaniya.md` §5 (v91), NFR-075: many staff members are not advanced users
of their devices and do not know Ctrl+F, ⌘F or the phone browser's
"Find in page". Long journals and lists are hard to read without it. On paged
lists browser search sees only the rows shown, which silently hides a teacher or
a Meet code that is on another page — the hint warns about that, and on the
course list points to the list's own search field, which searches everything.

---

# Scope

**In scope:**

- one line of small, unobtrusive text directly under the page title, always
  visible, not dismissible, nothing stored, no device detection;
- three text variants from translation files (Ukrainian and English, the
  user's language), worded exactly as in `trebovaniya.md` §5 (v91):
  - **ordinary page** — the course card, the journal, the template report, the
    Meet reports page and a single meeting's page, the database page;
  - **course list** — points to the list's search field by that field's label
    («Пошук за назвою або розділом» / "Search by name or section") and says that
    Ctrl+F finds only the rows shown;
  - **paged list without a search field** — the teacher list and the Meet codes
    page: browser search finds only the rows shown, the rest are on the next
    pages of the list;
- the same text whatever the number of pages and whatever the mode (read-only
  included — the hint is not an action);
- tests that the hint, with the right variant, is on every listed page in both
  languages, and is absent from every other page of the installation.

**Out of scope:**

- the sign-in page, password forms, the landing page, the Admin's service pages
  (Dean accounts, report templates and their forms, the workspace connection,
  the connection instructions, the access check), the error page and the whole
  Control Plane — no hint (`trebovaniya.md` §5, v91);
- a search field on the teacher list or the Meet codes page;
- any change to paging, filters, page titles or page content other than the
  hint line;
- remembering that a user has seen the hint, or hiding it.

---

# Acceptance Criteria

## AC-001 Ordinary teaching-data pages show the ordinary hint

**Given** a signed-in Dean or Admin

**When** they open the course card, the journal, the template report, the Meet
reports page or a single meeting's page, or the database page

**Then** directly under the page title there is one line of small text with the
ordinary hint, in the user's language:

- uk: «Пошук на сторінці: Ctrl+F (на Mac — ⌘F), на телефоні — меню браузера →
  «Знайти на сторінці».»
- en: «Search this page: Ctrl+F (⌘F on Mac); on a phone — browser menu →
  "Find in page".»

## AC-002 The course list points to its own search field

**Given** a signed-in Dean or Admin

**When** they open the course list

**Then** directly under the page title the hint reads, in the user's language:

- uk: «Курс шукайте в полі «Пошук за назвою або розділом» нижче — воно шукає в
  усьому списку; Ctrl+F (на Mac — ⌘F) знаходить лише показані рядки.»
- en: «Find a course in the "Search by name or section" field below — it
  searches the whole list; Ctrl+F (⌘F on Mac) finds only the rows shown.»

**And** the field name quoted in the hint is the same text as the label of the
search field on that page, in both languages.

## AC-003 Paged lists without a search field warn about the rows shown

**Given** a signed-in Dean or Admin

**When** they open the teacher list or the Meet codes page

**Then** directly under the page title the hint reads, in the user's language:

- uk: «Ctrl+F (на Mac — ⌘F; на телефоні — меню браузера → «Знайти на
  сторінці») шукає лише серед показаних рядків; решта — на наступних сторінках
  списку (кнопки внизу).»
- en: «Ctrl+F (⌘F on Mac; on a phone — browser menu → "Find in page") searches
  only the rows shown; the rest are on the next pages of the list (buttons
  below).»

## AC-004 The hint is always there and stores nothing

**Given** any page from AC-001 … AC-003

**When** it is shown — with one page of rows or many, with no rows, in
read-only mode, as a Dean or as an Admin

**Then** the hint is shown with the same text, has no close control, and showing
it writes nothing (no cookie, no stored setting, no audit event).

## AC-005 No hint anywhere else

**Given** the installation and the Control Plane

**When** any other page is shown — sign-in, password forms, the landing page,
the Admin's service pages, the Meet code course-choice page, the error page, any
Control Plane page

**Then** it has no page-search hint.

## AC-006 Texts come from translation files

**Given** the three hint texts

**When** the UI is in Ukrainian or English

**Then** each text comes from the translation files (`Application.Localization`)
in both languages, and no hint text is hard-coded in a view.

## AC-007 Nothing else changes

**Given** the existing test suite

**When** it runs after the change

**Then** it stays green: no page title, paging, filter, status code or other page
content changes.

---

# Open Decisions

## OD-001 The Meet code course-choice page

The page where a Dean or Admin picks a course for a Meet code
(`/workspace/meet-codes/{meetingCode}/…`) shows the whole course list, not paged.
`trebovaniya.md` v91 names "коды встреч Meet" but does not say whether this
choice page counts as a teaching-data page or as a form.

- a) **(Recommended)** No hint — it is a form for one action, like the forms
  §5 excludes; AC-005 is written this way.
- b) The ordinary hint (AC-001) — the course list on it can be long, and browser
  search works there because it is not paged.

**Resolution (Owner, 2026-10-13): a)** — no hint on the course-choice page;
AC-005 stands as written.

---

# Notes

- Expected stages: API_DESIGN is NOT_APPLICABLE or records only the view
  change (no new endpoint, no request, no status code); DB_DESIGN is
  NOT_APPLICABLE (no persistence change).
- No new NuGet package is needed.
- A single meeting's page is part of the Meet reports (US-033) and gets the
  ordinary hint; it is not paged.
- Phone wording is one shared name, "Find in page"; Safari on iPhone calls the
  item "Find on Page". No device detection (`trebovaniya.md` v91).
