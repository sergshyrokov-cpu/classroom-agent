---
artifact_type: specification
story: US-071
version: 2
status: APPROVED
created_at: 2026-11-03T15:28:58Z
updated_at: 2026-11-03T16:43:20Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-071-search-field-in-the-header.md
    version: null
  - path: trebovaniya.md
    version: 109
  - path: docs/product/business-rules.md
    version: null
  - path: docs/product/non-functional-requirements.md
    version: null
  - path: docs/specifications/US-070-spec.md
    version: 4
supersedes: docs/specifications/US-071-spec.md@v1
---

# US-071 Specification: one search field in the header, including students with their courses

Nothing was delegated: the sources were read directly (`trebovaniya.md` Epic 2 "Поиск", Epic 3 "Выбывший ученик", section 5 hint,
section 7, section 3 `CourseMembership`; US-060 OD-003; BR-068, BR-085, BR-087, BR-088; NFR-075, NFR-076, NFR-078; SC-10, SC-14, SC-15; US-039 spec I-5; US-070 spec; the code named below).

## 1. Overview

A «Пошук» / "Search" field in the header of every installation page after sign-in opens one results page with four groups: students,
teachers, courses and Meet codes. Students become findable for the first time, with the courses they are or were in. The page hints get
their final v108 texts. The Story also makes disabled buttons grey on both hosts, removes a raw query from the error page, and adds
tests left over from US-070.

The matching rules are US-070 FR-001 (BR-087), reused and not restated.

## 2. Business Goal

Finding information easily is a main product goal (`trebovaniya.md` section 1, v107). Today a Dean must know the right screen before
searching, and cannot find a student without knowing the course. Deans must have this before they start work in the pilot.

## 3. Business Flow

1. An Admin or Dean types part of what they remember into the header field on any page and submits.
2. The results page shows the matches by group, and the text stays in the field and in the address.
3. They follow a row to a course card, a teacher's courses or a Meet code list, or refine the text. The browser's Back button returns to the results.

## 4. Functional Requirements

### FR-001 The field in the header

- A GET form in the page header with one input: name `q`, `type="search"`, `maxlength="100"`, `autocomplete="off"`, the visible label
  «Пошук» / "Search", and a submit button «Знайти» / "Find" (I-1). It works with scripts off.
- **Shown** when all of these hold: the user is signed in; the endpoint requires sign-in; the session holds no temporary password claim
  (`InstallationClaimTypes.PasswordIsTemporary`); the page is not the error page (FR-010).
- **Not shown** on pages before sign-in, on the forced password change, on the error page and in the Control Plane. The own-password page
  (`/account/password`) is an ordinary page and shows it.
