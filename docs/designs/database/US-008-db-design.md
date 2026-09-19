---
artifact_type: database_design
story: US-008
version: 1
status: DRAFT
created_at: 2026-09-19T18:07:59Z
updated_at: 2026-09-19T18:07:59Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-008-spec.md
    version: 2
  - path: docs/designs/api/US-008-api-design.md
    version: 1
  - path: docs/designs/api/US-008-openapi.yaml
    version: 1
  - path: docs/decisions/US-008-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 78
supersedes: null
---

# US-008 Database Design — AppUser and AuditEvent

## 1. Scope

Two new tables in the **installation** database, created by one EF Core
migration:

- `app_user` — the account of an Admin or a Dean (`trebovaniya.md` §3; spec
  FR-003, FR-011);
- `audit_event` — the installation's own audit trail, opened by this Story with
  its first two rows: a successful and a refused Admin sign-in (§5; spec FR-012).

**The Control Plane database is not changed.** Its new endpoint
(`POST /service/v1/admin-login-checks`) reads `AllowedAdmin` and writes nothing —
no audit row, no `InstanceLicenseCheck`, no last-seen stamp (spec FR-009, S-17;
api-design §3). A migration in the Control Plane project would itself be a
defect of this Story.

Nothing else is touched: `legitimacy_state` (US-005) keeps its shape, and the
sign-in path reads it only through `GetLegitimacyModeQuery`.

## 2. Conventions applied

PC-2 (migrations only, no `EnsureCreated()`, applied by an explicit deployment
step), PC-3 (`bigint` identity surrogate key named `id`; a natural key such as
email gets a unique index, never the primary key), PC-4 (every column explicitly
mapped — length, nullability, uniqueness; no convention defaults), PC-5
(`snake_case` singular tables; `pk_` / `uq_` / `ix_` / `ck_` constraint names),
PC-6 (`created_at` / `updated_at` as UTC `timestamptz`, stamped by the existing
`TimestampInterceptor`), PC-7 (indexes justified per column), PC-8 (explicit
cardinality, `Restrict` by default), PC-9 (sensitive data rules).

Enums are stored as **explicit string codes**, never as integers or CLR member
names, with a `HasConversion` and a matching `CHECK` constraint — the pattern
`legitimacy_state` and the Control Plane's `audit_event` already use. An integer
enum would make a migration's meaning depend on member order.

## 3. Installation database — table `app_user`

The account of a person inside the installation (`trebovaniya.md` §3 "AppUser";
spec FR-003).

| Column | Type | Null | Default | Constraint / note |
|---|---|---|---|---|
| `id` | `bigint` | NOT NULL | identity | `pk_app_user` |
| `email` | `varchar(254)` | NOT NULL | — | as stored, already lower-cased (BR-079); the account identifier, never a login column of its own (`trebovaniya.md` §3 v55) |
| `normalized_email` | `varchar(254)` | NOT NULL | — | `uq_app_user_normalized_email` — the uniqueness *and* the lookup key |
| `role` | `varchar(16)` | NOT NULL | — | `ck_app_user_role`: `admin`, `dean` |
| `sign_in_method` | `varchar(16)` | NOT NULL | — | `ck_app_user_sign_in_method`: `google`, `password` |
| `password_hash` | `varchar(256)` | **NULL** | — | NULL for every Admin — not empty, not random (S-04, BR-010) |
| `security_stamp` | `varchar(64)` | NOT NULL | — | Identity-shaped, as `owner` (US-001) |
| `concurrency_stamp` | `varchar(64)` | NOT NULL | — | EF concurrency token, as `owner` |
| `access_failed_count` | `integer` | NOT NULL | `0` | `ck_app_user_access_failed_count`: `>= 0`; unused by the Admin path (spec I-3) |
| `lockout_end` | `timestamptz` | NULL | — | unused by the Admin path (spec I-3) |
| `ui_language` | `varchar(8)` | NOT NULL | — | `ck_app_user_ui_language`: `uk`, `en`; set to the school default at creation (FR-011); US-039 lets the user change it |
| `is_disabled` | `boolean` | NOT NULL | `false` | only US-012 sets it, and only for a Dean (spec I-9) |
| `last_successful_sign_in_at` | `timestamptz` | NULL | — | NULL until the first successful sign-in; PC-11 counts retention from it, or from `created_at` when never set |
| `created_at` | `timestamptz` | NOT NULL | — | PC-6 |
| `updated_at` | `timestamptz` | NOT NULL | — | PC-6 |

