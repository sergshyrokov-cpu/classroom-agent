---
artifact_type: api_design
story: US-067
version: 1
status: DRAFT
created_at: 2026-10-29T08:18:31Z
updated_at: 2026-10-29T08:18:31Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-067-spec.md
    version: 1
  - path: docs/decisions/US-067-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 104
supersedes: null
---

# US-067 API Design — An Admin changes a Dean's login

**Verdict: PASS.** One server-rendered operation is added to the US-012 Dean accounts screen:
`POST /settings/deans/{deanId}/login`. Contract: `docs/designs/api/US-067-openapi.yaml`.

Delegation: none. The designer read the Specification, `US-012-openapi.yaml` (the `state` and
`password` operations and their schemas), `DeanAccountsController.cs`, `SignInRoutes.cs` and
`Views/DeanAccounts/Index.cshtml` directly.

## 1. Decisions (items the Specification left to this stage)

- **D-1 Path and method — `POST /settings/deans/{deanId}/login`** (FR-001). A sibling of `/state`
  and `/password`: a sub-resource of the account, form POST because the screen is Razor (§8) and
  the host's antiforgery filter covers it. Route constant `SignInRoutes.DeanLogin =
  "/settings/deans/{deanId:long}/login"`; a non-numeric id does not match the route (spec §6).
- **D-2 Form field `newEmail`** (FR-010). Not `email`: the creation form's `email` is what
  `DeanAccountsPageModel.emailInput` re-fills, and the change form must never re-fill (I-5). A
  distinct name keeps the two forms independent in tests and in the view.
- **D-3 "Login unchanged" answers `409`** (FR-006, I-3), re-rendering the screen — the same status
  and shape as `AlreadyInThatState` on `/state`. The controller's shared `AnswerAsync` gains one
  case; the new refusal value (`DeanAccountRefusal.LoginUnchanged`) maps there.
- **D-4 Validation refusals answer `400`** with the creation keys (FR-011): the shared
  `AnswerAsync` default branch already does this. `emailInput` passed as null.
- **D-5 New translation keys** (FR-011, NFR-073), uk and en:
  `DeanAccounts.ChangeLogin.Email.Label` (field label), `DeanAccounts.Action.ChangeLogin` (button),
  `DeanAccounts.Confirmation.LoginChanged` (success), `DeanAccounts.Refused.LoginUnchanged`. Exact
  wording is IMPLEMENTATION's; the keys are fixed here so tests can assert them.
- **D-6 The input is `type="email"` with `autocomplete="off"`**, like the creation form;
  server-side checks remain the only authority (browser validation is a convenience).

## 2. Operation notes

| Status | When | Body | Writes |
|---|---|---|---|
| `302` → `/settings/deans` | changed | — (TempData confirmation) | AppUser login + stamp, 1 audit row |
| `302` → `/sign-in` | anonymous | — | nothing |
| `400` | malformed / outside domain / taken (incl. race) / bad antiforgery | screen, page-level message | nothing |
| `403` | Dean, any id | error page | nothing |
| `404` | unknown id or not a Dean | error page | nothing |
| `409` | read-only | host error page | refusal audit row only |
| `409` | same address | screen, "login unchanged" | nothing |

Order of evaluation is spec FR-002; the first refusal answers.

## 3. Acceptance Criterion → operation

| AC | Operation / response |
|---|---|
| AC-001 | `changeDeanLogin` `302`; `GET /settings/deans` shows the new address; template list shows it as author |
| AC-002 | `changeDeanLogin` `302`, then `POST /sign-in` (US-012) with new / old address |
| AC-003 | `changeDeanLogin` `302`, then a replayed pre-change Dean cookie on any page → `/sign-in` |
| AC-004 | `changeDeanLogin` `400` with the creation keys |
| AC-005 | `changeDeanLogin` `400` `EmailAlreadyUsed` |
| AC-006 | `changeDeanLogin` `302` on a disabled, locked, temporary-password account |
| AC-007 | `changeDeanLogin` `302`, one `dean_login_changed` row |
| AC-008 | `changeDeanLogin` `409` read-only |
| AC-009 | `changeDeanLogin` `403` (Dean), `302` → `/sign-in` (anonymous) |
| AC-010 | `changeDeanLogin` `302`, then `POST /settings/deans` (US-012) with the old address → `302` |

## 4. Auth model

`ManageDeanAccounts` (Admin only, §2 matrix line 764), inherited from the controller attribute.
Antiforgery required (host-wide). No new anonymous path.

## 5. Error model

Server-rendered: refusals re-render `/settings/deans` with one `messageKey`; `403`, `404` and the
read-only `409` are the host's single translated error page. No JSON error body (no `/api/v1`
operation). The rejected value is never echoed, logged or audited (SC-10).

## 6. Compatibility

Additive. Existing operations, routes, schemas and keys are unchanged; the page gains a third form
per row. `ContractVersion.Current` stays 1.

## 7. Open questions

None.
