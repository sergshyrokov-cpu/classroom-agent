---
artifact_type: specification
story: US-070
version: 4
status: APPROVED
created_at: 2026-11-02T19:46:14Z
updated_at: 2026-11-02T20:19:04Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-070-search-in-lists-and-pickers.md
    version: null
  - path: trebovaniya.md
    version: 109
  - path: docs/product/business-rules.md
    version: null
  - path: docs/product/non-functional-requirements.md
    version: null
supersedes: docs/specifications/US-070-spec.md@v3
---

# US-070 Specification: search in the teacher and Meet code lists, shared matching rules, pickers that narrow as you type

Nothing was delegated: the Story's sources were read directly (`trebovaniya.md` Epic 2 "Поиск",
section 5 hint, section 7 items 26 and 30; BR-087; NFR-075, NFR-078; SC-10, SC-14, SC-15; US-032
spec I-14; US-039 security review INFO-1; the code named in the Story's Notes).

## 1. Overview

One set of matching rules for every search in the installation (BR-087). The course list moves to
it, the teacher list and the Meet codes get their own search fields, and the long pickers narrow as
the user types. The three paged lists get the new first half of the §5 hint.

The header search field, student search and the ordinary page hint belong to US-071, which reuses
the rules defined here.

## 2. Business Goal

Finding information easily is a main product goal (`trebovaniya.md` section 1, v107). The pilot
school has about 300 teachers. Today the teacher list and Meet codes have no search, browser search
sees only the rows shown, and the journal, report and Meet report forms start from a plain dropdown
of every course. Deans must have search before they start work in the pilot.

## 3. Business Flow

1. An Admin or Dean opens a list, types part of what they remember in its field and submits.
2. The page shows the matches of the whole list; the text stays in the field and in the address.
3. They page through the results, act on a row, or switch language, and return to the same search.
4. On a form with a long picker, they type, the rows narrow, they choose one and submit as before.

## 4. Functional Requirements

### FR-001 Matching rules (BR-087, one implementation per side)

Given a validated search text (VR-001), the rules are:

1. The text splits into words at white space. Repeated white space produces no empty word.
2. A record matches when **every** word is found in **at least one** of the record's searchable
   fields (I-1). Words may land in different fields and in any order.
3. A word is found in a field when it is a contiguous part of that field's text. It need not start a
   word of the field.
4. Case does not matter.
5. The apostrophe variants `'` (U+0027), `’` (U+2019) and `ʼ` (U+02BC) count as one character, in the
   word and in the field. Only these three (I-2).
6. In a Meet code field only, hyphens are ignored in both the word and the code. A word made only of
   hyphens does not match a Meet code field (it would otherwise match every code), though it may
   match another field of the record. In every other field a hyphen is an ordinary character.