No column for a Google subject identifier, a Google token, a refresh token, a
display name or a picture: the sign-in stores nothing Google returned except the
email (spec FR-007, S-09). No column for the OAuth client id or secret reference
— those are configuration (FR-001, PC-9).

### 3.1 Constraints

| Name | Definition | Why |
|---|---|---|
| `pk_app_user` | `PRIMARY KEY (id)` | PC-3 |
| `uq_app_user_normalized_email` | `UNIQUE (normalized_email)` | one account per email, enforced by the database and not only by a check before insert (FR-011, AC-007) |
| `ck_app_user_role` | `role IN ('admin', 'dean')` | `AppRole` has exactly two members; Teacher and Student are Epic 7 |
| `ck_app_user_sign_in_method` | `sign_in_method IN ('google', 'password')` | FR-003 |
| `ck_app_user_role_sign_in_method` | `(role = 'admin') = (sign_in_method = 'google')` | in the first version Admin signs in through Google and Dean by password — `trebovaniya.md` §2, §3 |
| `ck_app_user_password_hash` | `(sign_in_method = 'password') = (password_hash IS NOT NULL)` | with the row above: **an Admin cannot have a password hash at all** (S-04, BR-010, PC-9) |
| `ck_app_user_ui_language` | `ui_language IN ('uk', 'en')` | NFR-073, as `owner` |
| `ck_app_user_access_failed_count` | `access_failed_count >= 0` | as `owner` |
| `ck_app_user_email_lowercase` | `email = lower(email) AND normalized_email = lower(normalized_email)` | BR-079: stored and compared in lower case; the last line of defence if a caller forgets to lower-case (VR-005) |

`ck_app_user_role_sign_in_method` and `ck_app_user_password_hash` together are
the database-level statement of S-04: no code path, present or future, can store
a password for an Admin. That is deliberate — S-04 is the requirement a reviewer
will check first, and an entity-level guarantee alone would be weaker than the
rule deserves.

**Consequence to accept knowingly:** both constraints encode a first-version
truth. If Epic 7 ever adds a role whose sign-in method differs, or US-012 ever
needs a Dean row without a hash, the constraint changes in that Story's
migration. That is the intended cost — a migration is cheap; an Admin with a
password is not.

### 3.2 Indexes

| Name | Columns | Why |
|---|---|---|
| `uq_app_user_normalized_email` | `normalized_email` | uniqueness and the only lookup this Story performs (PC-7: a unique index covers both) |

**Deliberately not created here:** an index on `last_successful_sign_in_at`. PC-7
asks for an index on a column a repository query uses as a lookup key, and no
such query exists in this Story — the retention purge that will scan it is
EPIC-10 (PC-11). The purge Story adds the index in its own migration, with its
query, rather than this Story guessing the shape of a query nobody has written.

### 3.3 Write — the sign-in path

In the sign-in use case (`Application`), through a repository in
`Infrastructure/Persistence/Repositories` that only stages changes; the use case
saves (package-map: repositories never call `SaveChangesAsync`).

| Situation | Change |
|---|---|
| approved email, no row | insert: `email`, `normalized_email`, `role = admin`, `sign_in_method = google`, `password_hash` NULL, `ui_language` = school default, `is_disabled` false, `last_successful_sign_in_at` = now |
| approved email, row exists | update `last_successful_sign_in_at` = now; nothing else |
| refused for any reason | **no write to `app_user`** (FR-011, AC-008) |

Concurrent first sign-in (FR-011, spec I-10):

