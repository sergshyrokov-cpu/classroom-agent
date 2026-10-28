---
id: US-065
epic: EPIC-10
title: An Admin enters an erasure exclusion by email before the person is loaded
slug: erasure-exclusion-by-email-in-advance
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-10-28 from trebovaniya.md v104 (`578281e`,
# §3 ErasedParticipant, §4 Epic 10 «Исключение по email заранее», §2 matrix, §5
# audit, §9 «Перевод школы на другой домен» step 4; derived BR-076, BR-021, SC-11,
# DC-8 in `154a031`). Item 2 of the v104 agenda. Needed before any school moves
# to another domain.
---

# User Story

As an **Admin**

I want to enter, by email, an erasure exclusion for a person the program has not
loaded yet

So that, when the school moves to another Google Workspace domain and the new
installation starts with an empty database, its first synchronization does not
load again the people whose data the school had erased on written request.

---

# Business Value

An erasure on request (US-038, BR-076) is permanent only while the installation's
database keeps its `ErasedParticipant` exclusions. A domain move creates a new
installation with a new database (BR-021, DC-8): without this Story its first
synchronization would import every erased person who is still in Google — the
school's written request would be silently undone. The same entry also covers a
person whose request arrives before the program has ever loaded them.

---

# Scope

**In scope:**

- on the existing erasure page (Admin only, US-038), a block "exclude by email":
  the Admin enters one email and confirms; the program stores an exclusion with
  the salted fingerprint of that email and **no Google user id** (§3 v104);
- the page works before the Google connection is configured (§4 Epic 10 v104,
  DC-8 step 4), like the rest of the erasure page;
- when a loaded participant already has that email, no exclusion is entered: the
  page says so and offers the ordinary erasure of that participant (US-038 flow);
- when an exclusion with that fingerprint already exists, nothing new is stored and
  the page says the email is already excluded;
- Classroom synchronization additionally matches by fingerprint: a roster person
  whose email in Google's answer has the fingerprint of an exclusion without a
  Google user id is skipped exactly like an excluded id (US-038 FR-012 — not
  stored, no membership, no submissions, not recorded as course owner), and the
  exclusion gets that person's Google user id, from then on working by id even if
  the address changes (§3 v104);
- the Meet load needs no change — it already skips by fingerprint (US-038); a test
  shows that an exclusion entered in advance also hides that person's Meet
  connections and organizer email;
- lifting an exclusion entered in advance works as any lifting — by the same email
  (US-038);
- audit: entering an exclusion by email — actor the Admin, object the exclusion's
  id; no email, no fingerprint, no Google id (§5 v104, SC-11). Filling the Google
  user id by synchronization is not a separate audit event (it is a sync write);
- read-only mode: entering is refused in `Application`, like erasure and lifting
  (§2 v104, BR-026, AD-6), with the refusal audited as US-038 does for lifting;
- texts in Ukrainian and English (NFR-073);
- the entity, constraint and migration changes this needs (PC-2): the Google user
  id becomes optional, and the immutability rule allows the one fill by
  synchronization.

**Out of scope:**

- a person whose email Google did not return (a submitter found only by
  submissions, an account without a visible address) — known limitation in v104:
  the Admin erases them the ordinary way after synchronization;
- carrying exclusions over from the previous installation's database — not chosen
  (v104 agenda item 2: entered again from the school's written requests);
- a list of the exclusions on the page — only fingerprints are stored, there is
  nothing readable to list;
- the rest of the domain-move procedure (new `Installation`, suspension of the old
  one) — Owner operations in DC-8, no code.

---

# Acceptance Criteria

## AC-001 An exclusion entered in advance is stored without personal data

**Given** an Admin on the erasure page, no participant with the email
`ivan.petrenko@school.example` loaded

**When** the Admin enters that email and confirms

**Then** one exclusion is stored with the salted fingerprint of the email and no
Google user id, the page confirms it, and one audit event "exclusion entered by
email" is written with the exclusion's id and no email.

## AC-002 Works before the Google connection is configured

**Given** a new installation with no `WorkspaceConnection` saved yet

**When** the Admin enters an exclusion by email

**Then** it is stored as in AC-001.

## AC-003 The first synchronization does not load the excluded person

**Given** an exclusion entered in advance for an email, and a course in Google
whose roster has a student with that email, submissions and grades

**When** synchronization runs

**Then** that student is not stored — no participant, no membership, no submission
— the rest of the course is stored as usual, and the exclusion now holds that
student's Google user id.

## AC-004 After the match, the exclusion works by Google id

**Given** the situation after AC-003

**When** the student's email is renamed in Google and synchronization runs again

**Then** the student is still not stored.

## AC-005 Meet connections are hidden too

**Given** an exclusion entered in advance for an email

**When** the Meet load reads a meeting the person joined and one they organized

**Then** the person's connections are not stored and the organized meeting is
stored without its organizer email (as for an exclusion created by erasure).

## AC-006 Already loaded person — offered the ordinary erasure

**Given** a loaded participant with that email

**When** the Admin enters the email in "exclude by email"

**Then** no exclusion is stored, no audit event is written, and the page says the
person is already loaded and leads to the ordinary erasure of that participant.

## AC-007 Already excluded — nothing new stored

**Given** an exclusion already exists for that email (entered in advance or left
by an erasure)

**When** the Admin enters the same email again

**Then** no second exclusion is stored and the page says the email is already
excluded.

## AC-008 Lifting an exclusion entered in advance

**Given** an exclusion entered in advance, before or after its match by
synchronization

**When** the Admin lifts it by the same email

**Then** it is removed, the lifting is audited as today, and the next
synchronization loads the person as new.

## AC-009 Refused in read-only mode

**Given** the installation in read-only mode

**When** the Admin enters an exclusion by email

**Then** the request is refused in the Application layer, nothing is stored, and
the refusal is audited as for lifting.

## AC-010 Only the Admin

**Given** a Dean signed in

**When** the Dean posts to the "exclude by email" endpoint

**Then** the request is forbidden (TC-5), and an anonymous request is sent to the
sign-in page.

## AC-011 An invalid email is refused

**Given** an Admin on the erasure page

**When** the field is empty, too long or not an email

**Then** nothing is stored, the field shows the validation error, and the rejected
value is not written to the log (SC-10).

---

# Open Decisions

Resolved by the Owner on 2026-10-28 before activation.

## OD-001 Which addresses the field accepts

v104 does not say whether the email must be in the school's domain.

- **a) (recommended)** — any domain, like lifting an exclusion today (US-038 I-13):
  the two fields behave the same, and a Classroom course may hold a participant
  from another domain.
- b) — only the school's domain: catches a mistyped personal address, but an
  external participant could not be excluded in advance.

