---
artifact_type: open_decisions
story: US-001
version: 2
status: DRAFT
created_at: 2026-09-16T07:54:22Z
updated_at: 2026-09-16T07:58:02Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-001-owner-first-run-setup.md
    version: null
  - path: trebovaniya.md
    version: 67
supersedes: null
---

# US-001 Open Decisions

Story-level Open Decisions for US-001 (Owner first-run setup). Every item is
resolved only by a human, at `HUMAN_SPEC_APPROVAL`; the resolution is written
next to the item and nothing is deleted.

`trebovaniya.md` section 7 has no open item this Story depends on (items 10 and
14 concern Google access and synchronization).

Status summary:

| Id | Subject | Status | Affects |
|---|---|---|---|
| OD-001 | Owner password and lockout policy | RESOLVED (v62, v64) | AC-005, AC-006, AC-008 |
| — | Where the Owner switches UI language | RESOLVED (US-039) | AC-009 |
| OD-002 | Lifetime of the Owner session | RESOLVED (2026-09-16) | AC-005, AC-011 |
| OD-003 | What a refused setup shows when the Owner account already exists | RESOLVED (2026-09-16) | AC-003, AC-004 |
| OD-004 | Order of field validation and the setup-code check | RESOLVED (2026-09-16) | AC-006, AC-007, AC-012 |
| OD-005 | Form, strength and comparison of the one-time setup code | RESOLVED (2026-09-16) | AC-007 |

---

## Carried from the Story

### OD-001 — Password policy for the Owner account

**Status: RESOLVED.** Carried verbatim in substance from the Story.

`trebovaniya.md` states that the Owner authenticates with a login and password
via ASP.NET Core Identity (§9), but did not fix minimum length, complexity or
lockout behaviour.

*Resolution (`trebovaniya.md` v62, refined in v64 and v65;
`security-conventions.md` SC-2):* the password is 15 to 128 characters, counted
in characters, not bytes; spaces are allowed; there are no composition rules; it
may not equal or contain the login, compared case-insensitively. The Owner login
is 4 to 64 characters — Latin letters, digits, `.`, `-`, `_` — compared
case-insensitively. 5 consecutive failed attempts lock sign-in for 15 minutes; a
successful sign-in resets the counter; there is no permanent lockout. Every
refused sign-in shows the same message. The audit refusal category "locked out"
exists. No external breached-password check (SC-13).

### (no id) — Where the Owner switches UI language

**Status: RESOLVED.** In the separate cross-cutting Story US-039, which covers
the Owner, Admins and Deans. US-001 ships translations and a Ukrainian default
only (AC-009).

---

## Raised by the Specification

### OD-002 — Lifetime of the Owner session

**Status: RESOLVED.**

**Gap.** `trebovaniya.md` §8 and SC-2 fix the session cookie's attributes
(`httpOnly`, `Secure`, `SameSite=Strict`) but nowhere say how long a Control
Plane session lasts: whether it survives closing the browser, whether it expires
after inactivity, and whether it has an absolute limit. ASP.NET Core's default
(a 14-day sliding expiry) would be an unapproved decision for the one account
that governs every school.

**Impact.** FR-010 (session) cannot state the expiry; tests of AC-005 and AC-011
cannot assert it; `dotnet-implementor` would fall back to the framework default.
The same gap exists for Admin and Dean sessions in the installation, so the
resolution is likely project-wide and belongs in a new version of
`trebovaniya.md` §8 and SC-2.

**Options.**

1. *(Recommended)* The session cookie is not persistent — it ends when the
   browser is closed — and expires after **30 minutes without activity**
   (sliding), with an absolute limit of **8 hours** from sign-in. There is no
   "remember me". The Owner works with the Control Plane rarely and briefly;
   a short session costs one extra sign-in.
2. Non-persistent cookie with a sliding expiry only (e.g. 8 hours), no absolute
   limit.
3. ASP.NET Core defaults (14-day sliding, persistent) — not recommended.

**Resolution:** *Resolved 2026-09-16 by the human (the Owner): option 1.* The Owner session cookie is not persistent, expires after 30 minutes without activity (sliding) and after 8 hours from sign-in at the latest; there is no "remember me". Recorded for the Owner session in the Control Plane only; Admin and Dean sessions are not decided here.

### OD-003 — What a refused setup shows when the Owner account already exists

**Status: RESOLVED.**