1. look up by `normalized_email` (tracked);
2. none → add; exists → update the stamp;
3. save. On PostgreSQL `23505` with `ConstraintName = uq_app_user_normalized_email`
   — two sessions signing the same new Admin in at once — detach, reload by
   `normalized_email`, apply step 2 as an update, save once more;
4. any other failure surfaces as a failed sign-in, not as a duplicate row.

Both writes are on the BR-026 closed list as `SignInBookkeeping`, so they run in
read-only mode and no read-only guard applies to them (FR-013, AC-011).

### 3.4 Read

The sign-in path reads one row by `normalized_email`, tracked (it may be
updated). The landing page needs no read: the signed-in user's email and role
come from the session claims, not from a query (api-design §3, `LandingPageModel`).

## 4. Installation database — table `audit_event`

The installation's audit trail (`trebovaniya.md` §5; SC-11; spec FR-012). It is
**not** a copy of the Control Plane's table: the actor types, actions and refusal
categories differ, and the two databases never share a schema.

| Column | Type | Null | Default | Constraint / note |
|---|---|---|---|---|
| `id` | `bigint` | NOT NULL | identity | `pk_audit_event` |
| `occurred_at` | `timestamptz` | NOT NULL | — | UTC, from the injectable clock |
| `actor_type` | `varchar(16)` | NOT NULL | — | `ck_audit_event_actor_type`: `app_user`, `anonymous`, `system` |
| `actor_id` | `bigint` | NULL | — | the `AppUser` id; **no foreign key** (PC-9) |
| `actor_role` | `varchar(16)` | NULL | — | `ck_audit_event_actor_role`: `admin`, `dean`; the role at the time of the action |
| `action` | `varchar(64)` | NOT NULL | — | `ck_audit_event_action`: `admin_sign_in` — the only action this Story adds |
| `target_type` | `varchar(32)` | NULL | — | unused by this Story; present because the column set is fixed by SC-11 |
| `target_id` | `bigint` | NULL | — | unused by this Story |
| `outcome` | `varchar(16)` | NOT NULL | — | `ck_audit_event_outcome`: `succeeded`, `refused` |
| `refusal_category` | `varchar(32)` | NULL | — | `ck_audit_event_refusal_category_value`, five values (§4.1) |
| `request_id` | `varchar(128)` | NULL | — | ties the row to its log line (SC-11) |
| `created_at` | `timestamptz` | NOT NULL | — | PC-6 |
| `updated_at` | `timestamptz` | NOT NULL | — | PC-6; always equals `created_at` (§4.2) |

**No column may carry personal data.** No email, no name, no Google subject
identifier, no grade, no free-text field a caller could fill with any of them.
A migration adding one is a Critical finding (PC-9, SC-11). `actor_id` is an
internal identifier, and `request_id` is generated by the host.

**`actor_id` has no foreign key to `app_user`** — stated by PC-9 and load-bearing
here: audit rows outlive the accounts they name. PC-11 deletes an `AppUser` N
years after its last sign-in, while a refused-sign-in row naming it may be newer
and survives to its own expiry. A foreign key would either block that deletion or
cascade the evidence away.

### 4.1 Constraints

| Name | Definition | Why |
|---|---|---|
| `pk_audit_event` | `PRIMARY KEY (id)` | PC-3 |
| `ck_audit_event_actor_type` | `actor_type IN ('app_user', 'anonymous', 'system')` | FR-012 |
| `ck_audit_event_actor_id` | `(actor_type = 'app_user') = (actor_id IS NOT NULL)` | an account actor always carries its id; anonymous and system never do |
| `ck_audit_event_actor_role` | `(actor_type = 'app_user') = (actor_role IS NOT NULL)` | the role travels with the account actor |
| `ck_audit_event_actor_role_value` | `actor_role IS NULL OR actor_role IN ('admin', 'dean')` | matches `ck_app_user_role` |
| `ck_audit_event_action` | `action IN ('admin_sign_in')` | the only action this Story performs; a later Story extends the list in its own migration (spec I-11) |
| `ck_audit_event_target` | `(target_type IS NULL) = (target_id IS NULL)` | as the Control Plane's table |
| `ck_audit_event_outcome` | `outcome IN ('succeeded', 'refused')` | FR-012 |
| `ck_audit_event_refusal_category` | `(outcome = 'refused') = (refusal_category IS NOT NULL)` | a refusal always says why; a success never does |
| `ck_audit_event_refusal_category_value` | `refusal_category IS NULL OR refusal_category IN ('not_in_allowed_admin', 'control_plane_unavailable', 'unknown_installation', 'callback_failed', 'account_disabled')` | the five categories of FR-012 |
| `ck_audit_event_immutable` | `updated_at = created_at` | see §4.2 |

