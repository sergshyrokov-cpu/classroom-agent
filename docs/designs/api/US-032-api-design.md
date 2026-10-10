---
artifact_type: api_design
story: US-032
version: 1
status: DRAFT
created_at: 2026-10-10T13:42:52Z
updated_at: 2026-10-10T13:42:52Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-032-spec.md
    version: 1
  - path: docs/decisions/US-032-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 88
  - path: docs/designs/api/US-012-api-design.md
    version: 1
  - path: docs/designs/api/US-025-api-design.md
    version: 1
  - path: docs/designs/api/US-027-api-design.md
    version: 1
  - path: docs/designs/api/US-031-api-design.md
    version: 1
supersedes: null
---

# US-032 API Design — Meet meeting codes linked to courses

Companion to `docs/designs/api/US-032-openapi.yaml`.

Delegation: none at this stage — the earlier contracts (US-012, US-025, US-027,
US-031), `SignInRoutes` and `InstallationPolicies` were read directly. The code
facts gathered by `quick-look` at SPECIFICATION were reused.

## 1. Scope of the contract

Five new operations on the installation's public port, two existing ones changed
additively:

| Operation | Purpose | Policy | Writes |
|---|---|---|---|
| `GET /workspace/meet-codes` | the page, one list at a time, paginated | `ViewMeetCodes` | nothing |
| `GET /workspace/meet-codes/{meetingCode}/course-choice` | course-choice form | `LinkMeetCodes` | nothing |
| `POST /workspace/meet-codes/{meetingCode}/link` | pick / re-link / remove the mark | `LinkMeetCodes` | link + audit |
| `POST /workspace/meet-codes/{meetingCode}/confirmation` | confirm an automatic link | `LinkMeetCodes` | link + audit |
| `POST /workspace/meet-codes/{meetingCode}/not-a-course-mark` | mark "not a course" | `LinkMeetCodes` | link + audit |
| `GET /` (US-008) | gains "Meet meetings" entry | unchanged | nothing |
| `GET /settings/workspace-connection` | `failedStep` may be `Linking` | unchanged | nothing |

No `/api/v1` path, no Control Plane change, no private-port change;
`ContractVersion.Current` stays **1**. No operation calls Google. The automatic
linking step (spec FR-006) and the purge (FR-016) have no HTTP surface; the
thresholds (FR-005) are configuration.

## 2. Decisions this stage made

### 2.1 Path `/workspace/meet-codes`

