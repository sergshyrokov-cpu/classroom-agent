---
artifact_type: specification
story: US-065
version: 2
status: APPROVED
created_at: 2026-10-28T11:05:46Z
updated_at: 2026-10-28T11:09:40Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-065-erasure-exclusion-by-email-in-advance.md
    version: null
  - path: trebovaniya.md
    version: 104
  - path: docs/specifications/US-038-spec.md
    version: 1
  - path: docs/decisions/US-065-open-decisions.md
    version: 2
  - path: docs/designs/database/US-038-db-design.md
    version: null
supersedes: null
---

# US-065 Specification — An Admin enters an erasure exclusion by email before the person is loaded

## 1. Overview

This Story adds to the erasure page of US-038 a third action: entering an
exclusion by email for a person the program has not loaded
(`trebovaniya.md` v104 §2 row "… и внесение исключения по email", §3
`ErasedParticipant` v104, §4 Epic 10 «Исключение по email заранее», §5 audit,
§9 «Перевод школы на другой домен» step 4; BR-021, BR-076, SC-11, DC-8).

Five properties shape everything below.

- **Only a fingerprint is stored.** The exclusion holds the salted fingerprint
  of the email (US-038 FR-010) and no Google user id until synchronization
  finds the person (§3 v104). No name, no email.
- **Usable on an empty installation.** The page works before the Google
  connection is saved (§4 Epic 10 v104, DC-8 step 4), and entering creates the
  installation's salt when none exists yet.
- **Synchronization matches once, then works by id.** A roster person whose
  email in Google's answer has the fingerprint of an exclusion with no Google
  user id is skipped exactly as an excluded id is skipped today (US-038 FR-012),
  and the exclusion receives that person's Google user id (§3 v104).
- **No race can leave a loaded person behind an exclusion.** Entering is
  serialized with synchronization and Meet writes the way erasure is
  (US-038 FR-011), so a person is either already loaded — and the Admin is sent
  to the ordinary erasure — or never stored.
- **Admin only, never in read-only mode,** audited by the exclusion's id only
  (§2, §5, SC-11, BR-026, AD-6).

Delegated lookups (read only, `Explore`): `RunSynchronizationUseCase`
(per-course transaction order, exclusion lookup, owner clearing, roster and
submitter writers), the Meet page exclusion check, `IErasedParticipantRepository`
and its implementation, `IErasureFence` / `ErasureFence`, `IErasureSaltStore`,
`EmailFingerprint`, `WorkspaceConnection.NormalizeEmail`,
`EraseParticipantUseCase`, `LiftErasureExclusionUseCase`, `ErasureController`
and its routes and view models, `AuditAction`, the erasure resources. Read
directly: the Story, `trebovaniya.md` v104 §2, §3, §4 Epic 10, §7, §9 (domain
move), BR-021, BR-076, SC-11, the US-038 Specification and US-038 db-design §7.

## 2. Business Goal

An erasure on request stays permanent only while the installation keeps its
exclusions. A school that moves to another domain gets a new installation with
an empty database (BR-021, DC-8); without this Story its first synchronization
would load again everyone the school had erased who is still in Google, silently
undoing the school's written requests. The same entry covers a request that
arrives before the program has ever loaded the person (§4 Epic 10 v104).

## 3. Business Flow

### 3.1 Entering

On the erasure page (US-038 FR-001) the Admin finds the block "Exclude by
email", types one address from the school's written request and confirms
(FR-001, FR-002, OD-002). Three outcomes:

- nobody with that email is loaded and no exclusion has that fingerprint → the
  exclusion is stored; the page confirms it (FR-003);
- somebody with that email is loaded → nothing is stored; the page says the
  person is already loaded and lists them with the link to the ordinary erasure
  (FR-004);
- an exclusion with that fingerprint exists → nothing is stored; the page says
  the email is already excluded (FR-005).

### 3.2 On a new installation

During a domain move (DC-8 step 4) the Admin signs in and, before saving the
Google connection, enters every address from the school's erasure requests.
Then the connection is configured and synchronization starts.

### 3.3 Synchronization

When a roster person's email in Google's answer matches an exclusion entered
in advance, synchronization stores nothing of them and writes their Google
user id into the exclusion (FR-006 … FR-008). From then on the exclusion works
by id, even after the address changes. The Meet load already skips by
fingerprint (FR-009).

### 3.4 Lifting