The `actor_type = 'system'` value is allowed although nothing in this Story
writes it: FR-012 names it as a member for later Stories (the retention purge
writes `system`, PC-11). A test asserts the factories of this Story produce only
`app_user` and `anonymous` rows.

### 4.2 Immutability

PC-9: an audit row is never updated, and is deleted only by the retention purge.
PC-6: because it is never updated, its `updated_at` always equals its
`created_at`.

The design enforces this on **two** levels:

1. **Entity level**, as the Control Plane's `AuditEvent` already does: private
   setters and factory methods that accept no free string which could carry an
   email or a token. No repository stages an audit row as `Modified`.
2. **Database level**, `ck_audit_event_immutable`: `updated_at = created_at`.
   The `TimestampInterceptor` stamps `updated_at` on any entity in `Modified`
   state, so any update that goes through EF Core moves `updated_at` past
   `created_at` and is rejected by the constraint.

Level 2 is derived, not invented: PC-6 already states the equality as a property
of the table, and turning a stated property into a `CHECK` costs nothing. Its
limit is stated plainly so nobody over-trusts it: it catches updates that pass
through EF Core, not a hand-written `UPDATE` that also rewrites `updated_at`.
Against a database superuser nothing in the schema is a defence; the Owner hosts
the server (BR-006).

A trigger rejecting `UPDATE` outright was considered and not adopted: the Control
Plane's audit table relies on the entity level, and introducing a second,
divergent mechanism in the installation without a requirement asking for it would
be this stage inventing a rule. If `SECURITY_REVIEW` wants the stronger
guarantee, it is a one-statement migration in a later Story.

### 4.3 Indexes

**None.** No repository query in this Story reads `audit_event` — the rows are
written and never read back until the audit viewer (EPIC-9) or the retention
purge (EPIC-10) exists. PC-7 asks for indexes on lookup keys, and there are none
yet. Both of those Stories will index `occurred_at` for their own query, with
their own migration.

This is a deliberate omission, not an oversight: an index on a write-only table
costs write amplification and buys nothing today.

### 4.4 Write

Exactly one row per sign-in attempt — success or refusal, never neither
(api-design §3). Written in the same transaction as the `app_user` change, so a
successful sign-in cannot leave an account without its audit row.

| Outcome | `actor_type` | `actor_id` / `actor_role` | `outcome` | `refusal_category` |
|---|---|---|---|---|
| approved | `app_user` | the row's id / `admin` | `succeeded` | NULL |
| not in `AllowedAdmin`, account exists | `app_user` | the existing row's id / its role | `refused` | `not_in_allowed_admin` |
| not in `AllowedAdmin`, no account | `anonymous` | NULL / NULL | `refused` | `not_in_allowed_admin` |
| Control Plane unavailable | `app_user` if a row exists, else `anonymous` | accordingly | `refused` | `control_plane_unavailable` |
| Control Plane does not know the installation | same rule | accordingly | `refused` | `unknown_installation` |
| callback failed (`state`, correlation, unverified email) | `anonymous` | NULL / NULL | `refused` | `callback_failed` |
| account disabled | `app_user` | the row's id / its role | `refused` | `account_disabled` |

The email entered is **never** written, in any outcome (`trebovaniya.md` §5 v45).
A callback failure is always `anonymous`: the callback never got as far as a
trustworthy identity, so attributing it to an account would be a guess.

Writing this table is `AuditEvent` on the BR-026 closed list, so it runs in
read-only mode (FR-013).

