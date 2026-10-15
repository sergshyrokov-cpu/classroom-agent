---
artifact_type: specification
story: US-038
version: 1
status: APPROVED
created_at: 2026-10-15T05:35:49Z
updated_at: 2026-10-15T05:40:39Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-038-erase-participant.md
    version: null
  - path: trebovaniya.md
    version: 93
  - path: docs/decisions/US-038-open-decisions.md
    version: 2
supersedes: null
---

# US-038 Specification — Erase all data of one participant on request

## 1. Overview

This Story turns the Owner's manual erasure procedure into an Admin function
(`trebovaniya.md` v93 §2 row "Удаление всех данных участника по письменному
запросу школы и снятие исключения" ✔ Admin ✘ Dean; §3 `ErasedParticipant`;
§4 Epic 10; §5 "Удаление данных конкретного человека по запросу"; §9
backups and restore; BR-076, BR-077; SC-11).

Five properties shape everything below.

- **One person, one transaction.** Every record of one `ClassroomParticipant` —
  memberships, submissions with grades, Meet participations — is deleted, and
  the person's mentions in other records (a meeting's organizer email, a
  course's owner) are cleared, in one transaction with the creation of the
  exclusion. Either all of it happens or none of it (Epic 10, AC-002).
- **The exclusion holds no readable personal data.** `ErasedParticipant` keeps
  the Google user id, a salted fingerprint of the email and the erasure date —
  never a name or an email (§3, BR-076). This is the trade-off v93 accepted
  explicitly.
- **The exclusion is enforced where data is written.** Synchronization and the
  Meet load consult the exclusion at the moment they store, so a run already in
  flight when the erasure commits cannot put the person back (Story Notes,
  FR-011).
- **Admin only, never in read-only mode.** The Dean has no access (§2); in
  read-only mode both actions are refused by `Application` with `409` (§2,
  BR-026, AD-6). Viewing the page stays available (§2 "работают просмотр").
- **Nothing personal leaves the screen.** Audit events and the log line carry
  internal ids and counts only (§4 Epic 10, SC-10, SC-11).

Delegated lookups (read only): `trebovaniya.md` v93 header, §2, §3, §4 Epic 10,
§5, §7, §8, §9 (`quick-look`); `business-rules.md` BR-026, BR-072, BR-075,
BR-076, BR-077, `security-conventions.md` SC-4, SC-5, SC-10, SC-11, SC-13,
`persistence-conventions.md` PC-11, `epic-map.md` EPIC-10, `artifact-schema.md`
(`quick-look`); the existing code of `ClassroomParticipant`, `CourseMembership`,
`Submission`, `MeetSession`, `MeetParticipation`, `Course`, their EF
configurations, `RunSynchronizationUseCase` (participants, memberships,
submissions, course owner, Meet load and its 3-day overlap),
`WorkspaceConnection.NormalizeEmail`, `IReadOnlyModeGuard`,
`ReadOnlyModeUnitOfWork`, `PermittedServiceWrites`, `AuditEvent` and its
configuration, `InstallationPolicies`, `DeanAccountsController`,
`Views/Home/Index.cshtml`, `RetentionPurgeStore`, the Data Protection setup and
the localization resources (`Explore`). Read directly: the US-024 Specification
and the US-047 Open Decisions (format).

## 2. Business Goal

The school is the controller of personal data of minors (§5). When it receives
a written request to erase one person's data, it must be able to honour it at
once, itself, without asking the Owner to work in its database and without
first deleting the person from Google — and the person must not reappear with
the next synchronization or Meet load while they remain in Google (§4 Epic 10).

## 3. Business Flow

### 3.1 Finding the person

The Admin opens the "Erasure of personal data" page from the Admin block of the
home page (FR-001). They type an email or a part of a name and search (FR-002).
The page lists matching participants across all courses.

### 3.2 The confirmation

The Admin opens one result. The confirmation page (FR-003) shows the person's
name and email, the courses they are in, the number of submissions, of grades,
of Meet participations and of meetings they organized, a warning that files
already exported cannot be erased by the program, and the mark "erasure is
irreversible". Nothing has changed yet.

### 3.3 Erasure

The Admin sets the mark and confirms. The erasure runs in one transaction
(FR-004 … FR-007); the Admin returns to the search page with a message that the
data was erased (FR-008). One audit event and one log line record it (FR-013).