Lifting works as today (US-038 FR-009): the Admin types the same email; the
exclusion is removed whether or not synchronization has already matched it
(FR-010).

## 4. Functional Requirements

### FR-001 The "Exclude by email" block

- A third block on the existing erasure page (US-038 FR-001, route
  `/settings/erasure`), next to search and lifting. One email field and one
  confirm button; a short explanation that the block is for people not yet
  loaded, chiefly for a move to another domain, and that only a fingerprint of
  the address is kept (§4 Epic 10 v104).
- One `POST` endpoint for the entry; the route is API_DESIGN's (indicative:
  `/settings/erasure/exclusions`). The email travels in the body, never in the
  address (US-038 S-09).
- All logic is in an `Application` use case (name indicative,
  `EnterErasureExclusionUseCase`) returning a result DTO; the controller only
  hands over and renders (AD-3, AD-8).
- The page, including this block, opens whether or not a `WorkspaceConnection`
  is saved (AC-002), and calls no Google API.

### FR-002 The entry command

The use case, in order:

1. checks the read-only guard first (FR-012);
2. trims and validates the email (VR-001); invalid → validation outcome,
   nothing stored, no audit event (AC-011);
3. in **one transaction**, serialized with synchronization and Meet writes as
   US-038 erasure is (FR-011):
   1. looks for loaded participants with this email (FR-004) — any → outcome
      "already loaded", nothing stored;
   2. gets or creates the installation's salt (US-038 FR-010 — created here when
      no erasure ever happened, as erasure does) and computes the fingerprint;
   3. looks for an exclusion with that fingerprint (FR-005) — any → outcome
      "already excluded", nothing stored;
   4. otherwise adds one `ErasedParticipant` with the fingerprint, no Google
      user id and the entry date as its erasure date (FR-003), and the audit
      event (FR-013), and commits.

The loaded check runs before the exclusion check: when both hold (see OD-003),
the ordinary erasure is what the person's data needs.

### FR-003 The exclusion entered in advance

- Exactly: the email fingerprint (never absent for an exclusion entered this
  way), no Google user id, the date of entry (UTC) in the existing erasure-date
  field (I-3). No name, no email, no reference to anything (§3 v104, BR-076).
- After commit the Admin is redirected (PRG) to the erasure page with a one-time
  message "the exclusion was entered" that carries no personal data and travels
  outside the address, as US-038 FR-008 does. The email is not echoed after a
  success.
- `ErasedParticipant.GoogleUserId` becomes optional. An exclusion must still
  hold at least one of the Google user id and the fingerprint (an exclusion with
  neither could never match anyone). Constraints and the migration are
  DB_DESIGN's (PC-2).

### FR-004 A person who is already loaded

- A participant "has the email" when their stored `ClassroomParticipant.Email`,
  normalized (VR-002), equals the entered email normalized the same way. Every
  stored participant counts, on a roster or not, as in the US-038 search.
- When one or more match, the page re-renders (`200`) with the message "this
  person is already loaded — erase their data the ordinary way" and the matching
  participants listed exactly as US-038 search results (name, email, courses
  count with role, link to the confirmation page; US-038 FR-002). Several can
  match because email is not unique (US-038 I-7).
- Nothing is stored, no audit event (AC-006).

### FR-005 An email already excluded

- When any `ErasedParticipant` has the fingerprint of the entered email —
  entered in advance or left by an erasure, matched or not — the page
  re-renders (`200`) with "this email is already excluded". Nothing is stored,
  no audit event (AC-007).
- An exclusion with no fingerprint (US-038 OD-001) can never match an email and
  does not trigger this outcome.

### FR-006 Synchronization matches by fingerprint

In each course's transaction of `RunSynchronizationUseCase` (full and
incremental), at the point where today's excluded Google user ids are looked
up (after the shared fence, before the course row is written; US-038 FR-011,
db-design §7.3–7.4):

- for every roster entry (teachers and students) that has an email in Google's
  answer, the fingerprint of the normalized email is computed with the
  installation's salt; when no salt exists there can be no exclusion entered in
  advance and this step is skipped;
- a roster entry whose fingerprint equals that of an exclusion **with no Google
  user id** is treated as excluded for this course exactly as an excluded id is
  (US-038 FR-012): no `ClassroomParticipant` created or updated, no
  `CourseMembership`, no `Submission` (also as a submitter), not recorded as the
  course's owner when the owner's Google user id is that entry's id;
