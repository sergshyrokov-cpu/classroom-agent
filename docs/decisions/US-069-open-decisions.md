---
artifact_type: open_decisions
story: US-069
version: 1
status: DRAFT
created_at: 2026-11-02T11:06:50Z
updated_at: 2026-11-02T11:06:50Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-069-small-interface-texts-and-show-password.md
    version: null
supersedes: null
---

# US-069 Open Decisions

Carried from the Story. All were resolved by the Owner on 2026-11-02 («всё по умолчанию»); none is open.

## OD-001 Where the show-password rule is decided

`trebovaniya.md` said nothing about showing a typed password, and AGENTS.md forbids a security
behaviour no artifact defines. a) a new requirements version first; b) the Story alone.

**Resolved 2026-11-02 by the Owner: a)** — done as `trebovaniya.md` v106, with SC-2 and NFR-077.

## OD-002 The role line

a) add the word: «Роль: Декан», en "Role: Dean"; b) leave the line and drop Scope item 1.

**Resolved 2026-11-02 by the Owner: a).** Spec FR-001.

## OD-003 Language labels

a) leave `УКР` and `ENG`; b) change to `УКР` and `EN` in both hosts.

**Resolved 2026-11-02 by the Owner: a).** Spec Out of Scope.

## OD-004 What the control looks like

a) an eye icon inside the right edge of the field, a same-host SVG in two states with translated
accessible names and no visible new word; b) a small text button «Показати» / «Сховати» next to the field.

**Resolved 2026-11-02 by the Owner: a).** Spec FR-005, FR-008.

## OD-005 Which fields

a) all 9, including the temporary password an Admin sets for a Dean; b) all but the Admin's temporary
password fields.

**Resolved 2026-11-02 by the Owner: a).** Spec FR-005.
