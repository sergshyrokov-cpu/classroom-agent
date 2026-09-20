---
artifact_type: open_decisions
story: US-009
version: 1
status: DRAFT
created_at: 2026-09-20T13:09:21Z
updated_at: 2026-09-20T13:09:21Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-009-configure-workspace-connection.md
    version: null
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-009 Open Decisions

Story-level Open Decisions for US-009 (Configure `WorkspaceConnection`). Every
item is resolved only by a human; the resolution is written next to the item and
nothing is deleted.

| Id | Raised by | Status |
|---|---|---|
| OD-001 Whether the Admin types the domain at all | the Story | RESOLVED 2026-09-20 (option 1) |
| OD-002 A saved connection whose domain no longer matches the `Installation` | the Story | RESOLVED 2026-09-20 (option 1) |

`trebovaniya.md` section 7 has no open item this Story depends on. Item 10 (the
minimum Google Workspace roles sufficient for the technical account) is a
"Проверить при внедрении" item about domain-wide delegation: the roles belong in
the super-admin instructions of **US-010** and in the diagnostic of **US-011**,
and this Story neither names a role nor calls Google, so it is unaffected. Item
14 (submissions of a removed student) is a Classroom question; item 26 is
documentation.

The rules this Story implements are closed in the requirements, not open: §3
(`WorkspaceConnection` — the two values it holds, the key that is *not* in it,
and the invariant checked on save), §9 ("Технический аккаунт" and "Три точки
контроля Владельца"), §2 (the permission matrix row and the read-only rules),
Epic 6 (§4) and BR-015, BR-020, BR-021, BR-026.

No new Open Decision was raised by SPECIFICATION. Where the requirements leave a
detail to the implementer rather than to the Owner, it is written as an
**interpretation** in §11 of the Specification, so it can be corrected at
`HUMAN_SPEC_APPROVAL` instead of being discovered in code.

---

## OD-001 Whether the Admin types the domain at all

**Carried from the Story, keeping its id.**

`trebovaniya.md` §3 and Epic 6 both say the Admin configures "домен и
impersonation-пользователь", while BR-020 requires that the saved domain equal
the `Installation` domain that the Control Plane reports and that
`LegitimacyState` keeps (v54). Taken together, the only domain a save can ever
carry successfully is one the installation already knows. The requirements do
not say whether the field is therefore an input or a displayed value.

Options as the Story put them:

1. The domain is **shown, not typed**: the page displays the `Installation`
   domain from `LegitimacyState` and the Admin enters only the technical
   account. The BR-020 check stays in `Application` — it is the Owner's control,
   not a UI convenience — but no human can fail it by typing.
2. The domain is typed and checked. Literal to the wording; its cost is a
   refusal that exists only to catch a typo.
3. The domain is typed, pre-filled and editable — a middle ground.

**Resolution:** option 1, decided by the Owner on 2026-09-20.

Consequences the Specification is written against, and which must not be read as
a relaxation of BR-020:

- the **save request carries the impersonation user's email only** (spec FR-005,
  VR-003). The domain written into the record is read server-side from
  `LegitimacyState` at the moment of the save (FR-003);
- the **BR-020 check stays in `Application`** and is tested by constructing the
  request rather than by typing in the form (FR-007, S-02, AC-004). A save whose
  domain differs — a crafted request, a later API client, a `LegitimacyState`
  that changed between the page render and the post — is refused, never
  corrected;
- `WorkspaceConnection` **keeps its own domain column** (FR-001). The record
  states which domain it was saved for, which is what makes the OD-002 mismatch
  detectable at all;
- **AC-005 is unaffected**: the impersonation user's email domain is typed by a
  person and is the field that can genuinely be wrong, so its refusal is the one
  Admins will actually see;
- the page still **shows** the domain: an Admin must be able to see which school
  the installation is bound to before entering an account in it.

This resolution needed no new version of `trebovaniya.md`: §3 fixes what the
record holds and what is checked on save, and both hold in full. What it settles
is where a value comes from inside the program, which §3 leaves open.

---

## OD-002 What happens to a saved connection if the `Installation` domain ever changes

**Carried from the Story, keeping its id.**

BR-021 says an `Installation` domain never changes — a school moving domains
gets a **new** `Installation` with a new database — so a legitimacy check
returning a domain that differs from the saved connection should be impossible.
The requirements do not say what the installation does if it happens anyway: a
Control Plane defect, a restored backup of the wrong database, or an installation
pointed at the wrong `Installation` record.

Options as the Story put them:

1. **Treat the connection as invalid and say so.** It is not used; the settings
   page states that the saved domain no longer matches the one the Owner records
   and that the connection must be saved again. Nothing is deleted
   automatically.
2. Ignore the difference; only the next save is checked. Simplest — and the only
   path by which the program reads a domain the Owner did not approve.
3. Clear the connection automatically. Rejected on sight: a deleted connection
   is indistinguishable from one never configured, and the evidence is gone.

**Resolution:** option 1, decided by the Owner on 2026-09-20.

Consequences the Specification is written against:

- the mismatch is a **state the program reports**, never a silent repair: an
  automatic rewrite would hide exactly the defect this exists to expose
  (FR-002 `DomainMismatch`, FR-004);
- **"not used" is defined here and consumed later**: the connection state is a
  value produced in `Application` (FR-002), so synchronization (EPIC-1) and
  "check access" (US-011) read an unusable connection as absent rather than
  each inventing their own check. This Story defines and tests the state; it has
  no Google caller of its own;
- **a save clears the mismatch**: saving follows the ordinary rules, and the
  domain written is the one `LegitimacyState` holds now (OD-001);
- the comparison uses the **same domain rule** as the two BR-020 checks (FR-007,
  VR-004), so case or a trailing dot can never manufacture a mismatch;
- nothing is deleted, and no audit row is written merely by *observing* the
  mismatch — audit records actions, not observations (spec I-5).

This resolution needed no new version of `trebovaniya.md` either: BR-021 keeps
saying the situation cannot legitimately arise, and the resolution only fixes
what the program does when the impossible is nevertheless in the database.