### 3.4 Afterwards

Synchronization and the Meet load skip the person (FR-010 … FR-012). Journals,
Meet reports and exports built afterwards simply no longer contain them.

### 3.5 Lifting an exclusion

If the person returns to the school, the Admin types their email in the
"Lift an exclusion" form on the same page (FR-009). If an exclusion matches, it
is removed and the next synchronization loads the person as new; otherwise the
page says there is no exclusion for that address.

### 3.6 Read-only mode

The page, the search and the confirmation open as usual with a notice that
erasure and lifting are unavailable; an erasure or a lifting is refused by
`Application` with `409` and the read-only reason (FR-014).

## 4. Functional Requirements

### FR-001 The erasure page and its place in the menu

- One server-rendered page (route indicative, `/erasure`; API_DESIGN fixes the
  routes) holding the search form (FR-002) and the "Lift an exclusion" form
  (FR-009).
- A link to it is added to the Admin-only block of the home page
  (`<nav id="admin-settings">`), so the Dean does not see it (AC-008). The link
  is not enforcement; the policy is (FR-015).
- All page logic is in `Application` queries and use cases returning DTOs
  (AD-8); the host only hands over and renders.

### FR-002 Searching for a participant

- The Admin submits one search term (VR-001). The term is sent in the body of a
  `POST`, never in the address (S-09, I-3).
- A participant matches when the term, compared case-insensitively, is
  contained in their `Email`, `FullName`, `Surname` or `GivenName`. Every stored
  `ClassroomParticipant` is searched, in every course and course state,
  including people no longer on any roster (they still hold data).
- Each result shows the full name (or "no name" when empty), the email (or "no
  email"), and the number of courses with a role (teacher / student) — enough to
  tell two people with the same name apart (OD-011 of US-014: email is not
  unique). Results are ordered by full name, then email, then internal id.
- At most 50 results are shown; when there are more, the page says so and asks
  for a more precise term (I-4).
- No match → "nobody found".
- Each result links to the confirmation page addressed by the participant's
  internal id (FR-003). The search writes nothing.

### FR-003 The confirmation page

`GET`, addressed by the participant's internal id; writes nothing, calls no
Google API, works in every mode. It shows:

- the person's full name and email (§4 Epic 10; the Admin already sees them on
  other pages);
- the courses they are in — the name of each course with a `CourseMembership`
  of this participant, on the roster or not, with the role;
- **submissions** — the number of `Submission` rows of the participant;
- **grades** — the number of those submissions with an `AssignedGrade` or a
  `DraftGrade` (I-5);
- **Meet participations** — the number of `MeetParticipation` rows whose email
  equals the participant's email (FR-006);
- **meetings they organized** — the number of `MeetSession` rows whose
  organizer email equals the participant's email (FR-006);
- **courses they own** — the number of `Course` rows whose owner is the
  participant's Google user id (I-6);
- the warning that files already exported (journals, reports) are outside the
  program and cannot be erased by it (§4 Epic 10);
- when the participant has no email, the notice of OD-001;
- the mark "erasure is irreversible" (unset) and the "Erase" button; in
  read-only mode the notice of FR-014 instead of an active button (I-8).

An unknown or already-erased id → `404` (FR-016).

### FR-004 The erasure command

`POST`, addressed by the participant's internal id, carrying the mark (VR-002).
It runs a use case in `Application` (name indicative,
`EraseParticipantUseCase`) that:

1. checks the read-only guard first (FR-014);
2. validates the mark (VR-002) — unset → validation refusal, nothing changes
   (AC-003);
3. loads the participant — none → `404`;
4. in **one transaction** performs FR-005, FR-006, FR-007 and adds the audit
   event (FR-013); any failure rolls everything back (AC-002);
5. after commit writes the log line (FR-013).

Counts recorded in the audit event and the log are the numbers of rows actually
deleted or cleared in the transaction, which may differ from the confirmation
page if a run stored rows in between.

### FR-005 What is deleted

Inside the transaction, child-first (all foreign keys are `Restrict`, PC-8;
nothing relies on cascades):

- every `MeetParticipation` matched by FR-006;
- every `Submission` with `ParticipantId` = the participant (grades live on the
  submission and go with it, BR-059);
