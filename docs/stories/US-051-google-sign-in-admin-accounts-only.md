---
id: US-051
epic: EPIC-6
title: The Google sign-in admits only Admin accounts
slug: google-sign-in-admin-accounts-only
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent from pre-pilot audit 2, finding F-2
# (docs/reviews/audits/2026-10-pre-pilot-cross-cutting-audit.md); to be reviewed
# and edited by the Owner before activation. Aligned with trebovaniya.md v96.
# Option (a) chosen by the Owner on 2026-10-15; the rule went into
# trebovaniya.md v96 (sections 2, 5, 9), SC-3, SC-11 and BR-011 first.
---

# User Story

As the **Owner** of the service

I want the Google sign-in to refuse an account that belongs to a Dean, even when
its email is in `AllowedAdmin`

So that a Dean can never sign in past their password, the forced change of a
temporary password and the lockout, and every account keeps exactly one role.

---

# Business Value

Pre-pilot audit 2 (finding F-2) found that `CompleteGoogleSignInUseCase` admits
any existing, enabled account whose email the Control Plane approves, without
checking its role. If the Owner approves as Admin an email the school already
uses for a Dean — or an Admin creates a Dean with an approved Admin's address
before that Admin's first sign-in — the person signs in through Google **as a
Dean**: no password, no forced temporary-password change, no lockout, and the
audit records an Admin sign-in. They gain no Admin rights, but the Dean
password rules (SC-2) stop protecting that account.

`trebovaniya.md` v96 closes the gap: one account, one role. The Google sign-in
admits only an Admin account; a Dean account is refused with its own message and
audit category, and is never converted.

---

# Scope

**In scope:**

- the Google sign-in refuses an existing account whose role is not Admin, after
  the `AllowedAdmin` check and before the disabled check (§2 v96);
- the refusal is written to the audit with the new refusal category "not an
  Admin account" and the account's id as actor (§5 v96, SC-11);
- the person sees a translated message (Ukrainian and English) on the sign-in
  page saying the address is a Dean account: sign in with login and password; a
  different address is needed for the Admin role;
- the database accepts the new refusal category (its migration, if the category
  is constrained in the schema).

**Out of scope:**

- converting a Dean into an Admin, or any change to the Dean account;
- any check in the Control Plane — it does not know the installation's accounts;
- the Dean password sign-in — unchanged;
- revoking a live session (audit 1 I-2) — unchanged.

---

# Acceptance Criteria

## AC-001 A Dean account is refused through Google

**Given** an enabled Dean account with email E, and the Control Plane answering
that E is in `AllowedAdmin`

**When** a person completes the Google sign-in as E

**Then** no session is created, the Dean account is unchanged (no sign-in time,
counter or stamp change), and the sign-in page shows the "this address is a
Dean account" message.

## AC-002 The refusal is audited

**Given** the refusal of AC-001

**When** the audit is read

**Then** exactly one row records a refused Admin sign-in with the Dean account's
id as actor and refusal category "not an Admin account", and no email.

## AC-003 The order of checks is kept

**Given** a Dean account with email E

**When** the Control Plane answers that E is not in `AllowedAdmin`, or does not
answer

**Then** the refusal and its category are "not in `AllowedAdmin`" or "Control
Plane unavailable", as before v96.

**And given** a **disabled** Dean account whose email is in `AllowedAdmin`

**Then** the refusal category is "not an Admin account", not "account disabled".

## AC-004 Admins are unaffected

**Given** an approved email with no account, and an approved email of an
existing Admin account

**When** each signs in through Google

**Then** the first gets a new Admin account and the second is admitted, as
before; a disabled Admin account is still refused as disabled.

## AC-005 Works in read-only mode as before

**Given** the installation in read-only mode

**When** a Dean account is refused through Google

**Then** the refusal and its audit row are written exactly as in AC-001 and
AC-002 (audit rows are on the BR-026 list).

## AC-006 The Dean still signs in with the password

**Given** the Dean account of AC-001

**When** the Dean signs in with login and password

**Then** the sign-in works as before, including the forced change of a
temporary password.

---

# Open Decisions

None. The rule, the order of checks and the audit category are fixed by
`trebovaniya.md` §2, §5 and §9 v96, SC-3 and SC-11.

---

# Notes

- Origin: pre-pilot audit 2 2026-10-15, finding F-2 (audit 1 I-3).
- Code entry point: `CompleteGoogleSignInUseCase.ExecuteAsync`
  (`src/ClassroomAgent.Application/UseCases/CompleteGoogleSignInUseCase.cs:45-71`)
  — the new branch sits between the Control Plane answer and the disabled check.
- New members: `AuditRefusalCategory` value and the matching sign-in refusal
  shown on the page; translation entries in both languages.
- Expected stages: API_DESIGN NOT_APPLICABLE (no new endpoint; the existing
  callback answers with the sign-in page); DB_DESIGN — only if the refusal
  category is constrained in the schema (a check constraint needs a migration,
  PC-2).
- No new NuGet package.
