---
artifact_type: ac_test_matrix
story: US-051
version: 1
status: DRAFT
created_at: 2026-10-15T21:00:00Z
updated_at: 2026-10-15T21:00:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-051-google-sign-in-admin-accounts-only.md
    version: null
  - path: docs/specifications/US-051-spec.md
    version: 1
  - path: docs/designs/database/US-051-db-design.md
    version: 1
supersedes: null
---

# US-051 Acceptance Criteria → Test Matrix

Classes: **R** = `Web.UseCases.DeanGoogleSignInRefusalTests`,
**G** = `Application.UseCases.GoogleFirstSignInRaceTests`,
**P** = `Infrastructure.Persistence.NotAnAdminAccountRefusalTests`,
**T** = `Web.Localization.NotAdminAccountTranslationTests`.
Status is the red-phase state before IMPLEMENTATION (see the test generation report).

| AC | Scenario | Level | Class | Method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Approved Dean refused, no session, "Dean account" text, no address on page | Integration (HTTP) | R | `AnApprovedDeanAccount_IsRefused_AndSeesTheDeanAccountMessage` | Redirect to /sign-in, no session cookie, uk text shown | Red |
| AC-001 | Dean account unchanged (all columns) | Integration (HTTP) | R | `TheRefusal_LeavesTheDeanAccountExactlyAsItWas` | Row JSON identical, still one Dean row | Red |
| AC-001 | Text is the specified one in uk/en | Localization | T | `TheKey_HasTheSpecifiedTextInEachLanguage` (uk, en) | FR-006 texts exactly | Red |
| AC-001 | Text differs from every other refusal | Localization | T | `TheText_DiffersFromEveryOtherSignInRefusal` (uk, en) | Different from 4 existing refusals | Red |
| AC-002 | One audit row: admin_sign_in, refused, Dean actor, new category, no email | Integration (HTTP) | R | `TheRefusal_IsAuditedAgainstTheDeanAccount_WithTheNewCategory` | Single row of that shape, no `@` in row JSON | Red |
| AC-003 | Dean + not approved → not_in_allowed_admin | Integration (HTTP) | R | `ADeanAccountNotInAllowedAdmin_KeepsTheNotApprovedRefusal` | Pre-v96 category and text | Green (regression) |
| AC-003 | Dean + unknown installation → unknown_installation | Integration (HTTP) | R | `ADeanAccount_WhenTheInstallationIsUnknown_KeepsTheUnknownInstallationRefusal` | Pre-v96 category and text | Green (regression) |
| AC-003 | Dean + Control Plane unavailable → control_plane_unavailable | Integration (HTTP) | R | `ADeanAccount_WhenTheControlPlaneDoesNotAnswer_KeepsTheUnavailableRefusal` | Pre-v96 category and text | Green (regression) |
| AC-003 | Disabled Dean approved → not_an_admin_account, not account_disabled | Integration (HTTP) | R | `ADisabledDeanAccount_IsRefusedAsNotAnAdminAccount_NotAsDisabled` | New category and text, account unchanged | Red |
| AC-004 | Approved address, no account → new Admin signed in | Integration (HTTP) | R | `AnApprovedAddressWithNoAccount_StillGetsAnAdminAccount` | Landing, session, Admin row, success row | Green (regression) |
| AC-004 | Existing Admin admitted | Integration (HTTP) | R | `AnExistingAdminAccount_IsStillAdmitted` | Landing, stamped, success row | Green (regression) |
| AC-004 | Disabled Admin refused as disabled | Integration (HTTP) | R | `ADisabledAdminAccount_IsStillRefusedAsDisabled` | account_disabled, disabled text | Green (regression) |
| AC-005 | Read-only (3 causes): refusal + audit row as in AC-001/002 | Integration (HTTP) | R | `InReadOnlyMode_TheDeanAccountIsRefusedAndAuditedTheSameWay` (NeverConfirmed, Suspended, GracePeriodExpired) | Same refusal, row, unchanged account | Red |
| AC-006 | After refusal Dean signs in by password through the forced change | Integration (HTTP) | R | `AfterTheRefusal_TheDeanStillSignsInWithThePassword_ThroughTheForcedChange` | Forced-change redirect, then landing | Green (regression) |
| AC-007 | Dean created between read and Admin insert → refused, unchanged, one committed row | Integration (interleaved) | G | `ADeanCreatedInTheMeantime_IsRefusedAsNotAnAdminAccount` | NotAnAdminAccount, no Admin row, one row | Red |
| AC-007 (FR-005) | Disabled Admin created meanwhile → account_disabled, not stamped | Integration (interleaved) | G | `ADisabledAdminCreatedInTheMeantime_IsRefusedAsDisabled` | AccountDisabled, row unchanged | Red |
| AC-007 (FR-005, FR-009) | Enabled Admin created meanwhile → still signed in | Integration (interleaved) | G | `AnAdminCreatedInTheMeantime_IsStillSignedIn` | Success, stamped, success row | Green (regression) |
| AC-008 | New category accepted by the CHECK constraint | Persistence | P | `TheNewCategory_IsAccepted` | Insert succeeds | Red |
| AC-008 | Unknown category still rejected | Persistence | P | `AnUnknownCategory_IsStillRejected` (not_admin_account, NotAnAdminAccount) | ck_audit_event_refusal_category_value violation | Green (closed list) |
| AC-008 | Enum ⇄ code round trip | Persistence | P | `TheCategory_RoundTripsThroughTheMapping` | Stored `not_an_admin_account`, read back as member | Red |
| AC-009 | Warning, own event, request id, no email | Integration (HTTP) | R | `TheRefusal_IsLoggedAsAWarningWithItsOwnEvent_AndWithoutTheEmail` | One AdminSignIn* event at Warning, new name | Red |

Every Acceptance Criterion AC-001…AC-009 has at least one mapped test.
