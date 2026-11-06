---
artifact_type: specification
story: US-074
version: 1
status: DRAFT
created_at: 2026-11-06T19:35:52Z
updated_at: 2026-11-06T19:35:52Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-074-meet-code-organizer-by-name.md
    version: null
  - path: trebovaniya.md
    version: 112
  - path: docs/specifications/US-070-spec.md
    version: null
  - path: docs/specifications/US-071-spec.md
    version: null
  - path: docs/decisions/US-074-open-decisions.md
    version: 1
supersedes: null
---

# US-074 Specification: Meet code lists show the organizer by name and find the code by that name

Grounded in `trebovaniya.md` v112: section 4 Epic 4 («организатор в списках кодов», v110), section 5
«Поиск» (matching rules, «Что не находится», «Только чтение»), the header search group «коды встреч
Meet», and the volume estimate in section 5 (about 1000 students and 300 teachers). Business rules BR-085,
BR-087 and BR-088. Section 7 holds no open item on this subject; item 28 names the `call_ended` field
`organizer_email` and is not touched.

Delegated to `quick-look` (read only, facts only): the `trebovaniya.md` line ranges above and the code facts
at commit `eff66cb` (`MeetCodesReadSource`, `SearchMatch`, `MeetReportSource.GetPeopleAsync`,
`MeetReportRules.Person`, `_MeetPerson.cshtml`, `MeetCodes/Index.cshtml`, `GetSearchResultsQuery`, the entity
configurations). The spec-writer read the decisive lines itself.

## 1. Overview

The Meet codes page (`GET /workspace/meet-codes`) shows each code's organizers as emails only, on all three
lists (Unassigned, Linked, Not a course; `MeetCodes/Index.cshtml` lines 69, 121, 184). After this Story each
organizer is shown as the Meet reports show a person: the name as Google gave it with the email beside it,
the email alone when the program knows no name. The page's search field and the header's search field also
find a code by its organizer's name. The name comes from Classroom participants already stored. No new call
to Google, no new stored data, no new endpoint, no new role right, no new translation entry.

## 2. Business Goal

Linking unassigned codes is daily Dean work in the first weeks of the pilot. An email alone is hard to
recognise (rehearsal finding P-15 on the real domain). The Meet reports already show the name with the
email; the codes page is the one place left with the email alone.

## 3. Business Flow

1. A Dean or an Admin opens the Meet codes page on any of the three lists.
2. Each row's organizer cell lists the organizers by name and email (or email alone).
3. The user types part of a teacher's name into the page's search field; the three counters show the
   matching codes per list, and the shown list contains only its matches.
4. Or the user types the name into the header's search field; the "Meet codes" group lists the matching
   codes with their list and a link, as since US-071.
5. The user links the code to a course, confirms it or marks it "not a course" as before; the search stays.

## 4. Functional Requirements

### FR-001 Who an organizer is (one definition for display and search)

An **organizer of a code** is each distinct email among the non-null `MeetSession.OrganizerEmail` values of
the code's sessions, as `LoadOrganizersAsync` reads them today. An erased or excluded organizer is stored as
null (Epic 10, `MeetSession.ClearOrganizer`) and is therefore never an organizer.

