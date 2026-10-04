---
artifact_type: api_design
story: US-025
version: 1
status: DRAFT
created_at: 2026-10-04T09:41:30Z
updated_at: 2026-10-04T10:05:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-025-spec.md
    version: 1
  - path: docs/decisions/US-025-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 83
  - path: docs/designs/api/US-008-openapi.yaml
    version: 1
  - path: docs/designs/api/US-019-openapi.yaml
    version: 1
supersedes: null
---

# US-025 API Design — Journal view for a period

Companion to `docs/designs/api/US-025-openapi.yaml`.

## 1. Scope of the contract

**One new** page on the installation's public port, and one existing page changed
additively:

| Operation | Purpose | Calls Google | Writes |
|---|---|---|---|
| `GET /workspace/journal` (new) | the selection form and the journal of one course for a period | no | nothing |
| `GET /` (US-008) | gains a "Journal" navigation entry for Admin and Dean | no | nothing |

No `/api/v1` path, no `POST`, no Control Plane change, no private-port change;
`ClassroomAgent.Contracts` gains nothing and `ContractVersion.Current` stays
**1**. The time zone setting of spec FR-010 is configuration, not HTTP, and
changes no contract.

## 2. Decisions this stage made

### 2.1 One path in the Google Workspace section, the form submits to itself by `GET`