## 5. Relationships

None. `app_user` and `audit_event` have no foreign key — to each other or to
anything else — and neither has a navigation property.

`audit_event.actor_id` is intentionally a bare identifier (§4, PC-9).
`app_user` will gain relationships when Epic 7 links an account to a
`ClassroomParticipant`; the first version has none (`trebovaniya.md` §2).

`legitimacy_state` is unrelated to both and unchanged.

## 6. Sensitive data

| Data | Where | Rule |
|---|---|---|
| An Admin's or Dean's email | `app_user.email`, `app_user.normalized_email` | personal data; access limited to Admin and Dean (NFR-022); never in a log (SC-10); never in an audit row; purged with the account (PC-11) |
| Dean password hash | `app_user.password_hash` | hash only, never plaintext, never returned by any API or view model (SC-2, AD-8). NULL for an Admin, enforced by `ck_app_user_password_hash` |
| Google tokens, refresh tokens, subject id | **nowhere** | not stored at all (S-09). A column for any of them is a Critical finding |
| OAuth client id, client secret reference | **nowhere in any database** | configuration and the secret store only (FR-001, SC-7, PC-9) |
| Service-account key or its reference | **nowhere in any database** | PC-9, SC-7 — unchanged by this Story |
| Audit rows | `audit_event` | internal identifiers, categories and outcomes only; no personal data; never updated; deleted only by the purge (SC-11, PC-11) |

Retention (PC-11, not implemented here): an `app_user` row is deleted when
`last_successful_sign_in_at` — or `created_at` if it is NULL — is more than N
years old, disabled or not; an `audit_event` row when its own `occurred_at` is
more than N years old. The columns those rules need exist from this migration, so
EPIC-10 adds a query, not a schema change.

## 7. Migrations

### 7.1 Installation — `InitialAppUserAndAuditEvent`

One migration in `ClassroomAgent.Infrastructure`, created with the documented
command, applied by an explicit deployment step — never by
`Database.Migrate()` at start-up (PC-2, DC-4).

It creates: `app_user` with its primary key, its unique index and its eight check
constraints; `audit_event` with its primary key and its ten check constraints.
No index on `audit_event` (§4.3), no index on `app_user.last_successful_sign_in_at`
(§3.2), no foreign key anywhere (§5).

It seeds nothing. There is no first Admin to seed: the first `AppUser` appears
when a person the Owner approved signs in (BR-011). A seeded account would be an
account nobody approved.

The `ClassroomAgentDbContext` gains two `DbSet`s and the two configuration
classes; the existing `TimestampInterceptor` covers both new entities without
change (PC-6).

### 7.2 Control Plane — none

Deliberately empty. The Admin login check reads and writes nothing, so the
Control Plane's schema and its model snapshot must be identical before and after
this Story. A migration there is a defect (§1).

## 8. Test-relevant facts

Against real PostgreSQL via Testcontainers; the EF Core InMemory provider is
forbidden (TC-2).

- `uq_app_user_normalized_email` rejects a second row with the same normalized
  email, and the use case turns that `23505` into a re-read, not an error
  (§3.3, FR-011).
- `ck_app_user_password_hash` with `ck_app_user_role_sign_in_method` rejects an
  Admin row carrying any `password_hash`, including an empty string (S-04).
- `ck_app_user_email_lowercase` rejects a mixed-case email.
- `ck_audit_event_actor_id` rejects an `app_user` row without an `actor_id`, and
  an `anonymous` row with one.
- `ck_audit_event_refusal_category` rejects a `succeeded` row carrying a category
  and a `refused` row without one.
- `ck_audit_event_immutable` rejects an update that passes through EF Core, and
  the test states that it does not defend against raw SQL (§4.2).
- A successful sign-in writes exactly one `app_user` row and exactly one
  `audit_event` row, in one transaction; a refused one writes only the audit row.
- Both writes succeed with the installation in read-only mode, through the
  existing BR-026 members and without widening `PermittedServiceWrite` (FR-013).
- The Control Plane's model snapshot is unchanged by this Story (§7.2).