**Gap.** AC-004 requires the losing concurrent setup submission to be "refused
with a conflict, not a server error", and AC-003 requires setup to be refused
once the account exists. The setup page is a Razor form, not a `/api/v1` call,
so it does not get the API-6 body. The host's error page (`trebovaniya.md` §8,
SC-4) has a closed set of texts — `400` antiforgery, `403`, `404`, `500` — and
none of them fits a conflict. What the Owner sees is therefore not defined.

**Impact.** FR-006 and the error-handling table cannot state the response to a
setup `POST` that arrives when an Owner account already exists (the concurrent
loser, or a stale form submitted later). AC-004 cannot be tested for its visible
result.

**Options.**

1. *(Recommended)* Status `409`; the setup page is shown again with a single
   translated form-level message — "the Owner account has already been created;
   sign in" — and a link to the sign-in page. No field is refilled, nothing
   about the existing account is shown. This needs no change to §8, because it
   is a form message, like a validation error, not the error page.
2. Add a `409` "setup already completed" text to the error page — requires a new
   version of `trebovaniya.md` §8 and SC-4.
3. Redirect to the sign-in page — contradicts AC-004's "refused with a
   conflict" and would need the Story changed.

**Resolution:** *Resolved 2026-09-16 by the human (the Owner): option 1.* Status `409`; the setup page is shown again with one translated form-level message "the Owner account has already been created; sign in" and a link to the sign-in page; no field is refilled and nothing about the existing account is shown.

### OD-004 — Order of field validation and the setup-code check

**Status: RESOLVED.**

**Gap.** `trebovaniya.md` §8 puts request validation (DataAnnotations) at model
binding, before the use case, while rules that need state are checked in the use
case. The setup code is held in the running process, so its check is a use-case
rule. `trebovaniya.md` §5 (v67) audits a setup refused "because of a missing or
wrong setup code". Nothing says what happens when a submission has **both** a
wrong or missing code **and** an invalid login or password: whether the field
errors are shown, and whether the refusal is audited.

**Impact.** FR-004 (processing order), VR-004, the error-handling table and the
audit tests of AC-012 depend on it.

**Options.**

1. *(Recommended)* **Fields first, as §8 prescribes.** Login and password are
   validated at binding; on failure the form is shown again with field errors
   (`400`) and nothing is audited — such a submission could not have created an
   account whatever its code. The setup code carries no binding-time `Required`
   rule: an empty code is treated in the use case exactly like a wrong one, so a
   *missing* code with valid fields is refused and audited as "wrong setup
   code". The field rules are public (SC-2), so showing them to someone without
   the code reveals nothing.
2. **Code first.** The use case checks the code before any field rule; a wrong
   or missing code gets only the code error and is always audited; field errors
   are shown only with a valid code. Every attempt without the code is audited,
   but it departs from the §8 binding-first rule and would need that exception
   recorded in `trebovaniya.md` §8.

**Resolution:** *Resolved 2026-09-16 by the human (the Owner): option 1.* Fields first: login and password are validated at binding (`400` with field errors, not audited); the setup code has no binding-time `Required` rule, and an empty code is treated in the use case like a wrong one — refused and audited as "wrong setup code".

### OD-005 — Form, strength and comparison of the one-time setup code

**Status: RESOLVED.**

**Gap.** `trebovaniya.md` §9 and SC-2 say only that the code is "random",
printed to the server console, never to the log file, void once the account
exists, and regenerated on a restart without an Owner. Its length, alphabet,
source of randomness and how it is compared are not defined — and a
security-reviewer needs a criterion to judge "random enough".

**Impact.** FR-002 and VR-004 cannot state the code's format; the tests of AC-007
cannot assert it; SECURITY_REVIEW has no measure.

**Options.**

1. *(Recommended)* At least **128 bits** from the operating system's
   cryptographic random number generator, shown as 26 upper-case characters of
   Crockford Base32 in groups of 4 or 5 separated by `-` for readability.
   On input, hyphens and spaces are ignored and letter case does not matter.
   Compared in constant time. Kept only in process memory — never in the
   database, a file or configuration. At this strength guessing is infeasible,
   so no separate attempt limit is needed beyond the audit of AC-012.
2. A shorter code (e.g. 8–10 characters) plus a limit on wrong attempts per
   process lifetime — easier to type, but adds a lockout rule that nothing in
   the requirements defines.

**Resolution:** *Resolved 2026-09-16 by the human (the Owner): option 1.* At least 128 bits from the OS cryptographic RNG, shown as 26 upper-case Crockford Base32 characters in groups of 4 or 5 separated by `-`; hyphens, spaces and letter case ignored on input; constant-time comparison; kept only in process memory; no separate attempt limit.