- **Email comparison.** Two emails are the same when they are equal after lower-casing
  (`ToLowerInvariant` in C#, `lower()` in SQL), the comparison the Meet reports use
  (`MeetReportSource.GetPeopleAsync`, `MeetReportRules.Lower`) and the erasure path uses
  (`ParticipantErasureStore`). Stored `ClassroomParticipant.Email` is already trimmed and lower-cased on
  write (`ClassroomParticipant.Apply`).
- **Distinct organizers (I-1).** Organizers of one code are distinct by that comparison: emails differing
  only in case are one organizer, shown with the ordinally first stored spelling. Today they are distinct by
  ordinal comparison.
- **The organizer's name.** The name of an organizer email is the `FullName` of the Classroom participant
  with the lowest `Id` among those whose email is the same and whose `FullName` is not null — the rule of the
  Meet reports (`MeetReportSource.GetPeopleAsync`, US-033 db-design §4). A name that is empty or only white
  space counts as no name (`MeetReportRules.Name`, US-033 I-12). No participant, or no participant with a
  name, means no name.
- The name is shown and searched exactly as stored (Google data is a mirror, v83). It is never corrected,
  translated or composed from `Surname` and `GivenName`.

Display (FR-002) and search (FR-003) use this one definition, so the list and the search cannot disagree about
who the organizer is or what the organizer is called. The implementation keeps one definition of the name rule
for the codes page and the Meet reports, or proves by test that the two give the same name for the same data
(AC-009).

### FR-002 The organizer cell on the three lists

On the Unassigned, Linked and Not-a-course tables, the existing organizer cell shows the code's organizers
(FR-001):

- in ordinal order of the shown email, as today;
- separated by «, » as today;
- each organizer with a name: the name, then the email beside it, rendered as the Meet reports render a person
  (`_MeetPerson.cshtml`: a name element, then an email element). The view may reuse that partial or render the
  same structure;
- each organizer without a name: the email alone;
- each organizer once (AC-003);
- an empty cell when the code has no organizer left, as today.

Name and email are encoded by Razor (no `Html.Raw`) and never translated. The "unnamed" label
(`MeetReports.Person.Unnamed`) never appears here, since an organizer always has an email.

The column header, the column position (right after the code on Linked and Not a course, US-070 FR-004), the
other columns, row actions, paging and empty-state texts are unchanged.

The row models change from a list of emails to a list of organizers, each a name (nullable) and an email:
`UnassignedCodeRow`, `LinkedCodeRow`, `MarkedCodeRow` in `Application/Models`, and the DTOs
`UnassignedCodeItem`, `LinkedCodeItem`, `NotACourseCodeItem` in `Application/Models/Dtos`. The exact type
shape is the API and database designs' choice; a domain entity never appears in them (AD-8).

### FR-003 Search by organizer name on the Meet codes page

The searchable fields of a code (US-070 FR-004) gain one: **the name of each of its organizers** (FR-001), on all
three lists. The rest is unchanged:

- matching follows US-070 FR-001 through the shared `SearchMatch` definition (BR-087): words in any order,
  each word in at least one field, part of a field, case-insensitive, the three apostrophe variants equal,
  literal characters, no typo tolerance. The hyphen rule 6 applies to the code field only, so a hyphen in a
  name is an ordinary character;
- the words of one search may land in different fields of the code: one word in an organizer's name, another
  in the code, the linked course or an email (US-070 FR-001 rule 2). Two words of a search may also land in the
  names of two different organizers of the same code, as two words may already land in two organizer emails
  today (I-2);
- only the name defined in FR-001 is searched: the name of an organizer the code has, chosen by the lowest-id
  rule. A participant's other names (another participant with the same email, `Surname`, `GivenName`, a former
  name) are not searched (BR-085, I-3);
- an erased or excluded organizer is never found by name or by email, since their email is null (FR-001,
  AC-004);
- the search runs over all three lists at once; each counter shows its list's matches; `q` is carried by the list
  links, the paging links, the language switcher's return path and through row actions, as today (US-070 FR-004,
  FR-005);
- the search text's validation and refusal (US-070 VR-001…VR-004) are unchanged.

The field's label already says "organizer" («Пошук за кодом, організатором або курсом») and is unchanged.

### FR-004 Search by organizer name in the header

The header search's "Meet codes" group (US-071 FR-006) searches codes through the same read as the codes page
(`GetFirstMatchesAsync`, `GetCountsAsync`), so it gains FR-003 with no change of its own: a code found by its
organizer's name is listed with its code, its list's name and the link to that list with this search; the group
keeps the 20-result rule and «показано 20 з N» with N the sum of the three counters; its link to the whole list
carries the search and opens the first list with a match. The group's rows show no organizer (unchanged).

### FR-005 Reading names for a page

The names of a page's organizers are read in a bounded number of queries for the whole page, never one query per
row or per organizer (AC-008). Search by name is evaluated in the database together with the other searchable
fields, not by loading the participants into memory. Whether an index is needed for search by name at the
expected volume is the database design's decision, stated with its reason.

## 5. Acceptance Criteria

- **AC-001 Name with email.** For a code whose organizer is a known participant with a name, on each of the three
  lists, a Dean and an Admin see the name with the email beside it, Razor-encoded and untranslated, in both UI
  languages. (FR-001, FR-002)
- **AC-002 Email alone.** An organizer with no participant of that email, or whose participants have no name or
  only a blank name, is shown by the email alone. (FR-001, FR-002)
- **AC-003 Several organizers.** A code with several organizers, some with a name and some without, shows each by
  its own rule, once, in ordinal email order; two stored spellings of one email differing only in case are one
  organizer. (FR-001, FR-002)
- **AC-004 An erased or excluded organizer stays invisible.** For a meeting whose organizer email was cleared by an
  erasure or an exclusion, no name or email of that person is shown on any list, and a search for their former
  name or email does not find the code through them (it may still find it through another field). (FR-001, FR-003)
- **AC-005 Search by name in the list.** On the Meet codes page, a search for part of an organizer's name —
  including words in another order, another case, or another apostrophe variant — finds the code in its list; the
  three counters show the matches; the search stays across pages and after a row action; search by code, email and
  linked course still works; a name of another participant with the same email that is not the chosen name does not
  find the code. (FR-001, FR-003)
- **AC-006 Search by name in the header.** The header search for an organizer's name lists the code in the "Meet
  codes" group with its list and link, under the 20-result rule; the group's link to the whole list carries the
  search. (FR-004)
- **AC-007 Read-only and rights.** In read-only mode the lists and both searches work and no Google port is called;
  `ViewMeetCodes` and the header search's policy keep their allowed roles (Admin, Dean) and forbidden callers,
  each proved by a test (TC-5); the search text reaches no application log and no audit event. (FR-003, FR-004,
  section 7)
