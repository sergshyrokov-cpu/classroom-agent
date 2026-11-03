---
artifact_type: test_strategy
story: US-070
version: 1
status: DRAFT
created_at: 2026-11-03T07:00:13Z
updated_at: 2026-11-03T07:00:13Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-070-search-in-lists-and-pickers.md
    version: null
  - path: docs/specifications/US-070-spec.md
    version: 4
  - path: docs/decisions/US-070-open-decisions.md
    version: 2
  - path: docs/designs/api/US-070-api-design.md
    version: 1
  - path: docs/designs/api/US-070-openapi.yaml
    version: 1
  - path: docs/designs/database/US-070-db-design.md
    version: 1
  - path: docs/designs/database/US-070-entity-model.md
    version: 1
supersedes: null
---

# US-070 Test Strategy

## 1. Scope

The shared matching rules (FR-001) on the course list, the teacher list and the Meet codes; the two new fields; the search carried through
paging, row actions and the language switch; the refusals; the Organizers column; the page clamp; the narrowing pickers' markup and script text;
the hint and the new translations. No schema change, so no migration test.

## 2. Test levels

| Level | Used for | Classes |
|---|---|---|
| Integration over a source (real PostgreSQL, TC-2) | how text matches, what each list searches, bound parameters, number of commands | `SearchMatchingSourceTests` |
| HTTP integration (real PostgreSQL) | pages, statuses, URLs, forms, refusals, row actions | `TeacherListSearchPageTests`, `MeetCodesSearchPageTests`, `MeetCodeSearchSurvivesActionsTests`, `CourseListSearchPageTests`, `PickerNarrowMarkupTests` |
| Security | roles, read-only mode, nothing outbound, logs, audit | `ListSearchAccessTests`, `MeetCodeSearchSurvivesActionsTests` (read-only row action) |
| Source scan | the script's text, translation files, no inline code | `PickerNarrowScriptTests`, `ListSearchTranslationTests` |
| Manual (the Owner, recorded in the implementation report) | narrowing in a real browser, computer and phone | AC-009 |

The matching rules are proved at source level because that is where they can go wrong (db-design §2), and each list is then proved over HTTP for its wiring. The project has no browser test tool and none is added: AC-009 says automated tests assert the markup and the script's text only.

## 3. Scenarios

**Positive.** Words in any order, as parts of words, in any case, across fields; all six apostrophe pairs on all three sources; a code without or with partial hyphens; a teacher on a later page by surname, given name, email; a code by linked course name and section and by organizer on each list; `q` in paging, list links, return path, redirects, choice page, Cancel.

**Negative.** A misspelled word; a word matching none of a record's fields; `‘`, `` ` ``, `′` stay literal; a hyphen-only word finds no code; a candidate course and the accounts that acted on a code are not searched; a student or former teacher stays unlisted; a row's course link drops the search (I-12).

**Boundary.** Exactly 100 characters allowed, 101 refused; white space only is no search; a page past the last, with and without a search; a total of 0 gives page 0; a row action leaving a page empty; a code with no organizer left, or no session.

**Validation.** VR-001's three refusals on each list (`400`, message, field empty, no list, return path without the text, text nowhere in the page); both invalid on the Meet codes (VR-004); invalid `list`, `page`, `size` with a valid search; a bad `returnQ` on a post and on the choice page is no search, and a repeated one does not make the form malformed (D-2), with a repeated known field as the control.

**Security.** Admin and Dean allowed, anonymous and restricted session redirected, on each list; read-only mode allows the search and still refuses a row action; no Google call, no write, no audit row and no log line holds the text; the word is a bound parameter in every read, the raw Unassigned SQL included; the text is encoded in URLs and markup; no code or email is built into a query string from a row.

**Persistence.** Reads only: no schema change; searching writes nothing; organizers come through the same null-excluding read, so an erased one is neither found nor shown (AC-005, with a before-erasure control); distinct, ordinal; the number of commands does not depend on the number of words.

## 4. Fixtures

`PostgreSqlFixture`, `InstallationTestHost`, `JournalHostExtensions` for a signed-in actor in a chosen legitimacy state, SQL seeding by the US-020, US-022 and US-032 helpers. New: `ListSearchTestData` (hooks, keys, URLs written out literally in the openapi's `query-order`, refused texts), `ListSearchReadLog`, `RecordingTeacherListSource` and `RecordingMeetCodesReadSource`, which wrap the real sources so "nothing was read with the rejected text" is observed over HTTP, paired with a control where an accepted text does reach them.

## 5. Skeleton

The port and row signatures of `US-070-entity-model.md` §1, §2. The new `search` parameter is ignored and `OrganizerEmails` is empty until IMPLEMENTATION; existing callers pass `null`. No new member, so nothing throws `NotImplementedException`; tests fail on their assertions. `SearchMatch` is not declared: the entity model leaves its members to the implementer and tests reach it through the sources.

## 6. Excluded, with reasons

- **Narrowing as behaviour.** No browser tool; AC-009 gives it to the person. Automated tests read markup and script text.
- **Read-only mode in all four causes.** One cause per list. The search adds no guard, and `TeacherListReadOnlyTests` and US-032 cover the pages in the other causes.
- **A log line on the Meet codes page.** There is none by design (D-7); the test asserts the text is in no log event.
- **Reverse proxy logs.** Not code; recorded for deployment.
- **The erasure screen's search.** Untouched (FR-007).

## 7. Known limitations

- A script text test proves that a call or a name is present, not that it runs correctly. The person's check in AC-009 and SECURITY_REVIEW's reading cover the rest.
- Case folding under `course_name_uk` is proved for Ukrainian and Latin text as the tests hold, not for every alphabet.
- The redirect after a row action has no `size` (D-5), so a non-default size is lost, as today.

## 8. Open Decisions affecting testing

None. OD-001 is resolved (option a) and its column and search are tested.