- every `CourseMembership` with `ParticipantId` = the participant, on the roster
  or not;
- the `ClassroomParticipant` row.

Courses, coursework, meetings, Meet code links and other people's rows are not
deleted (§4 Epic 10 "Курсы, задания и встречи остаются").

### FR-006 Matching Meet rows to the person

Meet rows refer to a person only by email (`MeetParticipation.Email`,
`MeetSession.OrganizerEmail`). A row matches when its email, normalized,
equals the participant's email normalized the same way (VR-004). A participant
with no email matches no Meet row (OD-001).

`ClassroomParticipant.Email` is not unique (US-014 OD-011). If two participants
share an email, the Meet rows of that email are erased with whichever of them is
erased first — §4 Epic 10 defines the Meet participation by "his email" (I-7).

### FR-007 What is cleared in other records

In the same transaction:

- every `MeetSession` matched by FR-006 keeps its meeting but loses its
  organizer email (the field becomes empty). The meeting stays in reports as a
  meeting with an unknown organizer;
- every `Course` whose `OwnerGoogleId` equals the participant's Google user id
  keeps the course but loses its owner.

Making `MeetSession.OrganizerEmail` optional is a schema change for DB_DESIGN.
Every existing reader of the organizer email must accept an empty one (I-9).

### FR-008 The exclusion and the result

In the same transaction one `ErasedParticipant` is created with exactly:

- the participant's Google user id;
- the email fingerprint (FR-010) — or none when the participant has no email
  (OD-001);
- the erasure date (UTC).

No name, no email, no reference to the deleted participant row. After commit the
Admin is redirected (PRG) to the erasure page with a one-time message "the
data was erased" that carries no personal data and travels outside the address.

### FR-009 Lifting an exclusion

`POST` from the "Lift an exclusion" form carrying one email (VR-003). A use case
in `Application` (name indicative, `LiftErasureExclusionUseCase`):

1. checks the read-only guard first (FR-014);
2. validates the email (VR-003);
3. computes its fingerprint (FR-010) and removes **every** `ErasedParticipant`
   with that fingerprint, in one transaction with one audit event per removed
   exclusion (FR-013; I-10);
4. none removed → the page says there is no exclusion for that address; nothing
   changes, no audit event;
5. success → PRG back to the page with a one-time message "the exclusion was
   lifted"; the email is not echoed after a success.

The next synchronization then imports the person as a new participant (the
Google user id is no longer excluded); an archived, unchanged course is read
again only at the weekly full run (US-018, I-11). Meet participations from
before the lifting that fall outside the 3-day overlap are not reloaded.

### FR-010 The email fingerprint and the salt

- The fingerprint is a keyed cryptographic hash of the normalized email
  (VR-004) with the installation's salt: HMAC-SHA-256 from the .NET base
  library, no new package (I-2).
- The salt is a random value of 32 bytes from a cryptographic random generator,
  generated once by the installation itself and never changed afterwards —
  changing it would make every exclusion unmatchable. It is not a configuration
  value, not a secret the Owner sets, never shown, never logged, never exported,
  never sent to the Control Plane (Story Notes, SC-13). Where it is stored and
  when it is generated is DB_DESIGN's (Story Notes).
- Two installations generate different salts, so the same email gives different
  fingerprints (AC-006).
- The fingerprint is computed in `Application`/`Domain` code shared by erasure,
  lifting and the Meet load, so all three agree.

### FR-011 Enforcement at the point of writing

The exclusion is consulted inside the transaction that stores the data, not
when the data was read from Google. No interleaving of an erasure and a
synchronization or Meet load may leave stored data that FR-012 forbids — in
particular a `ClassroomParticipant` with an excluded Google user id, or a
`MeetParticipation` whose email fingerprint is excluded. The mechanism (lock,
constraint or isolation) is DB_DESIGN's; it is proven by a test that commits an
erasure between the read and the write of a run (AC-004, AC-005).

### FR-012 What synchronization and the Meet load skip

Synchronization (`RunSynchronizationUseCase`, full and incremental):

- no `ClassroomParticipant` is created or updated for an excluded Google user
  id — neither from a roster nor as a submitter not on the roster;
- no `CourseMembership` and no `Submission` is stored for it;
- a course whose owner is an excluded Google user id is stored without an owner
  (new or updated);