- the rest of the course is stored as usual; a skipped person is not an error
  and not a failure (US-038 FR-012).

The check is done per course in a bounded number of queries, not per roster
row (NFR-003, US-038 §9).

Exclusions that already hold a Google user id keep working by id only, as
today; synchronization does not additionally match them by fingerprint (I-1).

### FR-007 The one fill of the Google user id

- In the same course transaction, an exclusion matched by FR-006 receives the
  matched entry's Google user id. From then on synchronization skips that
  person by id even if their address changes (§3 v104, AC-004).
- Only an exclusion with no Google user id is filled, and only once. After the
  fill the exclusion is again unchangeable. The immutability rule
  (`ck_erased_participant_immutable` today) changes to allow exactly this one
  change — no Google user id → a Google user id, nothing else; how is
  DB_DESIGN's.
- If the matched Google user id is already held by another exclusion, the
  person is already excluded by id; the exclusion entered in advance is left
  without an id and nothing fails (I-2).
- If two roster entries with different Google user ids give the same
  fingerprint in one course, both are skipped in that course and the exclusion
  receives the Google user id that sorts first ordinally (I-2).
- The fill is a synchronization write, not an audit event (Story Scope); it is
  not one of the BR-026 service writes and never runs in read-only mode, because
  synchronization does not run then (§2).
- The fill writes nothing to the log that identifies the person (SC-10); the
  run's existing counters are unchanged — a skipped person is not counted as a
  failure.

### FR-008 A matched Google id already stored as a participant

When the Google user id of a roster entry matched by FR-006 already belongs to
a stored `ClassroomParticipant` (the person was loaded under another address
and the Admin entered the new one), the match does not apply (OD-003 a):

