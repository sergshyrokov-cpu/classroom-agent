---
artifact_type: specification_review
story: US-067
version: 1
status: APPROVED
created_at: 2026-10-29T08:14:07Z
updated_at: 2026-10-29T08:14:07Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-067-admin-changes-dean-login.md
    version: null
  - path: trebovaniya.md
    version: 104
  - path: docs/specifications/US-067-spec.md
    version: 1
  - path: docs/specifications/US-067-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-067-open-decisions.md
    version: 1
supersedes: null
---

# US-067 Specification Review

Nothing was delegated: the reviewer read the cited `trebovaniya.md` text, SC-2, SC-11, BR-014
and the code facts of spec §1.1 directly.

## 1. Summary

**PASS.** The Specification is faithful to `trebovaniya.md` v104 (§2 «Смена логина», §2 matrix
line 764, §5 audit line 1780) and to the derived BR-014, SC-2, SC-11. All ten Acceptance Criteria
are covered, both Story Open Decisions are applied as resolved, and every interpretation is named
in the digest. One Minor finding.

## 2. Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-067-admin-changes-dean-login.md` | — |
| Specification | `docs/specifications/US-067-spec.md` | 1 |
| Digest | `docs/specifications/US-067-spec-digest.ru.md` | 1 |
| Open Decisions | `docs/decisions/US-067-open-decisions.md` | 1 |

Inputs are current: the spec names Story and `trebovaniya.md` v104; the digest names spec v1.

## 3. Fidelity

- §1 quotes `trebovaniya.md` lines 713–720 verbatim; checked.
- FR-002 steps 3, 4, 6 ("same rules as at creation") match §2 «по тем же правилам, что при
  создании: только адрес в домене школы, не занятый другой учётной записью» and the creation code
  (`CreateDeanAccountUseCase.cs` 51–78): format, server-side domain from `LegitimacyState` with
  subdomains refused, uniqueness across roles. The password-policy step of creation is correctly
  left out (§2: «Пароль при этом не меняется»; FR-012).
- FR-003 / FR-005 (account, password, history, audit kept; old address refused; stamp rotated,
  session ends on next request) match §2 and SC-2 v104.
- FR-007 (audit without addresses) matches §5 line 1780 and SC-11 «the account's id only, neither
  address — v104».
- FR-009 (Admin only) matches the §2 matrix line 764; no new cell.
- §1.1 code facts checked: creation order and refusals, controller statuses (`400` refusals,
  `404` no such Dean, `409` `AlreadyInThatState`), per-row forms in `Index.cshtml`, read-only
  refusal audit carrying the account id in `SetDeanAccountStateUseCase`/`ResetDeanPasswordUseCase`.
- §10 "refused at Google sign-in (v96)" checked against §2 line 691.

No invented rule, no contradiction.

## 4. Story Coverage

AC-001…AC-010 are carried with stable ids and traced (§13). Out-of-scope list matches the Story
(Admin's own login, automatic detection, password change, mail) plus one addition (AllowedAdmin
cross-check), which narrows, not widens, scope.

## 5. Consistency

- Permission matrix: no new cell.
- Read-only (BR-026, AD-6): refused in `Application` before anything else, refusal audit row as
  for the other management operations (US-012 FR-017).
- Localization (NFR-073): new strings in uk/en; refusals reuse creation keys.
- SC-11: new closed-list action, CHECK constraint extended by migration (PC-2).
- US-064 (stamp check per request) reused for AC-003.

## 6. Open Items

`trebovaniya.md` §7 holds no item this Story depends on (checked items listed in the Open
Decisions file). OD-001 a) and OD-002 a) resolved by the Owner 2026-10-28, applied in FR-003 and
FR-006.

## 7. Interpretations

All six interpretations in spec §11 (I-1 order of checks, I-2 race answered as "taken", I-3
"unchanged" as a refusal-type re-render, I-4 read-only refusal audited, I-5 page-level message
without re-filling the row, I-6 author shown via existing join) are named in digest §2 or, for
I-6, stated in digest §1 as a plain fact (no data changes — not a choice the Owner must weigh).
The reviewer found no further unnamed interpretation. Choices deferred to API_DESIGN (path,
field name, status of "unchanged") are disclosed in FR-001, FR-006, FR-010.

## 8. Digest Fidelity

Every digest statement traces to an FR; no Open Decision is presented as open or hidden; the
password-rule consequence (FR-012) and the audit-without-addresses rule are stated plainly. The
language is plain Russian.

## 9. Findings

- **m-1 (Minor)** — spec FR-007 vs FR-008. FR-007 says only `AuditEvent.DeanAccountManaged`
  must accept the new action `DeanLoginChanged`; FR-008 additionally writes a read-only refusal
  row through `DeanAccountManagementRefused` with that action. The factory/validation behind the
  refusal row (and the CHECK constraint) must accept the new action too. Implied, not stated —
  DB_DESIGN / TEST_WRITING should cover the refused row's action explicitly.

## 10. Verdict

PASS — no Critical or Major findings; m-1 is advisory.