**Resolved 2026-10-28 by the Owner: a)** («a, a»).

## OD-002 One email or a list at a time

For a domain move the Admin may need to enter several addresses.

- **a) (recommended)** — one email per submission: erasures on request are rare,
  validation and messages stay per address, and each entry is one audit event.
- b) — a list (one address per line): faster for a move, but needs per-line
  results and partial-failure messages.

**Resolved 2026-10-28 by the Owner: a)** («a, a»).

---

# Notes

- **Code today:** `ErasedParticipant` (`src/ClassroomAgent.Domain/Entities/ErasedParticipant.cs`)
  requires a Google user id and is immutable (`ck_erased_participant_immutable`,
  `ErasedParticipantConfiguration.cs:23`); both change here — DB_DESIGN decides
  how the one fill is allowed. Classroom sync skips by id only
  (`RunSynchronizationUseCase.cs` ~757, `GetExcludedGoogleUserIdsAsync`); the Meet
  load already skips by fingerprint (~252, `GetExcludedFingerprintsAsync`).
  Lifting by email: `LiftErasureExclusionUseCase.cs` (salt may not exist yet —
  entering must create it, as `EraseParticipantUseCase` does with
  `GetOrCreateAsync`). Page and policy: `ErasureController.cs`
  (`InstallationPolicies.EraseParticipantData`).
- **Concurrency:** an entry made while a synchronization is running could race
  with that run loading the person. The specification should say whether entry
  takes the same fence as erasure (US-038 db-design §7) or the next run is enough.
- **Two exclusions, one fingerprint** are already possible (US-038 I-10); a match
  fills only exclusions that have no Google id yet.
- Expected stages: SPECIFICATION, API_DESIGN (one new POST on the erasure page),
  DB_DESIGN (nullable id, constraint, migration), TEST_WRITING, IMPLEMENTATION,
  verification, security review.
- No new NuGet package, no new requirements version.