- the entry is synchronized as usual — participant updated (their stored email
  becomes the one in Google's answer), memberships and submissions stored;
- the exclusion entered in advance is not filled and keeps no Google user id;
- the Admin who enters that address again is told the person is already loaded
  (FR-004) and erases them the ordinary way, which leaves its own exclusion with
  the id;
- known limitation until then: the Meet load already skips that address's
  connections by fingerprint (FR-009), while Classroom data keeps loading.

This keeps the US-038 rule that no stored participant has an excluded Google
user id. Through the serialization of FR-011 this case cannot arise from a
race — only from a stored email that differs from Google's current one.

### FR-009 The Meet load

No change: the Meet load already skips connections and clears the organizer
email by fingerprint, for any exclusion that has one (US-038 FR-012). An
exclusion entered in advance therefore hides that person's Meet connections and
organizer email from the first load (AC-005), before and after its match.

### FR-010 Lifting

No change to US-038 FR-009: lifting by email removes every exclusion with that
fingerprint, entered in advance or not, matched or not, one audit event each.
The next synchronization loads the person as new (AC-008; US-038 I-11 applies).

### FR-011 Serialization with synchronization and the Meet load

The entry's loaded check (FR-004) and its insert happen in one transaction that
excludes concurrent synchronization and Meet write transactions, as US-038
erasure does (db-design §7.3: the exclusive side of the shared fence):

- a run whose course transaction committed first has stored the person, and the
  entry answers "already loaded" (FR-004);
- a run whose course transaction starts after the entry commits sees the
  exclusion at its write (FR-006) and stores nothing of the person, even if it
  read the roster before the entry.

No interleaving may leave a stored participant whose email matches an exclusion
entered in that interleaving (AC-012). The mechanism is DB_DESIGN's; reusing
the erasure fence is the expected one.

### FR-012 Read-only mode

- The entry calls `IReadOnlyModeGuard` first; in any BR-025 cause it is refused
  with `ReadOnlyModeException`, answered `409` by the host's handler, nothing
  stored, and one refused audit event written under the `AuditEvent` service
  write, as for lifting (US-038 FR-013, FR-014, I-12).
- The block is shown with the read-only notice of US-038 I-8 and an inactive
  button; a forced request still gets `409` from `Application` (AD-6).
- A new installation is in read-only mode until its first successful
  legitimacy check (BR-025); entering before the Google connection is saved
  therefore works once that check has succeeded (DC-8 step 4 follows the
  deployment, when it has). No special case is added; while the installation is
  still read-only the entry is refused like every other write.

### FR-013 Audit

- **Entering** — one `AuditEvent`: actor the Admin, target the new exclusion's
  internal id, outcome succeeded (§4 Epic 10 v104, §5, SC-11). New action value
  (indicative `ErasureExclusionEntered`) and its code are DB_DESIGN's.
- **Refusal in read-only mode** — one refused event with the same action, no
  target, category read-only (FR-012).
- Validation refusals and the outcomes "already loaded" / "already excluded"
  are not audited (nothing changed).
- No audit event contains an email, a fingerprint or a Google user id (SC-11).

### FR-014 Authorization

The entry endpoint carries the existing `EraseParticipantData` policy (Admin
only, US-038 FR-015). Dean → `403`; a Dean on the forced password change →
refused as on every Admin page; anonymous → sign-in. The SC-4 anonymous list
gains nothing.

### FR-015 Localization

The block's title, explanation, field label, button, the three messages, the
validation messages and the read-only notice come from the `en`/`uk` resources
(NFR-073). The entered email and the names in the "already loaded" list are
shown as stored, never translated.

## 5. Acceptance Criteria

The Story's eleven criteria, unchanged in meaning; ids are the Story's.

| Id | Criterion | Specified by |
|---|---|---|
| AC-001 | Entry for an email nobody loaded stores one exclusion with the fingerprint and no Google id; page confirms; one audit event with the exclusion id, no email | FR-002, FR-003, FR-013 |
| AC-002 | Works with no `WorkspaceConnection` saved (and no salt yet) | FR-001, FR-002 |
| AC-003 | First sync of a course whose roster has that student (with submissions, grades) stores no participant, membership or submission of theirs; the rest stored; the exclusion now holds their Google id | FR-006, FR-007 |
| AC-004 | After the match, renaming the email in Google does not bring the student back | FR-007 |
| AC-005 | The Meet load skips their connections and stores the meeting they organized without organizer email | FR-009 |
| AC-006 | A loaded participant with that email → nothing stored, no audit, page says "already loaded" and leads to the ordinary erasure | FR-004 |
| AC-007 | An exclusion with that fingerprint exists (in advance or from erasure) → nothing new, page says "already excluded" | FR-005 |
| AC-008 | Lifting by the same email, before or after the match, removes it, audited as today; the next sync loads the person as new | FR-010 |
| AC-009 | Read-only → refused in `Application` (`409`), nothing stored, refusal audited | FR-012, FR-013 |
| AC-010 | Dean → `403`; anonymous → sign-in | FR-014 |
| AC-011 | Empty, over 254 characters or not an email → validation error on the field, nothing stored, value not logged | VR-001 |

Derived from the Specification:

| Id | Criterion | Specified by |
|---|---|---|
| AC-012 | An entry committing between a run's roster read and its course write → the run stores nothing of the person; a run whose course write committed first → the entry answers "already loaded" | FR-011 |
| AC-013 | The course's owner matched by fingerprint on that course's roster → the course is stored without an owner | FR-006 |
| AC-014 | Letter case and surrounding spaces do not matter: in the entry, in the "already loaded" check and in the sync match | VR-002 |
| AC-015 | An exclusion left by an erasure is not filled or changed by a sync; an exclusion entered in advance is filled once and never changed afterwards | FR-007, FR-003 |
| AC-016 | A matched Google id already held by another exclusion → the person is skipped, the exclusion entered in advance stays without id, the run does not fail | FR-007 |
| AC-017 | A roster entry matching an exclusion in advance whose Google id is already stored as a participant → synchronized as usual, the exclusion stays without id; entering that address again answers "already loaded" | FR-008 |
| AC-018 | Entry without a valid antiforgery token is refused; nothing stored | VR-003 |
| AC-019 | Read-only mode: the block shows the notice with an inactive button | FR-012 |
| AC-020 | The block, explanation, messages and validation errors in Ukrainian and English | FR-015 |
| AC-021 | After entering the first exclusion on an installation with no salt, a lifting by the same email finds it (the salt was created and is the one used later) | FR-002, FR-010 |

## 6. Validation Rules

### VR-001 The email to enter

The same rule as US-038 VR-003, so the entry and the lifting behave alike
(OD-001 a): required; trimmed; at most 254 characters; of the form
`local@domain` with exactly one `@`, non-empty parts and no whitespace. Any
domain is accepted (OD-001 a). Any other value → the field's validation
message, `400`, page re-rendered, nothing stored, no audit event. One address
per submission (OD-002 a). The value is never written to a log (SC-10), and
after an invalid submission it is shown back only in the field of that
response. The two use cases share one validation (no second copy of the rule).

### VR-002 Email normalization

`WorkspaceConnection.NormalizeEmail` (trimmed, lower-cased, domain normalized;
it depends on no saved connection) is used for the fingerprint input, for the
"already loaded" comparison and for the roster emails in the sync match, so all
three agree with the erasure, the lifting and the Meet load (US-038 VR-004).

### VR-003 Requests

The entry `POST` requires a valid antiforgery token (API-7); missing or invalid
→ refused, nothing stored, no audit event. The actor and role come from the
authenticated session (SC-1). The return page is fixed by the server.

### VR-004 Data read from Google

Roster emails are validated as today; an entry with no email in Google's answer
is not matched (known limitation, §4 Epic 10 v104).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | The entry endpoint uses the existing Admin-only `EraseParticipantData` policy; deny by default; SC-4 gains nothing. | §2, SC-4, US-038 FR-015 |
| S-02 | Allowed-role (Admin) and forbidden-role (Dean, Dean on the forced password change, anonymous) tests for the entry endpoint. | SC-1, TC-5 |
| S-03 | No Teacher or Student access; no roster-based scoping. | Hard Stop |
| S-04 | Read-only refusal in `Application`, all three BR-025 causes, tested in `Application`. | §2, SC-5, BR-026, AD-6, TC-5 |
| S-05 | The exclusion stores only the fingerprint (and later the Google id); no email; the salt is handled as US-038 S-05. | §3 v104, BR-076, SC-13 |
| S-06 | Audit carries the exclusion's internal id only; no log line or audit row contains the email, the fingerprint or the Google id. | SC-10, SC-11 |
| S-07 | Antiforgery on the `POST`. | API-7 |
| S-08 | The email travels in the `POST` body; one-time messages carry no personal data. | US-038 S-09, SC-10 |
| S-09 | No write to Google; no new outbound destination. | Hard Stop, SC-13 |
| S-10 | DTOs only in view models. | AD-8 |
| S-11 | No new NuGet package. | AGENTS.md |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous | Redirected to sign-in. |
| Dean, or Dean on the forced password change | `403` / refused as on every Admin page. |
| Email invalid (VR-001) | Page re-rendered with the field message, `400`; nothing stored. |
| Already loaded (FR-004) | Page with the message and the matching participants, `200`; nothing stored. |
| Already excluded (FR-005) | Page with the message, `200`; nothing stored. |
| Read-only mode | `409` with the reason; refusal audited (FR-012). |
| Failure inside the transaction | Rolled back, nothing stored; the generic error page with no detail (AD-9, SC-10). |
| Missing antiforgery token | Refused per API-7. |
| Failure of the fill inside a course transaction | Handled as any failure of that course's transaction today; this Story adds no new failure handling to synchronization. |

## 9. Non-Functional Requirements

- **Atomicity** — the entry is one transaction; the match, the skip and the
  fill are part of the course's transaction (AD-7).
- **Bounded queries** — the sync match is per course, a fixed number of
  queries (NFR-003). An index on the fingerprint already exists
  (`ix_erased_participant_email_fingerprint`); any further index is DB_DESIGN's.
- **NFR-073** bilingual; **NFR-070** usable on a phone; **NFR-062** .NET 10.
- **Tests** — real PostgreSQL via Testcontainers; the Google ports substituted
  with synthetic rosters and Meet events (TC-2, TC-4).

## 10. Out of Scope

- A person whose email Google did not return (a submitter found only by
  submissions, an account without a visible address) — known limitation (§4
  Epic 10 v104); the Admin erases them the ordinary way after synchronization.
- A course owner who is on no roster of the course being read: the owner is
  known only by Google id, so the match cannot see them; once the person is
  matched in another course, the owner is cleared at the course's next read
  (US-038 FR-012) (I-4).
- Carrying exclusions over from the previous installation's database (v104
  agenda item 2).