`/workspace/journal`. `trebovaniya.md` v83 (§4, "Направление развития") divides
the program into sections, and every first-version screen with teaching data
belongs to the **Google Workspace** section; later sections (curricula,
timetable, attendance, the integrated journal) sit beside it. The `/workspace`
prefix is that section, chosen now so the URL — and users' bookmarks — need not
change when the other sections arrive (Owner's decision, 2026-10-04). It is
outside `/settings`, the Admin's section, because the matrix row is granted to
both roles (as US-019 §2.1 reasoned). Later Google Workspace screens (US-020
course list, EPIC-4 Meet reports) are expected under the same prefix.

The selection form is `method="get" action="/workspace/journal"`, so a journal
is a bookmarkable, reloadable URL with no resubmit prompt, and US-020 can later
link straight to `/workspace/journal?courseId=…` (OD-001 a). The query carries
the internal course id, two dates and the view — no personal data (spec S-05).

### 2.2 Parameter names

`courseId`, `from`, `to`, `view`. `from`/`to` take `yyyy-MM-dd`, the value an
HTML `<input type="date">` submits, so the browser's date picker needs no script.

### 2.3 Validation answers with the journal page itself

A malformed or inverted query answers `400`, an unknown course `404` (API-5) —
but the body is **this page** with the form and the message, never the host's
error page (spec FR-008, Story AC-011).

The host has `UseStatusCodePagesWithReExecute("/error/{0}")`. That middleware
replaces only an empty-bodied response, so a rendered view with status `400` or
`404` is left alone; TEST_WRITING asserts that the body of each is the journal
form with the message, not the error page, so a later change to the middleware
cannot silently swap it.

Validation collects **all** shape failures in parameter order (`courseId`,
`from`, `to`, `view`, then the pair) so the user sees every problem at once. The
course lookup runs only when the shape is valid; hence a query that is both
malformed and names an unknown course answers `400`.

A malformed value is **not echoed**: its field is redisplayed empty (`from` /
`to` null, no course selected, `view` falls back to `full`). Valid fields keep
their values. Unknown parameters are ignored and never echoed (spec VR-005).

### 2.4 The full / short switch

When a journal is shown, the switch is two links — "full" and "short" — each a
`GET /workspace/journal` with the effective `courseId`, `from`, `to` and the other view; the
current one is marked. The form carries the current view as a hidden field so a
change of course or dates keeps it. It is all `GET`; nothing is stored about the
choice (OD-003 a).

### 2.5 The view model is flat and closed

`JournalPageModel` (contract components) is built in `Application` and contains
no entity (AD-8). Cells are a closed `JournalCellState` plus optional numbers and
dates; the page turns each state into a translation key `Journal.Cell.<State>`.
Rows carry **no participant id** and columns **no item id**: the screen does not
need them, and leaving them out keeps internal identifiers of people out of the
markup.

Numbers are carried as decimals and formatted in the view for the UI language
with insignificant trailing zeros dropped (spec FR-006). Dates are carried as
calendar dates already converted to the school's time zone in `Application`, so
the view never converts time.

### 2.6 Policy name

`ViewJournal` — its own policy for the matrix row "Просмотр и экспорт журнала
успеваемости" (spec FR-012), granted to Admin and Dean. US-028 / US-029 reuse
it.

### 2.7 Where the read-only guard is not

The query does not call `IReadOnlyModeGuard` (spec FR-013): viewing is permitted
in every mode, and calling the guard would make a read depend on legitimacy
state. TEST_WRITING proves the absence of writes and Google calls rather than
the presence of a guard.

### 2.8 Caching

No `Cache-Control` header is specified: no convention defines one, and this stage
does not invent a security rule. Recorded as a non-blocking finding (§8).

## 3. Operation notes

### `GET /workspace/journal`

- Order of evaluation, each step observable in tests: authentication (`302
  /sign-in`) → restricted session of a Dean with a temporary password (`302
  /sign-in/change-password`) → authorization (`ViewJournal`) → query shape
  (`400`, page) → course existence (`404`, page) → build (`200`).
- B, the moment the journal is built, is read once from the injected clock
  (`TimeProvider`) per request (spec FR-006). The default period (spec FR-007)
  is derived from the same B in the school's time zone.
- No `courseId`: `200`, form only, default period, `journal` null.
- No course stored: `200`, `noCoursesStored` true; a `courseId` given anyway is
  `404` `CourseUnknown`.
- Empty journal: `200`, `emptyStateKey` `NoColumns` or `NoRows`, `journal` null
  (spec FR-009).
- An Admin and a Dean are both allowed; there is no forbidden *signed-in* role in
  v1. The forbidden-role tests are the anonymous visitor and the restricted
  session (spec S-02).

### `GET /`

- Additive markup only: a "Google Workspace" heading with a "Journal" link to
  `/workspace/journal` beneath it, outside the Admin-only settings block, for both
  roles, in read-only mode too (spec FR-011; v83 sections). Policy,
  status codes and view model unchanged.

## 4. Authentication and authorization model

| Operation | Anonymous | Dean (temporary password) | Dean | Admin | Antiforgery |
|---|---|---|---|---|---|
| `GET /workspace/journal` | `302 /sign-in` | `302 /sign-in/change-password` | `200` / `400` / `404` | `200` / `400` / `404` | — (GET) |
| `GET /` | unchanged | unchanged | unchanged | unchanged | — |

The SC-4 anonymous list and the antiforgery exemption list are unchanged.

## 5. Error model

All responses are HTML; there is no API-6 JSON body in this Story.

| Status | When | Body |
|---|---|---|
| `200` | form only, journal, empty journal, no course stored | the journal page |
| `302` | not signed in; restricted session | redirect |
| `400` | `CourseMalformed`, `FromMalformed`, `ToMalformed`, `ViewUnknown`, `PeriodInverted` | the journal page with `messageKeys` |
| `404` | `CourseUnknown` | the journal page with `messageKeys` |
| `500` | unexpected failure | the single error page, no detail (API-10, SC-10) |

There is no `403` for a signed-in user (both roles are granted) and no `409`
(nothing is refused in read-only mode).

## 6. Acceptance Criterion → operation map

| AC | Operation(s) | Evidence in the contract |
|---|---|---|
| AC-001 | `GET /workspace/journal` | `200` case 2; `Journal.columns`, `rows`, `cells` |
| AC-002 | `GET /workspace/journal` | `JournalRow` set per spec FR-005 |
| AC-003 | `GET /workspace/journal` | `JournalCellState` graded members, `points`/`maxPoints`, `late` |
| AC-004 | `GET /workspace/journal` | `NotAssigned` |
| AC-005 | `GET /workspace/journal` | `view`; `draftPoints` null in short view |
| AC-006 | `GET /workspace/journal` | `Unrecognised` + `rawState`; no `points` |
| AC-007 | `GET /workspace/journal` | `JournalColumn.kind`; ungraded members; `Empty` for materials |
| AC-008 | `GET /workspace/journal` | `nameKind: Unnamed`, ordered last |
| AC-009 | `GET /workspace/journal`, `GET /` | `x-read-only-mode`, `x-writes: nothing`, `x-calls-google: false` |
| AC-010 | `GET /workspace/journal` | `ViewJournal`; `302 /sign-in`; restricted session |
| AC-011 | `GET /workspace/journal` | `400` / `404` with `JournalMessageKey`; page, not error page |
| AC-012 | `GET /workspace/journal`, `GET /` | every label by translation key; Google strings untranslated |
| AC-013 | `GET /workspace/journal` | `x-calls-google: false`; data from PostgreSQL only |
| AC-014 | `GET /workspace/journal` | `from`/`to` in the school's time zone; `JournalColumn.date` |
| AC-015 | — | configuration (spec FR-010); no HTTP surface |

## 7. Compatibility

- Additive only: one new path; the home page gains a link.
- The US-008 endpoint enumeration test must classify `GET /workspace/journal` as protected
  and not anonymous.

## 8. Open questions for later stages

None blocking. Notes:

- **DB_DESIGN** names the read port (`IJournalSource`, spec FR-014), its fixed
  set of queries and the indexes behind them, and whether a migration is needed.
- **TEST_WRITING** asserts the evaluation order of §3, the `400` / `404` bodies
  being the journal page (§2.3), that a malformed value is not echoed, that
  unknown parameters are ignored, and that read-only mode answers `200` with no
  write and no Google port call for all three BR-025 causes.
- **Non-blocking — caching.** The journal page carries students' personal data.
  No convention sets `Cache-Control` for such pages; whether to send `no-store`
  on pages with personal data is for the Owner / SECURITY_REVIEW to decide, not
  this Story to invent.