7. Characters are literal. `%`, `_` and `\` have no special meaning (course list S-09 today).
8. Typos are not forgiven, and no extension or similarity function is used.
9. Only current stored values match (BR-085, v103).

The server side lives behind one shared definition used by the course list, the teacher list and
the Meet codes; US-071 reuses it. The script of FR-009 applies rules 1 to 5, 7 and 8 to the row's text.

### FR-002 Course list

The search of `GET /workspace/courses` follows FR-001. Searchable fields: name, section. A course with
no section is searched by name only. The status and teacher filters still combine with the search by
AND. The field's label, its validation and its paging and return-path behaviour are unchanged, and it
gains `autocomplete="off"` (FR-007).

### FR-003 Teacher list

`GET /workspace/teachers` gets the query parameter `q` and a field in its existing form:

- Label «Пошук за ім'ям або email» / "Search by name or email", input `type="search"`,
  `maxlength="100"`, value = the validated text.
- Searchable fields: surname, given name, email. An absent value is simply not searched.
- The search covers the whole list, combined with the status filter by AND. The total and the paging
  count the matches only; a page past the last shows the last (as today).
- `q` is carried by every paging link and the language switcher's return path, in the order `status`,
  `q`, `page`. The links of a row to the course list do **not** carry it: it is a search for a person,
  not for a course (I-12).
- An empty result under a search shows the message of FR-006. With no search, the existing empty message
  stays.

### FR-004 Meet codes

`GET /workspace/meet-codes` gets the query parameter `q` and a new GET form between the hint and the
list links:

- Label «Пошук за кодом, організатором або курсом» / "Search by code, organizer or course", input
  `type="search"`, `maxlength="100"`, value = the validated text, a submit button, and hidden `list`
  and `size` carrying their effective values. Submitting opens page 0 of the same list.
- Searchable fields of a code:
  - the code (with FR-001 rule 6);
  - the name and section of its **linked** course, where it has one. Candidate courses of an unassigned
    code are not searched;
  - the organizer emails, on all three lists (OD-001 a). Only stored non-null values are searched, through
    the same read the screen uses, so an erased or excluded organizer (Epic 10, US-038 I-9, stored as null)
    is never found.
- **New column (OD-001 a).** The Linked and Not-a-course tables get the existing "Organizers" column right after
  the code, shown as on the Unassigned list: the code's distinct non-null emails in ordinal order, comma-separated,
  an empty cell when none is left. It shows with or without a search, so every email that can match is on the screen.
- The accounts that linked, confirmed or marked a code are not searched.
- The search runs over all three lists at once. The counter next to each list name shows the matches
  of that list; the shown list has only its matches. With no search the counters are as today.
- `q` is carried by the three list links (page reset to 0), the paging links and the language switcher's
  return path.
- A page past the last shows the last, with or without a search (I-6). This keeps a search on page 2
  from ending on an empty page after its last row leaves the list.

### FR-005 The search survives work on the list (Meet codes)

The validated text travels through a row action, and only the validated text is ever re-emitted:

1. The hidden fields of the "confirm" and "mark as not a course" forms and the link to the course-choice
   page carry the text beside `returnPage`, in a field or parameter named `returnQ` (the API design may
   rename it). The course-choice page carries it into its own form.
2. A success redirects to the list the code came from, its return page and the search.
3. A refusal that re-renders a page keeps the search: stale state (`409`) and unknown code (`404`) on
   the list, same course (`400`) and unknown course (`404`) on the course-choice page.
4. The course-choice page's language-switcher return path and its "cancel" link carry the search. "Cancel"
   returns to the code's current list and the return page (today it goes to the default list; I-7).
5. A `returnQ` that fails VR-001 is treated as no search. The action itself is unaffected, nothing is shown
   about it, and it is not echoed or logged.

The malformed-request error page (`400`) and the read-only refusal (`409`, host error handler) stay as
they are, and do not return to the search.

### FR-006 Empty results under a search

| List | Text (Ukrainian / English) | Rule |
|---|---|---|
| Courses | existing `Courses.Empty.NoMatch` | unchanged |
| Teachers | «Жоден викладач не відповідає пошуку» / "No teacher matches the search" | new, I-8 |
| Meet codes | «Жоден код у цьому списку не відповідає пошуку» / "No code in this list matches the search" | new, I-8 |

### FR-007 The browser does not remember the fields

The three list search fields carry `autocomplete="off"`. The erasure screen's search is not touched.

### FR-008 Hint on the three paged lists

On the course list, the teacher list and the Meet codes the hint is the first half of the §5 v108 text:

- «У цьому списку — поле «<назва поля>» нижче (Ctrl+F бачить лише показані рядки)»
- "In this list: the "<field name>" field below (Ctrl+F sees only the rows shown)"

`<назва поля>` / `<field name>` is the same translated text as the field's label, taken from one
translation entry so they cannot differ, in the user's language. The text ends without a period (NFR-078),
is the same however many pages the list has, and otherwise follows NFR-075 (one small line under the
title, always visible). The ordinary hint does not change here. US-071 adds "у всій програмі…" when the
header field exists. The hint on other pages is not touched.

### FR-009 Pickers narrow as you type

Pickers: the course field of the journal, the template report and the Meet reports; the teacher filter of
the course list; the course rows of the Meet code course-choice page, its candidates and its "other courses"
together, which are all the courses that can be chosen there (I-11).

With scripts on, a same-host script adds one text field to each picker:

1. Label from translation files: «Почніть вводити, щоб звузити список» / "Start typing to narrow the list"
   (I-8). The field has no `name`, so its text is never submitted, never appears in the address and never
   reaches the server. It carries `autocomplete="off"`.
2. As the user types, only rows matching FR-001 remain. Rows disappear on every browser, phone browsers
   included (removal rather than a style that some phones ignore on `<option>`). Clearing the field brings
   every row back, in the original order.
3. **The row's text** is what the row shows for its record (I-3): course name and section; teacher name, and
   email where the list already shows it for namesakes. On the course-choice page the translated "absent" mark and
   a candidate's share percent are shown but not matched.
4. Always kept, whatever is typed: the "choose a course" or "all teachers" row, and the row currently selected,
   so that typing never changes what the form will submit (I-4).
5. On the course-choice page one field serves both blocks. A block left with no row is hidden with its heading, by the
   `hidden` attribute (no style attribute, SC-15). The current course is a line above the form, not a row: it cannot be
   chosen there and is not narrowed.
6. When no row matches, the field shows «Нічого не знайдено» / "Nothing found", announced politely to screen
   readers. A row kept by item 4 does not count as a match. Enter in the field does not submit the form.
7. The form submits the same names and values as today.
8. On the course-choice page, while no row is left to choose (no radio remains in the form), the script sets the `disabled` attribute on the
   submit button, and removes it as soon as a row returns. "Cancel" stays available. The form therefore cannot be sent without a course (I-15).
   The dropdown pickers need no such rule: their empty row always stays (item 4).

With scripts off, or before the script runs, each picker is what it is today, with no extra field. Where there is nothing
to narrow there is no field: a form with no courses stored (`NoCoursesStored`), a teacher filter with no teacher to choose,
a course-choice page with neither a candidate nor another course.

### FR-010 Nothing changes in rights or data

No role right, no data, no audit event, no entity (see section 8). Meet code row actions keep their policy and
read-only guard.

## 5. Acceptance Criteria

Ids are the Story's.

- **AC-001 Matching rules.** Given a list holding "Іваненко Петро", a course named with one apostrophe variant
  and a code `abc-defg-hij`; when the text is `петро іван`, the name with another apostrophe variant, or
  `abcdefg`; then each is found; a text matching none of the record's words finds nothing; a misspelled word finds
  nothing. Also: `abc-defg` finds the code, a text of hyphens only finds no code, `%` and `_` match only those
  characters, and words landing in different fields of one record find it.
- **AC-002 Course list.** AC-001 holds on the course list, and the status and teacher filters combine with the
  search by AND.
- **AC-003 Teacher list.** A teacher on another page is found by surname, by given name and by email, with the status
  filter applied, and the total counts the matches only. A search matching no teacher shows the message of FR-006.
- **AC-004 Meet codes over three lists.** With matching codes in different lists, the search on any list gives counters
  showing the matches of each list, and each list shows only its matches. Matching by code, by linked course name
  and section, and by organizer email holds, and the Linked and Not-a-course tables show the organizers (OD-001 a). A list with no match under a search shows the message of FR-006.
- **AC-005 What must not be found.** A meeting whose organizer was erased or excluded is not found by that email, on any
  list, through the same read the screen uses to show organizers.
- **AC-006 The search survives work on the list.** On a searched teacher or Meet code list on page 2, changing page, acting on
  a row (link, confirm, mark as not a course; the stale, unknown-code and course-choice outcomes of FR-005) and switching
  language show the same search afterwards, validated, never the raw text. A page left empty by the action shows the last page.
- **AC-007 Validation.** A text over the length limit, repeated, or with a control character gives the course list's behaviour
  on each list: `400`, the message from translation files, nothing read with the rejected text, the text not echoed in the
  field, links or return path, and not logged.
- **AC-008 Access, read-only mode, nothing outbound.** On each list, allowed roles (Admin, Dean) get results, a forbidden role
  and an anonymous request are refused, read-only mode does not block the search; no Google port is called; no audit event and
  no log line carries the text.
- **AC-009 Pickers narrow as you type.** On each picker of FR-009, with scripts on, only rows matching BR-087 remain (plus the
  rows of FR-009 item 4) and the choice submits as before. On the course-choice page candidates narrow with the other courses, and the submit button is disabled while no row is left. With scripts off the picker works as before. A person checks this on a
  computer and on a phone, recorded in the implementation report. Automated tests assert the markup and the script's text only.
- **AC-010 Fields and hint.** On the three lists, in Ukrainian and English, each field's label comes from translation files and has
  `autocomplete="off"`; the hint names the field by its label and ends without a period (NFR-078).

## 6. Validation Rules

- **VR-001 Search text** (`q`, `returnQ`). The course list's rules, one shared implementation (`GetCourseListQuery.ReadSearch`, US-020
  VR-003), in this order: more than one occurrence → repeated; trim; empty after trimming → no search; over 100 characters after
  trimming → too long; any control character → control character. The result is passed on as validated text. These are the same
  three messages and the same existing texts on all three lists (`Courses.Validation.SearchRepeated`, `SearchTooLong`,
  `SearchControlCharacter`), with no new translation.
- **VR-002 Refusal on the course and teacher lists.** As each list refuses today: `400`, the page with the message, the field empty, no
  list read, and a return path without the text. Other valid parameters keep their values.
- **VR-003 Refusal on the Meet codes** (I-5, I-13). `400`, the page with the message, the field empty, the list links without counters, no table
  and no read of the codes, and a return path without the text.
- **VR-004 Meet codes, other parameters.** An invalid `list`, `page` or `size` with a valid `q` keeps today's fallback (the Unassigned list at its
  defaults, `400`, `QueryInvalid`), with the valid search still applied. With both invalid, VR-003 applies.
- **VR-005** The picker field of FR-009 is not sent and has no server validation. Its `maxlength` is 100.

## 7. Security Requirements

- **Authorization.** Unchanged and deny by default: `GET /workspace/courses` and `/workspace/teachers` under `ViewCourses`; the Meet codes page under
  `ViewMeetCodes`; the course-choice page and writes under `LinkMeetCodes`. All are Admin and Dean only. An Owner has no installation account.
- **Read-only mode** (AD-6, BR-026). Search reads only and is allowed. Row actions stay refused.
- **Read only, stored data only** (BR-087). No Google port. No write and no audit event (SC-11 lists no view).
- **Query string and I-14.** US-032 I-14 says the Meet page puts no personal data in a query string, and US-039 INFO-1 warns that the switcher reflects
  it. v108 decides otherwise for the search text: it is in the address, and SC-14 records the Owner's acceptance of the browser-history trace. Reconciled:
  - `q` and `returnQ` hold only what the user typed. The program never builds a code or an email into a query string from a row. A row's code stays in the
    path (I-14 stands for every system-built address);
  - each page sets the switcher's return path from validated values, so the switcher's fallback to the raw query string is never reached on these pages;
  - the validated text is encoded in every URL (`Uri.EscapeDataString`) and in the markup (Razor).
- **Logs (SC-10, SC-14).** The application logs no query string or form value. A refusal log line names the parameter and the message key, never the text.
  Existing shown-page log lines keep their fields (a count is not the text). A test asserts the text is in no log event.
- **Referrer.** `Referrer-Policy: same-origin` (SC-15) keeps the address from other sites, and these pages link to none.
- **Injection.** The text is a bound parameter on every path, the raw SQL of the unassigned page included, and is matched literally (FR-001 rule 7).
- **Scripts (SC-15).** One own static file, no inline code, no CSP change, no new package (`AGENTS.md`). The script reads rows already in the page, sends nothing and stores nothing.
- **Server check.** Any reverse proxy on the pilot server must not log addresses with `?q=` (recorded in project notes for deployment); not code in this Story.

## 8. Error Handling

Expected outcomes, not exceptions (AD-9): refusals per VR-002, VR-003; empty results per FR-006; row-action outcomes per FR-005. A database failure goes to the host's
existing handler, with the text in no message.

## 9. Non-Functional Requirements

- **Reads per request** are a fixed number: the number of words changes a query's conditions, never the number of commands. At most 50 words fit in 100 characters.
- **Size.** About 300 teachers (Story) and about 1000 students (BR-088). A scan at query time is accepted. No new index or column is required by this Specification
  (DB_DESIGN confirms).
- **Collation.** Case folding uses the explicit collation `course_name_uk`, not the database default, whose folding depends on how the cluster was created.
- **Languages** (NFR-073). Every new text is in both translation files. Data from Google is never translated.
- **One-line texts** (NFR-078). New one-line texts end without a period. Existing ones keep theirs until the base version (v109).
- **Phone** (`trebovaniya.md` section 5, responsive UI). The fields and pickers work at phone width. Checked by the person in AC-009.

## 10. Out of Scope

- The header search field, student search, the ordinary page hint (US-071).
- Removing final periods from existing texts (v109, base version).
- Typo tolerance, a database extension for search, any new role right, a student card.
- Any change to the erasure screen's search.
- A browser-history warning on the lists (only US-071's results page has one, SC-14).
- In the Meet codes search: candidate courses of an unassigned code, and the accounts that linked or marked a code.
- Keeping the search through the malformed-request error page or the read-only refusal.

## 11. Interpretations

Listed so the Owner can overturn them at `HUMAN_SPEC_APPROVAL`.

- **I-1** "A record holds all the words" spans fields: `петро gmail` finds a teacher by name and email; `математика 10` finds name and section. Today the course search matches the whole text in a single field.
- **I-2** Only the three listed apostrophe characters are folded. Others (‘, `, ′) stay literal, as §4 names exactly three.
- **I-3** A picker matches the name its row shows, not the marks beside it. An email is not added to the page for teachers without a namesake, so a teacher in the filter is found by name. The "absent" mark and a candidate's share percent are not matched.
- **I-4** The selected row and the empty choice stay in a picker even when they do not match. This is a narrow exception to "only matching rows remain": removing a selected row would silently change what the form submits.
- **I-5** On the Meet codes a rejected text shows no list, matching the course list ("behaves as the course list does"), though other invalid Meet parameters show the default list.
- **I-6** A Meet codes page past the last now shows the last, as the other two lists do. Today it shows an empty table.
- **I-7** "Cancel" on the course-choice page returns to the code's list, page and search rather than the default list. "The work continues from the same place" (§4) covers it.
- **I-8** The two new "no match" texts, the picker label and "nothing found" are worded here; requirements do not word them.
- **I-9** The narrowing field is a separate text field above the existing control, not a replacement control. The native dropdown keeps working on phones, and no scripts means no change.
- **I-10** Withdrawn in v3: the name `q` has no second reading (Epic 2: "как в поиске курсов").
- **I-11** On the course-choice page the candidates narrow together with the other courses, because "the list of all courses" there is both blocks. Otherwise "nothing found" could appear beside a matching candidate. A candidate leaves the screen only while the text does not match it, and comes back when the field is cleared.
- **I-12** A teacher row's link to the course list drops the search. It opens another list for another question, and "stays after a row action" is read as covering actions that return to this list.
- **I-13** A refused text on the Meet codes also hides the counters next to the list names, since no list is read.
- **I-15** The submit button of the course-choice page is disabled while narrowing has left no row (FR-009 item 8). Decided by the Owner at `HUMAN_SPEC_APPROVAL` on 2026-11-02. Without it the browser's "choose one" check has no radio to act on, and the form would land on the error page and lose the search. The number I-14 is skipped: in this Specification it means US-032 I-14.