- A list of exclusions on the page (US-038 I-14).
- A list of addresses in one submission (OD-002 a).
- The rest of the domain move — Owner operations in DC-8, no code.
- Any change to the Meet load or to lifting.

## 11. Open Decisions

Full text in `docs/decisions/US-065-open-decisions.md`.

| Id | Status | Impact if not resolved |
|---|---|---|
| OD-001 Which addresses the field accepts | **RESOLVED** 2026-10-28 — a) any domain | none — VR-001 |
| OD-002 One email or a list | **RESOLVED** 2026-10-28 — a) one per submission | none — FR-001, VR-001 |
| OD-003 The matched person is already stored under another address | **RESOLVED** 2026-10-28 — a) do not match a stored Google id | none — FR-008, AC-017 |

**OD-003** — The "already loaded" check (FR-004) compares the stored email. A
person loaded earlier under address A whose address in Google is now B is not
found when the Admin enters B; the exclusion is stored, and at the next sync
the roster entry with B matches it while a `ClassroomParticipant` with that
Google id, its memberships and submissions are already stored. Neither
`trebovaniya.md` nor the Story says what happens. **Resolved by the Owner on
2026-10-28: option a).** The options were:

- **a) (recommended) Do not match a Google id that is already stored.** The
  sync loads the person as usual (their stored email becomes B) and leaves the
  exclusion without an id. The Admin who enters B again is told the person is
  already loaded (FR-004) and erases them the ordinary way; the erasure leaves
  its own exclusion with the id. Keeps the US-038 invariant "no stored
  participant with an excluded Google id" and never freezes personal data
  silently. Until the Admin acts, the Meet load already skips B's connections
  (fingerprint) while Classroom data keeps loading — a known limitation.