- everyone else in the course is synchronized as usual; a skipped person is not
  an error and is not counted as a failure.

Meet load (including the 3 days it re-reads):

- no `MeetParticipation` is stored whose email's fingerprint matches an
  exclusion;
- a meeting whose organizer email's fingerprint matches an exclusion is stored
  (if it qualifies under the existing domain-organizer rule, evaluated on the
  email as read) without the organizer email; an already stored meeting never
  regains it;
- other participants are stored as usual.

An exclusion without a fingerprint (OD-001) affects synchronization only.

### FR-013 Audit and log

- **Erasure** — one `AuditEvent`: actor the Admin, target the erased
  participant's internal id, outcome succeeded, and the count of each kind:
  memberships, submissions, Meet participations deleted; meetings whose
  organizer was cleared; courses whose owner was cleared (§4 Epic 10 "число
  удалённых записей каждого вида"; I-6). Grades are not a separate kind — they
  are columns of the submissions counted.
- **Lifting** — one `AuditEvent` per removed exclusion: actor the Admin, target
  the exclusion's internal id.
- **Refusal in read-only mode** — one refused `AuditEvent` per refused request,
  category read-only, written under the `AuditEvent` service write, as other
  write use cases do (I-12).
- **Log** — each erasure writes one `Information` line with the participant's
  internal id and the same counts, after commit (§4 Epic 10, BR-077: the Owner
  counts these lines after a restore).
- None of these contains a name, an email, a Google user id or a fingerprint
  (SC-10, SC-11, AC-009). New audit action values, target types and count
  columns are DB_DESIGN's.

### FR-014 Read-only mode

- The erasure and the lifting call `IReadOnlyModeGuard` first; in any of the
  three BR-025 causes they are refused with `ReadOnlyModeException`, answered
  `409` by the host's handler with the reason, and nothing changes but the
  refusal audit (FR-013). Erasure is not added to the BR-026 service writes.
- The page, the search and the confirmation are viewing and keep working
  (§2 "работают просмотр"); they show a notice that erasure and lifting are
  unavailable in read-only mode (I-8). The refusal does not depend on the
  notice (AD-6).
- In read-only mode the Owner's manual procedure applies (§5, DC-8); out of
  scope.

### FR-015 Authorization

A new policy for the §2 row (name indicative, `EraseParticipantData`) granting
Admin only, applied to the page, the search, the confirmation, the erasure and
the lifting. Dean → `403`; anonymous → sign-in. The SC-4 anonymous list gains
nothing.

### FR-016 Unknown ids and concurrent erasure

- A confirmation or erasure for an id with no participant (never existed, or
  erased meanwhile) → `404` with the generic not-found page; nothing changes, no
  audit event.
- Two concurrent erasures of the same participant: one succeeds, the other gets
  `404`; there is never more than one exclusion per erasure.

### FR-017 Retention purge and restore

- The retention purge never touches `ErasedParticipant` (§3 "срок N на него не
  действует"); its row is kept until an Admin lifts it.
- Re-applying erasures after a restore is an organizational step (§9, BR-077);
  no code.

### FR-018 Localization

Every label, message, notice, warning and the mark come from the `en`/`uk`
resources (NFR-073). Names, emails and course names are shown as stored, never
translated.

## 5. Acceptance Criteria

The Story's nine criteria, unchanged in meaning; ids are the Story's.

| Id | Criterion | Specified by |
|---|---|---|
| AC-001 | Confirmation shows name, email, courses, counts of submissions, grades, Meet participations and organized meetings, the export warning; nothing erased | FR-002, FR-003 |
| AC-002 | Erasure with the mark removes participant, memberships, submissions, Meet participations; clears organizer email and course owner; others unchanged; a failure erases nothing | FR-004 … FR-008 |
| AC-003 | Without the mark → validation message, nothing changes | FR-004, VR-002 |
| AC-004 | Sync does not recreate the person, their memberships, submissions or ownership; others synced | FR-011, FR-012 |
| AC-005 | Meet load (incl. 3-day re-read) stores none of their participations; their meeting without organizer email; others stored | FR-011, FR-012 |
| AC-006 | Exclusion holds Google user id, fingerprint and date only; same email in two installations → different fingerprints | FR-008, FR-010 |
| AC-007 | Lifting by email in any case removes the exclusion and the next sync loads the person as new; unknown email → message, nothing changes | FR-009, FR-010 |
| AC-008 | Dean → `403`, no menu link; read-only → both actions `409` from `Application`, nothing changes | FR-001, FR-014, FR-015 |
| AC-009 | One audit event each (erasure: internal id + counts; lifting: exclusion id) and one `Information` log line per erasure; no name, email or Google user id | FR-013 |

Derived from the Specification:

| Id | Criterion | Specified by |
|---|---|---|
| AC-010 | A sync that read the person before the erasure committed and writes after it stores nothing of theirs; likewise for a Meet page | FR-011 |
| AC-011 | A submitter not on the roster with an excluded Google user id is not created by sync | FR-012 |
| AC-012 | Meet participations whose stored email differs from the participant's email only in letter case are erased and later skipped | FR-006, FR-012, VR-004 |
| AC-013 | Search by part of a name and by part of an email, any letter case, finds the person; a participant off every roster is found; the term never appears in the address | FR-002, VR-001 |
| AC-014 | More than 50 matches → 50 shown and the "refine" message | FR-002 |
| AC-015 | Confirmation or erasure of an unknown id → `404`, nothing changes | FR-016 |
| AC-016 | Two exclusions with the same fingerprint → lifting removes both, one audit event each | FR-009, FR-013 |
| AC-017 | Read-only mode: page, search and confirmation open with the notice; each refused action writes one refused audit event | FR-013, FR-014 |
| AC-018 | The retention purge leaves `ErasedParticipant` rows untouched | FR-017 |
| AC-019 | A participant with no email: erasure succeeds, deletes no Meet row, exclusion has no fingerprint; sync skips them | OD-001, FR-008, FR-012 |
| AC-020 | Erasure, lifting and search without a valid antiforgery token are refused; nothing changes | VR-005 |
| AC-021 | Page, messages, warning and mark in Ukrainian and English | FR-018 |

## 6. Validation Rules

### VR-001 Search term

- Required; trimmed; after trimming 2 to 254 characters (I-4). Empty or one
  character → validation message, no search.
- Treated as literal text: no wildcard, pattern or escape character has a
  special meaning.
- Never written to a log (SC-10); after a search it is shown back only in the
  search field of that response.

### VR-002 The erasure mark

- A boolean form field; only an explicit "true" sets it. Absent, empty or any
  other value → unset → refused with "confirm that erasure is irreversible",
  `400`, the confirmation page re-rendered, nothing changes, no audit event
  (AC-003).

### VR-003 Email to lift an exclusion

- Required; trimmed; at most 254 characters; of the form `local@domain` with
  exactly one `@` and non-empty parts. Any other → validation message, `400`,
  nothing changes, no audit event.
- The domain is **not** required to be the installation's domain: a person may
  have been erased with an email of another domain (I-13).
- Never written to a log; not echoed after success.

### VR-004 Email normalization

The normalization of `WorkspaceConnection.NormalizeEmail` (trimmed,
lower-cased, domain normalized) is used for the fingerprint input and for
matching Meet rows, so letter case and surrounding spaces do not matter
(Story Notes, AC-007).

### VR-005 Requests

- Every `POST` requires a valid antiforgery token (API-7); missing or invalid →
  refused, nothing changes, no audit event.
- The participant id in a route is a positive integer; anything else → `404`.
- The actor and role come from the authenticated session, never from the
  request (SC-1).
- Return pages are fixed by the server; no open redirect.

### VR-006 Data read from Google

Google user ids and emails read by synchronization and the Meet load are
validated as they are today; this Story adds only the exclusion check (FR-012).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | New Admin-only policy for the §2 row on every endpoint of this Story; deny by default; the SC-4 anonymous list gains nothing. | §2, SC-4 |
| S-02 | Allowed-role tests (Admin) and forbidden-role tests (Dean, anonymous, a Dean on the forced password change) for the page, search, confirmation, erasure and lifting. | SC-1, TC-5 |
| S-03 | No Teacher or Student access; no roster-based scoping. | Hard Stop, §2 |
| S-04 | Read-only refusal in `Application` for erasure and lifting, all three BR-025 causes; proven in `Application` tests, not as UI state. | §2, SC-5, BR-026, AD-6, TC-5 |
| S-05 | The exclusion stores no name and no email; the salt is generated by the installation, never configured, shown, logged, exported or sent anywhere. | §3, BR-076, SC-10, SC-13 |
| S-06 | Audit events and the log line carry internal ids and counts only — no name, email, Google user id, fingerprint or search term. | SC-10, SC-11, §4 Epic 10 |
| S-07 | Audit rows are not erased by this Story and are never updated. | §4 Epic 10, PC rules, SC-11 |
| S-08 | Antiforgery on every `POST`. | API-7 |
| S-09 | No personal data in an address: the search term and the email to lift travel in `POST` bodies; pages are addressed by internal id; one-time messages carry no personal data. | US-039 INFO-1, SC-10 |
| S-10 | No write to Google; no new outbound destination. | Hard Stop, SC-8, SC-13 |
| S-11 | No entity in a view model; DTOs mapped in `Application`. | AD-8 |
| S-12 | No new NuGet package; HMAC-SHA-256 and the random generator from the .NET base library. | AGENTS.md, I-2 |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous request | Redirected to sign-in (SC-4). |
| Dean, or Dean on the forced password change | `403` / refused as on every other Admin page. |
| Search term invalid (VR-001) | Page re-rendered with the message, `400`. |
| Mark unset (VR-002) | Confirmation re-rendered with the message, `400`; nothing changes. |
| Email to lift invalid (VR-003) | Page re-rendered with the message, `400`; nothing changes. |
| No exclusion for that email | Page with "no exclusion for this address" (`200`, not an error); nothing changes. |
| Unknown participant id | `404`, generic not-found page (FR-016). |
| Read-only mode on erasure or lifting | `409` with the reason from the host's handler; refusal audit (FR-014). |
| Failure inside the erasure transaction | Rolled back, nothing erased; the generic error page with no detail (AD-9, API-10, SC-10). |
| Missing antiforgery token | Refused per API-7. |

## 9. Non-Functional Requirements

- **Atomicity** — the erasure is one database transaction (AD-7, §4 Epic 10).
- **Bounded queries** — the counts on the confirmation page and the deletions
  are set-based (a fixed number of round trips regardless of how many rows the
  person has); the exclusion check in sync and the Meet load is done per batch,
  not per row round trip (NFR-003). Indexes needed are DB_DESIGN's.
- **NFR-073** — bilingual; dates in the school's time zone.
- **NFR-070** — usable on a phone.
- **NFR-062** — .NET 10, C#, nullable enabled, warnings as errors.
- **Tests** — real PostgreSQL via Testcontainers; Google ports substituted with
  synthetic data (TC-2, TC-4).

## 10. Out of Scope

- Any write to Google.
- Erasing audit rows, backups or files already exported.
- Re-applying erasures after a restore (§9, BR-077) — organizational.
- The Owner's manual procedure in read-only mode (§5, DC-8).
- Erasing an `AppUser` (Admin or Dean) — a teacher who also has an account keeps
  the account (§5 account retention).
- A list of existing exclusions — nothing readable could be shown (I-14).
- Any change to journals, Meet reports or exports beyond no longer containing
  the person.
- Erasure through a JSON API.

## 11. Open Decisions

Full text in `docs/decisions/US-038-open-decisions.md`.

| Id | Status | Impact if not resolved |
|---|---|---|
| OD-001 A participant with no email | **RESOLVED** 2026-10-15 — option (a) | none — FR-003, FR-006, FR-008, FR-012, AC-019 |

**OD-001** — `ClassroomParticipant.Email` is optional (US-014 OD-006): Google
may omit it, and a submitter not on a roster is stored with no email and no
name. Such a person has no fingerprint, so their Meet rows cannot be found and
their exclusion cannot be lifted by entering an email. **Resolved by the Owner
on 2026-10-15: option (a).** The options were:

- **(a) Erase them; the exclusion has no fingerprint** (recommended) — the data
  that can be tied to the person goes, synchronization skips the Google user
  id, the confirmation page warns that the exclusion cannot be lifted by email
  and that Meet rows (if any) cannot be tied to them. Lifting such an exclusion
  needs a later Story or the Owner.
- **(b) Refuse erasure for a participant with no email** — the confirmation page
  says so; the request goes to the Owner's manual procedure.

No item of `trebovaniya.md` §7 (v93) blocks the Story: items 10, 14, 26, 27
and 28 are open but none concerns erasure or exclusions; item 27 (re-load of a
leaver after the purge) and item 14 (submissions of a student removed from a
course) touch synchronization without changing what this Story skips.

### Interpretations

Stated because neither the Story nor `trebovaniya.md` fixes them literally.
Accepted as written by the Owner on 2026-10-15.

- **I-1 Both pages on one address.** Search and lifting share one page; the
  confirmation is its own page per participant.
- **I-2 HMAC-SHA-256 with a 32-byte salt.** §3 says "хеш с солью, своей у каждой
  инсталляции, по которому адрес восстановить нельзя". A keyed hash from the
  base library meets it without a package. Anyone holding both the database and
  the salt can still test a guessed email — inherent to a fingerprint that must
  match a typed email, and the trade-off v93 accepted.
- **I-3 Search by `POST`.** The term is personal data; keeping it out of the
  address keeps it out of request-path logs, history and the language
  switcher's return path (US-039 INFO-1).
- **I-4 2 … 254 characters, 50 results.** Not fixed anywhere; chosen to keep the
  search useful and bounded.
- **I-5 A grade is an assigned or a draft grade.** §4 "число его сдач и оценок";
  grades live on `Submission` (`AssignedGrade`, `DraftGrade`); a submission with
  either counts as one grade.
- **I-6 Courses owned are shown and counted.** §4 lists four counts on the
  screen; the owner mention is cleared too (§4 Epic 10), so its count is shown
  and audited with the others. Organizer and owner clearings are counted as
  "records of a kind" in the audit.
- **I-7 Shared email.** With two participants sharing an email, the Meet rows of
  that email go with the first erased; Meet rows are tied to an email, not to a
  participant row.
- **I-8 Read-only: page works, actions refused.** §2 blocks the actions; viewing
  stays. The button is shown disabled with the notice; a forced request still
  gets `409` from `Application`.
- **I-9 A meeting with no organizer.** The organizer email becomes optional;
  every reader of it (reports, code linking, the purge) treats an empty one as
  "organizer unknown" and does not fail. IMPLEMENTATION lists the readers it
  checked.
- **I-10 Lifting removes all matching exclusions.** The same email can have two
  exclusions (two participants sharing an email, or erased, returned, erased
  again with a new Google user id); one email lifts all of them, one audit event
  each.
- **I-11 "The next synchronization".** An active course is read at the next run;
  an archived, unchanged course only at the weekly full run (US-018).
- **I-12 Refusals are audited.** Other write use cases audit their read-only
  refusals (US-012, US-019); this Story does the same. Validation refusals are
  not audited.
- **I-13 Lifting accepts any domain.** A student may have had an external email.
- **I-14 No list of exclusions.** It could show only dates and Google numbers,
  which tell the Admin nothing.

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-001 … FR-003 | VR-001 | S-09, S-11 |
| AC-002 | FR-004 … FR-008 | VR-002, VR-004 | S-07 |
| AC-003 | FR-004 | VR-002 | — |
| AC-004 | FR-011, FR-012 | VR-006 | — |
| AC-005 | FR-011, FR-012 | VR-004, VR-006 | — |
| AC-006 | FR-008, FR-010 | VR-004 | S-05 |
| AC-007 | FR-009, FR-010 | VR-003, VR-004 | S-09 |
| AC-008 | FR-001, FR-014, FR-015 | VR-005 | S-01, S-02, S-03, S-04 |
| AC-009 | FR-013 | — | S-06 |
| AC-010 (derived) | FR-011 | — | — |
| AC-011 (derived) | FR-012 | — | — |
| AC-012 (derived) | FR-006, FR-012 | VR-004 | — |
| AC-013 (derived) | FR-002 | VR-001 | S-09 |
| AC-014 (derived) | FR-002 | VR-001 | — |
| AC-015 (derived) | FR-016 | VR-005 | — |
| AC-016 (derived) | FR-009, FR-013 | VR-003 | S-06 |
| AC-017 (derived) | FR-013, FR-014 | — | S-04 |
| AC-018 (derived) | FR-017 | — | S-05 |
| AC-019 (derived) | FR-008, FR-012 (OD-001) | — | S-05 |
| AC-020 (derived) | FR-004, FR-009 | VR-005 | S-08 |
| AC-021 (derived) | FR-018 | — | — |