- **AC-008 A full page stays one set of queries.** Loading a page of codes reads the organizers' names in a bounded
  number of queries independent of the number of rows; a search by name stays usable at about 1000 students and 300
  teachers. (FR-005)
- **AC-009 One name rule.** For the same stored data, the name shown for an organizer email on the codes page equals
  the name the Meet reports show for that email. (FR-001)

AC-001…AC-008 are the Story's; AC-009 is added from the Story's note "so the list and the search cannot disagree
about who the organizer is" and `trebovaniya.md` Epic 4 «как в отчётах Meet».

## 6. Validation Rules

- **VR-001** The search text keeps US-070 VR-001…VR-004 unchanged (length, refusal, other parameters). This Story
  adds no input.
- **VR-002** A name read from storage is output only and never parsed; any characters it holds are rendered as text
  by Razor encoding.
- **VR-003** An email or name used inside a search is never concatenated into SQL; the word stays a bound parameter
  (`SearchMatch.WordHolder`), as today.

## 7. Security Requirements

- **Authorization unchanged, deny by default.** The Meet codes page under `ViewMeetCodes` (Admin, Dean); row actions
  under `LinkMeetCodes`; the header search under its existing policy. No anonymous access added (SC-4).
- **Roles.** Owner has no installation account; Teacher and Student have none (Hard Stops). No visibility scoping
  by roster.
- **Personal data.** Teacher names are personal data already shown to the same roles in the Meet reports and the
  teacher list; no new data is collected or stored. The name and the search text are never written to an application
  log (SC-10, `trebovaniya.md` §5 «Только чтение») and no audit event is added (viewing is not audited).
- **Erasure and exclusion** stay effective: no organizer name is derived from anything but a non-null stored
  organizer email (FR-001), so an erased or excluded person cannot reappear through a name.
- **Read-only mode.** The changed reads are views; they run in read-only mode and call no Google port (AD-6, BR-026).
- **No Google call, no outbound data** (SC-13): names come from stored participants only.
- **Encoding.** Razor encoding for name and email (VR-002).

## 8. Error Handling

No new error. A refused search text keeps US-070 VR-003. A database failure keeps the existing error page.
An organizer email with no matching participant is not an error: the email alone is shown (AC-002).

## 9. Non-Functional Requirements

- Expected volume: about 1000 students and 300 teachers (`trebovaniya.md` §5). FR-005 bounds the queries per page.
- UI languages: no new user-visible string; nothing to add to `Application.Localization` (NFR-073).
- Restrained display: name and email as in the Meet reports; no new emphasis.

## 10. Out of Scope

- The Meet reports and their exports — they already show the name.
- The wording of the search field — it already says "organizer".
- The course-choice page (`/workspace/meet-codes/{code}/course-choice`).
- Former names: search does not find them (BR-085).
- Any correction or editing of a name (Google data is a mirror, v83).
- Showing organizers in the header search's result rows.
- Searching the accounts that linked, confirmed or marked a code (US-070 FR-004, unchanged).

## 11. Open Decisions

None open. The Story carries no `OD-` items; the Owner decided on 2026-11-04: name with the email beside it, all
three lists, search by name both on the page and in the header. `trebovaniya.md` section 7 holds no open item on
this subject. Interpretations needing the Owner's confirmation at the gate are listed in section 13 (I-1…I-3).

## 12. Traceability

| AC | Story AC | FR / VR |
|---|---|---|
| AC-001 | AC-001 | FR-001, FR-002, VR-002 |
| AC-002 | AC-002 | FR-001, FR-002 |
| AC-003 | AC-003 | FR-001, FR-002 |
| AC-004 | AC-004 | FR-001, FR-003 |
| AC-005 | AC-005 | FR-001, FR-003, VR-001, VR-003 |
| AC-006 | AC-006 | FR-004 |
| AC-007 | AC-007 | FR-003, FR-004, section 7 |
| AC-008 | AC-008 | FR-005 |
| AC-009 | Story note, Epic 4 «как в отчётах Meet» | FR-001 |

## 13. Interpretations

- **I-1 Case-only duplicates are one organizer.** The Story says "each appears once"; the program compares a Meet
  email to a participant's email case-insensitively, so two spellings of one email would otherwise show the same
  name twice. They are merged; the ordinally first stored spelling is shown. In practice Google returns one spelling.
- **I-2 Words across organizers.** A two-word search may match through two different organizers of one code, as two
  words may already match two organizer emails today (US-070 FR-001 rule 2 treats each organizer value as a field of
  the code). Restricting a search to one organizer would change the shared rule.
- **I-3 Only the shown name is searched.** When several participants share one email, only the chosen name (lowest
  id with a name) is searched, since BR-087 searches an organizer's name "only where the screen shows" it. `Surname`
  and `GivenName` are not searched for organizers, unlike the teacher list: the codes page shows `FullName`.