The page belongs to the Google Workspace section, whose screens live under
`/workspace` (US-025 §2.1: "EPIC-4 Meet reports are expected under the same
prefix"). The resource is the meeting code, so the noun is `meet-codes`.
US-033's reports will sit beside it (e.g. `/workspace/meet-reports`).

Note for the record: spec FR-007 says the section's "other entries stay
Admin-only". The `/workspace` section's existing entry (Journal) is open to both
roles; the Admin-only pages are under `/settings`. Nothing in this contract
depends on the sentence — the policies below are per operation — so it is
recorded here and as a non-blocking finding, not as a blocker.

### 2.2 One list at a time, chosen by `list`

Three separately paginated lists on one page would need three pairs of paging
parameters, departing from API-8's `page` / `size`. Instead the page shows one
list, chosen by `list=unassigned|linked|not-a-course` (default `unassigned`),
with the sizes of all three in a list switcher. The URL stays bookmarkable and
API-8 holds unchanged. Spec FR-007 ("three lists, each paginated separately, each
with its own empty state") is met: each list has its own pagination and empty
state.

### 2.3 Three writes, three nouns, no verbs (API-3)

- **`…/link`** — the code's link to a course. Picking for an unassigned code,
  re-linking and removing the mark all end in the same state ("linked to C, by
  me, now, unconfirmed"); they differ only in the starting state, which the
  request already carries (`expectedState`). One use case, three audit actions
  chosen from the verified current state.
- **`…/confirmation`** — creates the confirmation.
- **`…/not-a-course-mark`** — creates the mark.

HTML forms cannot send `PUT`/`DELETE`; `POST` with nouns follows US-012 §2.1 and
US-027 §2.4. No `DELETE` exists: there is no plain unlink (OD-003, BR-083).

### 2.4 Expected state in every write

Spec FR-013 makes every action carry the state the person saw. The forms send
`expectedState` (`unassigned` / `linked` / `marked`) and, for `linked`,
`expectedCourseId`. A match is checked inside the write's transaction; a
mismatch is `409` with the page re-rendered on the list where the code now is,
message `StateChanged` (as US-012 §2.8: "two kinds of `409`, one status").
Confirmation additionally requires the link to be automatic and unconfirmed —
otherwise also `StateChanged`, since the page offered "Confirm" only for such a
link (`canConfirm`).

### 2.5 Evaluation order: read-only guard first

As US-012 and US-027: authentication → restricted session → policy →
antiforgery → **guard** → shape → existence → state → same-course → write.
Consequences for TEST_WRITING:

- A read-only school is told "read-only" even for a malformed or unknown code
  or a stale screen.
- The refused audit row (spec FR-014, I-11) records the code when it passes its
  shape, else none; its action is derived from `expectedState` when that parses
  (`unassigned` → `MeetCodeCoursePicked`, `linked` → `MeetCodeRelinked`, `marked`
  → `MeetCodeMarkRemoved` on `…/link`; `MeetCodeLinkConfirmed` and
  `MeetCodeMarkedNotACourse` on their own paths), else `MeetCodeCoursePicked`
  for `…/link`. Course ids are not recorded on a refused row: they are
  unverified input.
- A `400`, `404` or stale `409` writes no audit row (spec FR-014).

The two `GET`s do not call the guard and work in read-only mode.

### 2.6 The course options are not paginated (deviation recorded)

OD-004 offers **any** stored course in the choice form, candidates first. That is
a form control over the school's courses — hundreds at most — not a browsed
collection; paginating a choice would hide options. Recorded as
`x-pagination-deviation`, as US-012 §2.10 did for the Dean list. Each option
carries name and section (`CourseOption`, US-025), so courses with equal names
can be told apart.

### 2.7 Status codes

| Situation | Status | Body |
|---|---|---|
| Write succeeded | `302` → `/workspace/meet-codes?list=<origin list>&page=<returnPage>` | TempData `MeetCodeMessage`: `CoursePicked` / `Relinked` / `MarkRemoved` / `Confirmed` / `Marked` (PRG, as US-012 §2.2) |
| Malformed code or form field | `400` | error page `Error.PageExpired` (US-027 §2.5; existing text) |
| Re-link to the current course | `400` | choice form, field error `SameCourse` (spec VR-002) |
| `list` / `page` / `size` invalid | `400` | the page, Unassigned at defaults, message `QueryInvalid` (as US-025 §2.3) |
| Unknown code | `404` | the page, Unassigned, message `CodeNotFound` |
| Unknown course on `…/link` | `404` | choice form, field error `CourseNotFound` |
| Read-only, any write | `409` | host error page naming the BR-025 reason |
| Stale state | `409` | the page on the code's current list, message `StateChanged` |
| Not signed in | `302` → `/sign-in` | — |

The origin list is the one `expectedState` names. `returnPage` keeps the user's
place; a list that shrank simply shows a later page empty (`200`). `returnPage`
is a navigation aid: a malformed value becomes 0 rather than an error.

### 2.8 Form fields bound as strings

`MeetCodeLinkForm`, `MeetCodeConfirmationForm`, `MeetCodeMarkForm` live in
`Application/Models/Requests`; every field is a string validated in
`Application` (US-027 §2.7: MVC enum binding accepts numbers and ignores case).
`expectedState` is case-sensitive; `expectedCourseId` must be present exactly
when `expectedState` is `linked`; `marked` is not a valid state for the mark
form.

### 2.9 What the view models carry

- Codes, emails and course names as stored, never translated (spec FR-017).
- Accounts as `AccountLabel` — the email, or `deleted` (spec I-7). The `AppUser`
  internal id is not exposed.
- Shares as whole percents rounded down; dates in the school's time zone.
- No `MeetingCodeLink` internal id: the code is the key in every path.
- `readOnly` lets the page disable controls; enforcement stays in `Application`.

### 2.10 Policies

`ViewMeetCodes` (the page) and `LinkMeetCodes` (the form and the writes), both
granted to Admin and Dean, added to `InstallationPolicies` beside `ViewJournal`
(spec §7: one policy for viewing, one for the actions). Opening the choice form
is under `LinkMeetCodes`: it belongs to the action, as US-027 §2.10.

### 2.11 Language switcher

The page's return path is built from validated `list`, `page`, `size` only; the
choice form's from its path and validated `returnPage`. No personal data in a
query string (spec I-14; the code is in the path).

## 3. Operation notes

### `GET /workspace/meet-codes`
Reads only. Shares come from spec FR-003 over current data (spec I-8); whether
DB_DESIGN stores scores or computes them per page is its call, as long as the
values equal FR-003's. Ordering per the contract; the "no candidates" group
comes after the codes with candidates across pages, not within a page.

### `GET …/course-choice`
The code's current state decides the heading and the hidden `expectedState`;
for a linked code the current course is shown and excluded from the options.

### `POST …/link`, `…/confirmation`, `…/not-a-course-mark`
Order in §2.5. The link change and its audit row are one transaction; the unique
code makes a concurrent second insert fail, which the use case answers as stale
(`409`), not `500` (spec FR-013).

## 4. Authentication and authorization model

Cookie session of US-008 (`installationSession`). Every operation declares a
policy; both new policies admit Admin and Dean, so TC-5's forbidden case for each
operation is the unauthenticated request (`302` to `/sign-in`), plus — for
completeness — a Dean on the restricted (temporary-password) session, redirected
to `/sign-in/change-password`. The SC-4 anonymous list is unchanged. Every `POST`
requires the antiforgery token.

## 5. Error model

Server-rendered pages only, so the API-6 JSON body does not apply; errors are
translated pages (API-6 last paragraph, US-040). Messages: `MeetCodes.Message.*`
and `MeetCodes.FieldError.*` in `SharedResource` (uk, en); the malformed case
reuses `Error.PageExpired`; read-only reuses the existing read-only texts. No
message contains a code, email or course name supplied by the request.

## 6. Acceptance Criterion → operation map

| AC | Operation(s) |
|---|---|
| AC-001 | linking step (no HTTP); `getMeetCodes` (`list=linked` shows it; `madeAutomatically`) |
| AC-002 | linking step / FR-003 (no HTTP); `getMeetCodes` shares |
| AC-003 | `getMeetCodes` (Unassigned with candidates and shares) |
| AC-004 | linking step (no HTTP) |
| AC-005 | linking step (no HTTP) |
| AC-006 | configuration (no HTTP) |
| AC-007 | linking step; `getWorkspaceConnectionSettings` unchanged unless the step itself fails |
| AC-008 | `getMeetCodeCourseChoice`, `setMeetCodeCourse` (`unassigned`) |
| AC-009 | `confirmMeetCodeLink` |
| AC-010 | `getMeetCodeCourseChoice`, `setMeetCodeCourse` (`linked`) |
| AC-011 | `markMeetCodeNotACourse`; `getMeetCodes` (`list=not-a-course`) |
| AC-012 | `getMeetCodeCourseChoice`, `setMeetCodeCourse` (`marked`) |
| AC-013 | `getMeetCodes` (empty `candidates`, ordering) |
| AC-014 | all five new operations (Admin, Dean allowed; unauthenticated refused); `getLanding` |
| AC-015 | the three `POST`s (`409`, refused row); the two `GET`s work |
| AC-016 | purge (no HTTP) |
| AC-017 | all pages (translated keys; data shown as stored) |
| AC-018 | no operation calls Google |

## 7. Compatibility

- Additive only. `getLanding` gains a link; `LastSynchronizationView.failedStep`
  gains the value `Linking` (shown with a new translated step name).
- No existing path, policy, status code or parameter changes.

## 8. Open questions for later stages

- **DB_DESIGN**: storage of the mark (spec Story Notes), stored vs computed
  shares (§3), audit columns for code and course ids, indexes on code and course.
- **TEST_WRITING**: the stale-state `409` for each write, including the unique-
  code race (§3); the refused-row action derivation (§2.5); `returnPage`
  fallback.
- None blocks this contract.
