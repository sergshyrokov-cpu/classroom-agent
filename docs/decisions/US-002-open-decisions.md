---
artifact_type: open_decisions
story: US-002
version: 2
status: APPROVED
created_at: 2026-09-16T13:29:12Z
updated_at: 2026-09-16T13:45:30Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-002-register-installation.md
    version: null
  - path: trebovaniya.md
    version: 69
supersedes: null
---

# US-002 Open Decisions

Story-level Open Decisions for US-002 (Register an Installation). Every item is
resolved only by a human, at `HUMAN_SPEC_APPROVAL`; the resolution is written
next to the item and nothing is deleted.

The Story's own "Open Decisions" section records none: its questions (the
installation identifier, status at creation, uniqueness, value formats, correcting
the name, deletion) were decided in `trebovaniya.md` v69. `trebovaniya.md`
section 7 has no open item this Story depends on (items 10 and 14 concern Google
access and synchronization).

Status summary:

| Id | Subject | Status | Affects |
|---|---|---|---|
| OD-001 | Domain label structure beyond the v69 character rule | RESOLVED (2026-09-16) | VR-002; AC-003 |
| OD-002 | Control characters in the school name | RESOLVED (2026-09-16) | VR-001; AC-003 |
| OD-003 | Time zone and precision of the creation date in the Control Plane | RESOLVED (2026-09-16) | FR-001, FR-005; AC-001, AC-005 |

---

## Carried from the Story

None.

---

## Raised by the Specification

### OD-001 — Domain label structure beyond the v69 character rule

**Status: RESOLVED.**

**Gap.** `trebovaniya.md` §3 (v69) fixes the domain as 3 to 253 characters of
Latin letters, digits, `-` and `.`, with at least one dot, no scheme, path or
`@`, lower case, no Cyrillic (IDN). It says nothing about how those characters
are arranged. As written, it accepts values no Google Workspace domain can have:
`..`, `.dac.ukr`, `dac.ukr.`, `-dac.ukr`, `dac-.ukr`, a 100-character label, and
`xn--` punycode labels (ASCII spelling of a Cyrillic domain, which v69 means to
exclude).

**Impact.** VR-002 cannot state what is rejected; tests of AC-003 cannot assert
it. A malformed domain registered by mistake cannot be corrected later — the
domain never changes (BR-021) and nothing is deleted — so the record would be
stuck holding that domain forever, blocking the correct one only if they happen
to be equal, and cluttering the list.

**Options.**

1. *(Recommended)* Standard host-name structure (RFC 1123) on top of v69: the
   domain is dot-separated labels; no empty label (so no leading, trailing or
   double dot); each label 1 to 63 characters; a label neither starts nor ends
   with `-`; a label starting with `xn--` is rejected (consistent with "no IDN").
   These are the rules every real Google Workspace domain already satisfies, so
   nothing valid is refused.
2. v69 character rule only — accept any arrangement.
3. Option 1 without the `xn--` rule.

Requires no new version of `trebovaniya.md` if resolved with option 1 or 3 as a
refinement of the v69 format; the Owner may still choose to record it there.

**Resolution:** *Resolved 2026-09-16 by the human (the Owner): option 1.* The domain is dot-separated labels with no empty label (no leading, trailing or double dot); each label is 1 to 63 characters and neither starts nor ends with `-`; a label starting with `xn--` (any case) is rejected. Applied as a refinement of the v69 format; no new version of `trebovaniya.md`.

### OD-002 — Control characters in the school name

**Status: RESOLVED.**

**Gap.** `trebovaniya.md` §3 (v69) fixes the name as 1 to 200 characters with no
whitespace at the start or end, and nothing else. A pasted name can carry a line
break, a tab or an invisible character (zero-width space, right-to-left mark)
inside it. Such a name renders confusingly in the list and is hard to tell apart
from a similar name.

**Impact.** VR-001 cannot state whether such names are rejected; tests of AC-003
cannot assert it.

**Options.**

1. *(Recommended)* Reject a name containing any Unicode control character
   (category `Cc` — line breaks, tabs, etc.) or format character (category `Cf` —
   zero-width and direction marks). Letters in any script, digits, punctuation,
   symbols such as `№` and ordinary inner spaces stay allowed.
2. Reject control characters (`Cc`) only; allow format characters.
3. No rule beyond v69.

**Resolution:** *Resolved 2026-09-16 by the human (the Owner): option 1.* A name containing any Unicode control character (category `Cc`) or format character (category `Cf`) is rejected; letters in any script, digits, punctuation, symbols and inner spaces stay allowed.

### OD-003 — Time zone and precision of the creation date in the Control Plane

**Status: RESOLVED.**

**Gap.** AC-001 and AC-005 show the installation's creation date. Times are
stored in UTC (PC-6). In an installation dates are shown in the school's time
zone, a required installation setting (DC-3, NFR-074). The Control Plane has no
time zone setting, and `trebovaniya.md` does not say in which zone, or with what
precision, the Control Plane shows a time. US-001 showed none.

**Impact.** FR-001 and FR-005 cannot state what is displayed; tests of AC-001 and
AC-005 cannot assert it. Later Control Plane pages (US-003 "added when",
US-005 last check time) will face the same question, so the answer sets a
pattern.

**Options.**

1. *(Recommended)* Date and time to the minute, in UTC, explicitly marked "UTC"
   (e.g. `16.09.2026 13:29 UTC` in the Ukrainian format). Needs no new
   configuration and cannot be misread; the Owner is the only reader.
2. Date and time in a fixed `Europe/Kyiv` zone. Natural for the Owner, but a
   hard-coded zone contradicts AD-10 unless it becomes a Control Plane setting —
   which needs a new version of `trebovaniya.md` (DC-3).
3. A required Control Plane configuration setting for its time zone, like the
   installation's — needs a new version of `trebovaniya.md`.
4. Date only, in UTC.

**Resolution:** *Resolved 2026-09-16 by the human (the Owner): option 1.* The Control Plane shows times as date and time to the minute in UTC, explicitly marked "UTC", in the Owner's UI-language format (e.g. `16.09.2026 13:29 UTC` in Ukrainian). No time zone setting is added.