## 12. Open Decisions

Full text in `docs/decisions/US-070-open-decisions.md`.

| Id | Topic | State | Impact |
|---|---|---|---|
| OD-001 | Organizer email search on the Linked and Not-a-course lists, where the screen showed no organizer | resolved by the Owner 2026-11-02: a) add the column, search all three | FR-004, AC-004, AC-005; two list tables |

`trebovaniya.md` section 7: item 30 (privacy policy) and item 26 do not block this Story; the pilot runs at the Owner's school, SC-14.

## 13. Traceability

| AC | Requirements |
|---|---|
| AC-001 | FR-001, VR-001 |
| AC-002 | FR-001, FR-002 |
| AC-003 | FR-001, FR-003, FR-006 |
| AC-004 | FR-001, FR-004, FR-006, OD-001 |
| AC-005 | FR-004, OD-001, section 7 |
| AC-006 | FR-003, FR-004, FR-005, VR-001, section 7 |
| AC-007 | VR-001, VR-002, VR-003, VR-004, section 7 |
| AC-008 | FR-010, section 7 |
| AC-009 | FR-001, FR-009, VR-005 |
| AC-010 | FR-002, FR-003, FR-004, FR-007, FR-008 |

Design note: the API changes (`q`, `returnQ`), so `API_DESIGN` applies. Persistence reads change and no schema change is expected, so `DB_DESIGN` records the
read shapes or returns NOT_APPLICABLE with that reason. Existing tests that pin the old hint texts (`PageSearchHintTestData`, `PageSearchHintTranslationTests`) change with FR-008.
