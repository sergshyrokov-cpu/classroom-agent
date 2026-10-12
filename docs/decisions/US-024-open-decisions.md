---
artifact_type: open_decisions
story: US-024
version: 2
status: DRAFT
created_at: 2026-10-12T12:42:03Z
updated_at: 2026-10-12T12:51:16Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-024-database-statistics.md
    version: null
  - path: trebovaniya.md
    version: 90
supersedes: null
---

# US-024 Open Decisions — Database statistics and last synchronization

Ten decisions (OD-001 … OD-010) were written into the Story by the author and
**resolved by the Owner on 2026-10-12, before activation** (US-024 discovery),
each as recommended. They are carried here with their ids and resolutions
unchanged.

Writing the Specification raised **one new Open Decision, OD-011**, which is
resolved by the Owner at `HUMAN_SPEC_APPROVAL` on 2026-10-12: option (a). Other choices the Story and `trebovaniya.md` leave open
are stated as interpretations I-1 … I-8 in `docs/specifications/US-024-spec.md`
§11.

No item of `trebovaniya.md` §7 (v90) blocks this Story. The open items are 10
(minimum Google Workspace roles), 14 (submissions of a removed student), 26
(documentation), 27 (re-import of a purged leaver) and 28 (Meet `call_ended`
fields). Items 14 and 27 may change which rows exist, not how they are counted;
item 28 may change how meetings are stored, not that each stored meeting is one
row. None changes this Story's behaviour.

---

## Carried from the Story

### OD-001 Page

**Resolution:** a separate "Database" page; its home-page section is last among
the sections both roles see. Owner, 2026-10-12.

### OD-002 Counters

**Resolution:** courses by status and in total, teachers, students, coursework,
submissions, Meet meetings, Meet codes awaiting a decision. No application
accounts or templates. Owner, 2026-10-12.

### OD-003 People

**Resolution:** counted by current roster membership, teachers and students
separately (the US-022 rule); people who left every course are not counted.
Owner, 2026-10-12.

### OD-004 Synchronization block

**Resolution:** the last successful run, the current state, the last full run
and the Meet loaded-up-to time, to both roles; not the processed counter.
Owner, 2026-10-12.

### OD-005 Failed run

**Resolution:** "failed" without a cause; a hint for the Dean, a link to the
connection page for the Admin. Owner, 2026-10-12.

### OD-006 Button

**Resolution:** a "Synchronize" button on this page for both roles; the existing
buttons are unchanged. Owner, 2026-10-12.

### OD-007 Read-only mode

**Resolution:** the page works; the button behaves as in US-019. Owner,
2026-10-12.

### OD-008 Refresh

**Resolution:** no auto-refresh. Owner, 2026-10-12.

### OD-009 Disk size

**Resolution:** no database size on disk. Owner, 2026-10-12.

### OD-010 Export

**Resolution:** no export. Owner, 2026-10-12.

---

## Raised by the Specification

### OD-011 How the press returns to the Database page — RESOLVED

**Context.** The Story asks that the button make "the same request as US-019:
the same entry point" and that "after the press the user returns to this page",
and puts "any change … to the US-019 request" out of scope. The US-019 endpoint
(`POST /synchronization/requests`) chooses the return page on the server from
the role — Dean → home page, Admin → connection page — and takes no return value
from the request, so it cannot become an open redirect (US-019 VR-002). Both
Story sentences cannot hold literally.

**Options.**

- **(a) A second endpoint for the Database page — recommended.** A new `POST`
  (route fixed by API_DESIGN), policy `StartSynchronization`, calls the same
  `RequestSynchronizationUseCase` and always returns to the Database page; a
  connection refusal re-renders the Database page. "The same entry point" is read
  as the same Application use case: same guard, same audit row, same messages.
  The US-019 endpoint and its tests are untouched; the return page is still fixed
  by the server. Cost: one more protected endpoint with its own role and
  antiforgery tests.
- **(b) An origin field on the US-019 endpoint.** The forms send a value from a
  closed list (`database`, or nothing); anything else falls back to the role page.
  One endpoint, but the US-019 endpoint, its VR-002 and its tests change, against
  the Story's "no change to the US-019 request".
- **(c) Keep the US-019 return.** The press on the Database page lands the Dean
  on the home page and the Admin on the connection page, with the US-019 message.
  Nothing changes anywhere, but OD-006 and AC-008 ("back on this page") are not
  met.

**Impact.** API_DESIGN cannot fix the press endpoint, its return page or VR-002
until this is resolved. Affects FR-007, VR-002, AC-008, AC-016, S-07.

**Resolution:** option (a) — a second `POST` endpoint for the Database page,
policy `StartSynchronization`, calling the same `RequestSynchronizationUseCase`
and always returning to the Database page; the US-019 endpoint is unchanged.
Owner (Serhii SHYROKOV), via `/so:approve` comment "OD-011: a", 2026-10-12T12:51:16Z.