- b) Match and fill the id anyway. The sync stops updating the person, but
  their stored data stays until the Admin erases it — and nothing tells the
  Admin. Breaks the US-038 invariant (FR-011 of US-038).

An option that erases the stored person automatically, or shows the Admin a
notice about the match, is new behaviour that needs a `trebovaniya.md` change
first and is not offered here.

`trebovaniya.md` §7 (v104) has no open item this Story depends on: items 14, 26,
27, 28 and 29 concern other matters.

### Interpretations

- **I-1 Fingerprint matching only for exclusions without an id.** The Story and
  §3 v104 describe the fingerprint match for the exclusion entered in advance;
  exclusions created by erasure keep working by id as US-038 defined.
- **I-2 Fill conflicts.** An id already held by another exclusion is not
  written twice (one exclusion per Google id today); two Google ids with one
  fingerprint in one course — both skipped, the ordinally first fills. Both are
  edge cases inside one Workspace domain, where an address belongs to one
  account.
- **I-3 The entry date in the erasure-date field.** No erasure happened; the
  field records when the exclusion was made. Whether to rename it is
  DB_DESIGN's; no screen shows it.
- **I-4 Owner not on the roster.** The owner arrives from Google as an id only;
  without a roster email there is nothing to fingerprint.
- **I-5 "Already loaded" before "already excluded".** When both hold, the
  person's stored data still needs the ordinary erasure.
- **I-6 Serialization reuses the erasure fence.** The Story Notes ask whether
  entry takes the fence; it must, because otherwise a run that read the roster
  before the entry and commits after its loaded check would store a person the
  entry did not see (FR-011).

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-002, FR-003, FR-013 | VR-001, VR-002 | S-05, S-06 |
| AC-002 | FR-001, FR-002 | — | — |
| AC-003 | FR-006, FR-007 | VR-002, VR-004 | — |
| AC-004 | FR-007 | — | — |
| AC-005 | FR-009 | VR-002 | — |
| AC-006 | FR-004 | VR-002 | S-10 |
| AC-007 | FR-005 | VR-002 | — |
| AC-008 | FR-010 | — | S-06 |
| AC-009 | FR-012, FR-013 | — | S-04 |
| AC-010 | FR-014 | VR-003 | S-01, S-02, S-03 |
| AC-011 | FR-002 | VR-001 | S-06, S-08 |
| AC-012 (derived) | FR-011 | — | — |
| AC-013 (derived) | FR-006 | — | — |
| AC-014 (derived) | FR-004, FR-005, FR-006 | VR-002 | — |
| AC-015 (derived) | FR-003, FR-007 | — | S-05 |
| AC-016 (derived) | FR-007 | — | — |
| AC-017 (derived) | FR-008 (OD-003) | — | — |
| AC-018 (derived) | FR-002 | VR-003 | S-07 |
| AC-019 (derived) | FR-012 | — | S-04 |
| AC-020 (derived) | FR-015 | — | — |
| AC-021 (derived) | FR-002, FR-010 | VR-002 | S-05 |