- On the results page its value is the validated text. On every other page it is empty, including the three lists with their own search fields.
- It works at phone width (checked by the person in AC-011's walk).

### FR-002 The results page

- `GET` with the query parameter `q`, reached by the form and by address. The path is `API_DESIGN`'s to define.
- Title «Результати пошуку» / "Search results" (I-1).
- Groups in this fixed order, each with a heading, a group with no match hidden: «Студенти» / "Students" (I-2), «Викладачі» / "Teachers", «Курси» /
  "Courses", «Коди зустрічей Meet» / "Meet codes".
- A person in both roles is in both groups. Two namesakes are two rows.
- The page has no page-search hint: it is not on the closed list of NFR-075 (I-3).

### FR-003 Students

A person is in this group when they hold every word (BR-087) in surname, given name or email, and have a course membership with the role student in at least one course, on the roster or not.

This includes students known only through submissions: synchronization gives them an off-roster student membership (`trebovaniya.md` section 3, v56). **Such a student is found only if the program knows their name or email** (I-4). One first met through a submission is stored with neither until a roster read names them, so no text can match them; the journal shows them as unnamed.

A row shows the name (the label of `ParticipantLabel`: surname and given name, else email), the email, and **all** the courses the person is a student of, whichever courses
the search matched. Courses are in the course list's alphabetical order, each a link to its card. There is no student card.

A course the student is not on the roster of now carries the BR-068 mark, in the journal's words and from the same translation entries (`DataMarks.Left.On`, `DataMarks.Left`), by the journal's own rule (US-060 OD-003 a, `DataMarkRules.Left`): «вибув(-ла) <дата>» with the last-seen date, or «вибув(-ла)» with
no date when the membership's first and last sightings are equal, which is how a student known only through submissions is stored (I-5). A course they are on the roster of carries no mark. Back on the roster, no mark. Leaving belongs to a course: the person's row has no mark of its own.

A course of a teacher membership is not listed here, even for a person who is both.

### FR-004 Teachers

The people of the teacher list at status "all": a current teacher membership in a course of any status (I-6, I-7). Searchable: surname, given name, email. A row shows the name, the email and a
link to the course list filtered by that teacher at status "all" (`CourseListUrl.Build(All, teacherId, null, 1)`), without the search text. The existing teacher id in the address stays; no name or email is built into it.

### FR-005 Courses

Courses of any status (I-6). Searchable: name, section. A row shows the name, the section where there is one, the status in the course list's words (`Courses.Status.*`) and a link to the card (I-8).

### FR-006 Meet codes

The search of US-070 FR-004 over all three lists: the code, the linked course's name and section, the organizer emails through the read the screen uses. A row shows the code, the name of its list (`MeetCodes.List.*`) and a link to that list with this search (I-9). A code is never built into a query string by the program (section 7).

### FR-007 Limits

- At most 20 rows per group, alphabetical: students and teachers by `ParticipantLabel`, courses as in the course list, codes by the code in ordinal order.
- With more than 20 matches, under the group: «показано 20 з N, уточніть запит» / "showing 20 of N, refine your search", where N is the group's number of matches (I-10).
- With more than 20 matches, teachers, courses and Meet codes also get a link «Увесь список із цим пошуком» / "The whole list with this search" (I-1, I-11), never students:
  - teachers: the teacher list at status "all" with `q`;
  - courses: the course list at status "all" with `q`;
  - Meet codes: the Meet codes page with `q`, on the first of Unassigned, Linked, Not a course that has a match.

  N on the results page equals the total the list shows; for Meet codes, which the link opens on one of three lists, N equals the sum of the three lists' counters.

### FR-008 The messages of the page

| Case | Reads data | Text (Ukrainian / English) | Status |
|---|---|---|---|
| No text, or only white space | no | «Введіть щонайменше 2 символи» / "Enter at least 2 characters" (I-1) | 200 |
| 1 character after trimming | no | the same | 200 |
| Refused by VR-001 | no | the existing `Courses.Validation.Search*` text | 400 |
| Nothing found | yes | «Нічого не знайдено за запитом «<текст>»» / "Nothing found for "<text>"" (I-1), then the v108 reminder | 200 |

The v108 reminder, word for word: «Пошук знаходить поточні імена й назви з Google; за колишнім прізвищем або старим email не знайти» / "Search finds current names and titles from Google; a former surname or an old email will not be found".

The reminder shows only when nothing at all is found, not when one group is empty.

### FR-009 The browser history warning

One line in small text, from translation files, word for word: «Шуканий текст залишається в історії браузера; на спільному комп'ютері очистіть її» / "The text you search for stays in the browser history; clear it on a shared computer".

It shows in every state of the results page, including the short and refused texts, because the address holds the text in each of them (I-12).

### FR-010 No raw query on the error page

The error page has no language switcher in any case, as US-039 I-5 already intends (I-13). Today the rule looks only at whether the endpoint requires sign-in, so `ErrorController.Page` answered under a protected endpoint (a malformed code on `GET` course-choice, the read-only refusal, the antiforgery refusal) still renders the switcher, whose fallback copies the raw path and query into a hidden field.

One rule decides "this is the error page" for both the switcher and the header field. The page keeps its text, and its back link where it has one (`400`). Other pages' switchers are unchanged: section 7 lists how their return paths behave.

### FR-011 Page hints

The final §5 v108 texts replace the current ones, in one line, without a final period (NFR-078):

- Ordinary page: «На цій сторінці — Ctrl+F (на Mac — ⌘F; на телефоні — меню браузера → «Знайти на сторінці»), у всій програмі — поле «Пошук» угорі» / "On this page: Ctrl+F (⌘F on Mac; on a phone — browser menu → "Find in page"); across the program: the "Search" field at the top".
- Course list, teacher list, Meet codes: «У цьому списку — поле «<назва поля>» нижче (Ctrl+F бачить лише показані рядки), у всій програмі — поле «Пошук» угорі» / "In this list: the "<field name>" field below (Ctrl+F sees only the rows shown); across the program: the "Search" field at the top".

The names in the hint come from the same translation entries as the field labels, so they cannot differ. The hint's other rules (NFR-075) do not change. Every page that shows the hint today gets the text of its kind, which includes the «Стан даних» page: it is not on the §5 list but already holds the ordinary hint (I-14).

### FR-012 Grey disabled buttons

A disabled button (`.btn:disabled`) of either host is grey with readable text, whatever its colour when enabled, by one rule in the shared first part of each host's `site.css` (NFR-076, US-068 FR-010.1). There is no new text and no inline style (SC-15). Enabled buttons do not change.

It shows on the course-choice submit, the Meet and report export buttons during a download, and the three erasure buttons in read-only mode, including the outlined red one (I-15). The Control Plane has no disabled button today; it gets the rule so that a later one looks the same.

### FR-013 Tests left over from US-070

No behaviour changes. Tests prove:

1. A text with markup characters (`a&b=<c>"d'`) stays encoded through the Meet code list, the hand-built `MarkForm`, the confirm form and the course-choice form.
2. `GET` course-choice for an unknown code keeps a valid `returnQ`.
3. An invalid `status` or `page` with a valid `q` on the teacher list keeps the text (US-070 VR-002).
4. The script tests for hiding an empty block and for disabling the submit fail if the behaviour is removed, the script's comments notwithstanding.
5. The summaries of those tests cite the right US-070 FR-009 items.

### FR-014 Nothing changes in rights or data

No role right, no stored data, no entity, no audit event, no export of results.

## 5. Acceptance Criteria

Ids are the Story's.

- **AC-001 The field is where it should be.** On each installation page after sign-in, for an Admin and a Dean, in both languages, the header holds the field, except on the forced password change and the error page, including an error page answered under a protected endpoint. Pages before sign-in and the Control Plane do not hold it. The field has `autocomplete="off"`.
- **AC-002 Groups and order.** A text matching a student, a teacher, a course and a Meet code shows the four groups in the fixed order. A group with no match is hidden. A person in both roles appears in both groups.
- **AC-003 A student and their courses.** A student on one roster who has left another course shows both courses with links to their cards, and only the left course carries the mark in the journal's words, with its date. A named student whose membership in a course is off the roster with equal first and last sightings shows that course with the mark and no date, as the journal does. A student stored with no name or email is not found by any text.
- **AC-004 Limits.** With more than 20 matches in a group, 20 show alphabetically with the count line, and the link opens the whole list with this search, with the same total, which for Meet codes is the sum of the three lists' counters (not for students). With exactly 20 there is neither line nor link.
- **AC-005 Too short, invalid, nothing found.** A text of 1 character and an invalid text read no data and show a message from translation files; the invalid text is not echoed in the field, a link or the return path. A text matching nothing repeats the text, encoded, and shows the v108 reminder.
- **AC-006 What must not be found.** An erased or excluded person, and a meeting with such an organizer, are not found by their name or email, by a test on stored rows. A former surname is not found either (BR-085).
- **AC-007 Access and logs.** Admin and Dean get results, including in read-only mode. A forbidden role and an anonymous request are refused by the protected endpoint's rules, and a session with a temporary password goes to the forced change. No Google port is called; no audit event and no log line carries the text.
- **AC-008 The warning.** The one-line warning shows in small text from translation files, with or without matches.
- **AC-009 Hints.** Every page of the §5 hint list, in both languages, shows the v108 text of its kind without a final period, naming the same labels as the fields.
- **AC-010 Speed on the expected size.** The measurement of section 9 is made on real PostgreSQL, with no new index, and recorded in the implementation report.
- **AC-011 Grey disabled buttons.** A disabled button is grey by a rule that a test finds in the `site.css` of both hosts. Enabled buttons do not change. A person sees it on the course-choice page, with no row left to choose, on a computer and on a phone, and checks the header field in the same walk; both are recorded in the implementation report.
- **AC-012 No raw query on the error page.** For a signed-in user and `GET` course-choice with a malformed code and a `returnQ` holding a marker text, the marker is nowhere in the page, the switcher's hidden field included. The test first shows the defect, which was established by reading the code, not by a request.
- **AC-013 Tests left over from US-070.** The tests of FR-013 exist and pass.

## 6. Validation Rules

- **VR-001 Search text** (`q`). US-070 VR-001, the one implementation (`CourseListQueryRules.ReadSearch`): repeated; trim; empty is no search; over 100 characters; control character. Same three existing messages, no new translation.
- **VR-002 Refused text.** `400`, the results page with the message, the field empty, no read, no group, a return path without the text. The text is not logged: a refusal log line names the parameter and the message key only.
- **VR-003 Minimum length.** After VR-001, a validated text shorter than 2 characters (trimmed `string` length) reads no data and shows the FR-008 message with `200`, since it is not malformed. A 1-character text stays in the field. A text of two one-letter words (`а б`) is 3 characters and is searched.
- **VR-004** Other query parameters are not read and are never echoed.

## 7. Security Requirements

- **Authorization** (deny by default, SC-4). The results page is under `ViewCourses`, the §2 row "Просмотр курсов и участников": Admin and Dean only. Both roles also hold `ViewMeetCodes`, so the page shows them all four groups (Epic 2: "Админ и Декан видят то же, что и на экранах курсов"). An Owner has no installation account.
- **Temporary password.** `TemporaryPasswordMiddleware` sends such a session to the forced change before the page runs.
- **Read-only mode** (AD-6, BR-026). Search only reads and is allowed. Links lead to pages with their own rules.
- **Read only, stored data only** (BR-087). No Google port, no write, no audit event (SC-11 lists no view).
- **Personal data in a query string** (raised as the Story asks: US-039 INFO-1). This is the first screen with students' names in `?q=`, decided acceptable in v108 and accepted in SC-14:
  - `q` holds only what the user typed. The program builds no name, email or code into a query string; a row's links carry only ids or the user's own validated text;
  - the page sets the switcher's return path from the validated text, so the switcher's raw fallback is never reached there. Without a valid text the return path holds none. It is served only to the same user;
  - pages that set no return path keep the fallback, which copies their query encoded into the same user's own page. None of them holds a search text, and the error page no longer has a switcher (FR-010);
  - the text is encoded in every URL (`Uri.EscapeDataString`) and in the markup (Razor).
- **History, cache and referrer.** SC-14 keeps results out of the browser cache but not the address out of its history, hence FR-009. `Referrer-Policy: same-origin` (`SecurityHeadersMiddleware`) keeps the address from other sites.
- **Logs** (SC-10, SC-14). The application logs no query string, form value or referrer holding the text. A shown-page log line may carry counts, never the text. A test asserts the text is in no log event.
- **Injection.** The text is a bound parameter on every read and matched literally (US-070 FR-001 rule 7), through `SearchMatch`.
- **What is not found** (BR-087, Epic 10). Search reads only current stored rows. An erased person's participant, memberships and submissions no longer exist, and an erased or excluded organizer is stored as null.
- **Folding.** `SearchMatch` with its explicit collation, never the default of `ParticipantErasureReadSource`. The Admin-only erasure search stays separate and unchanged.
- **Server check.** Any reverse proxy on the pilot server must not log addresses with `?q=`; already in the project notes for deployment, and not code in this Story.

## 8. Error Handling

Expected outcomes, not exceptions (AD-9): refusals and messages per FR-008. A database failure goes to the host's existing handler, with the text in no message.

## 9. Non-Functional Requirements

- **Speed (AC-010).** `trebovaniya.md` gives no number for "usable", so this Specification sets what is measured (I-16):
  - **Data:** synthetic, on real PostgreSQL: 1000 students, 300 teachers, 600 courses, about 10 student memberships per student, and Meet codes and submissions at the proportions the test data allows, stated in the report.
  - **Texts:** a common 2-character word matching many people; a 2-word name; a text matching nothing.
  - **Measure:** the full time of all reads of the page, five runs per text after one warm-up, the median and the worst recorded in the implementation report.
  - **Bound:** a median under 2 seconds on the development machine, with no new index. Above it, the report says so and the Owner decides at `HUMAN_PR_APPROVAL`.
  - It is a recorded measurement, not a pass-or-fail timing in the suite: timings under a loaded test run have repeatedly proved unreliable in this project.
- **Reads per request** are a fixed number: words change a query's conditions, never the number of commands, and listing the courses of 20 students takes no read per student.
- **Languages** (NFR-073). Every new text is in both translation files. Data from Google is never translated.
- **One-line texts** (NFR-078). New one-line texts end without a period. Existing ones keep theirs until the base version (v109), so the three reused `Courses.Validation.Search*` texts keep their periods.
- **Phone** (`trebovaniya.md` section 5). The field and the results page work at phone width.
- **Manual checks** use a clean Edge profile on the stand, as the Owner's everyday profile lost the stand's styles in US-070 (report §3.1, cause not found).

## 10. Out of Scope

- A student card with grades.
- Typo tolerance, search in the Control Plane, any new role right, export of results.
- Removing final periods from existing texts (v109, base version).
- The Admin-only erasure search.
- The marks «нет в Classroom» and «не виден техническому аккаунту» on results rows (§4 places them in the course list and card).
- Searching a former surname or email (BR-085), `FullName`, or a code's candidate courses.
- A return from the course card's own links to the results page; the browser's Back button does this.
- The privacy policy (`trebovaniya.md` section 7 item 30).

## 11. Interpretations

Listed so the Owner can overturn them at `HUMAN_SPEC_APPROVAL`.

- **I-1** Texts the requirements do not word are worded here: the button «Знайти», the page title, the short-text message, the "nothing found" line and the whole-list link. The field label is the requirements' «Пошук».
- **I-2** The students' group is headed «Студенти», the word of the course card that lists the same people, though `trebovaniya.md` says «ученики» and the journal column says «Учень». The screens use both words today; aligning them is not this Story's work.
- **I-3** The results page has no page-search hint, since NFR-075's list is closed and the page has only 80 rows at most.
- **I-4** "Кто числится учеником хотя бы в одном курсе, включая выбывших" meets a limit the requirements do not state. A student known only through submissions has a student membership, so they count, but one first met through a submission is stored with no name or email until a roster read names them, and search has nothing to match. The journal shows that person as unnamed; search does not find them. They are found once the program knows their name or email, for example from a roster of another course. Finding them otherwise would need synchronization to read their profiles, which changes synchronization and is outside this Story. **If the Owner wants them found, that is a separate Story.**
- **I-5** The mark carries the last-seen date where the journal shows it ("как в журнале"), though Epic 2 quotes only «вибув(-ла)».
- **I-6** The teachers' and courses' groups cover every status, not the lists' default of active. A global search that hid archived courses would give "nothing found" for something that exists; a course row shows its status.
- **I-7** A teacher who has left every roster is not in the teachers' group, while a departed student is in theirs. Epic 2 says "включая выбывших" only of students, this group is "the teacher list", and the link to that person's courses would find nothing. **If the Owner wants departed teachers found, this needs its own wording of what the row links to.**
- **I-8** A course row shows no «нет в Classroom» mark. It is on the card one click away; section 10.
- **I-9** A Meet code row links to its list with the search text, not to one code. The Meet codes page has no single-code view, and US-032 I-14 keeps the system from building a code into a query string. With many matches the code can be on that list's next page.
- **I-10** The Story's «показано 20 з N» is used. `trebovaniya.md` Epic 2 prints «из» inside the Ukrainian text, read as a slip of the Russian document.
- **I-11** The whole-list link shows only with more than 20 matches: the Story places it in the limits, and at 20 or fewer the page already shows everything. On the Meet codes it opens the first list with a match, so it never lands on an empty list.
- **I-12** The warning also shows on short and refused texts, which stay in the history too. AC-008 names "with or without matches".
- **I-13** The Story asks that the error page's switcher never copy the raw query. The fix chosen removes the switcher from every error page rather than giving it a clean return path, because US-039 I-5 already says the error page has none. A signed-in user who wants another language first leaves the error page. The US-070 follow-up note suggested the other fix; it would have kept a switcher on a page that should have none, and given POST-only addresses a return path.
- **I-14** The «Стан даних» page keeps its hint and gets the ordinary text, though §5 does not list it: removing it is not this Story's work.
- **I-15** Every disabled button goes grey, not only primary ones as AC-011's example says, because the Story's scope says "a disabled button" and names the erasure screens, one of whose buttons is outlined red.
- **I-16** The speed bound (2 seconds, median) and the data's proportions are set here. The Story leaves "usable" to this Specification.
- **I-17** The field goes into the header although `trebovaniya.md` section 8 (v105) and NFR-076 say the header's content does not change. Epic 2 v108 is later and explicit, so it wins here. Both older sentences now state something false and need correcting in the next requirements version; this Story does not edit them.

## 12. Open Decisions

None open. Full text in `docs/decisions/US-071-open-decisions.md`.

`trebovaniya.md` section 7: item 30 (privacy policy) and item 26 do not block this Story; the pilot runs at the Owner's school, SC-14.

## 13. Traceability

| AC | Requirements |
|---|---|
| AC-001 | FR-001, FR-010 |
| AC-002 | FR-002, FR-003, FR-004, FR-005, FR-006 |
| AC-003 | FR-003 |
| AC-004 | FR-007 |
| AC-005 | FR-008, VR-001, VR-002, VR-003, section 7 |
| AC-006 | FR-003, FR-004, FR-006, section 7 |
| AC-007 | FR-014, section 7 |
| AC-008 | FR-009 |
| AC-009 | FR-011 |
| AC-010 | section 9 |
| AC-011 | FR-012, FR-001 |
| AC-012 | FR-010 |
| AC-013 | FR-013 |

Design note: a new endpoint, so `API_DESIGN` applies. Reads change and no schema change is expected, so `DB_DESIGN` records the read shapes (in particular the students' read with the courses of each row) or returns NOT_APPLICABLE with that reason. Existing tests that pin the old hint texts (`PageSearchHintTestData`, `PageSearchHintTranslationTests`) change with FR-011.
