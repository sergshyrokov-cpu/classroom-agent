---
artifact_type: specification
story: US-072
version: 1
status: APPROVED
created_at: 2026-11-03T20:19:39Z
updated_at: 2026-11-03T20:30:26Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-072-control-plane-error-page-without-switcher.md
    version: null
  - path: trebovaniya.md
    version: 109
  - path: docs/specifications/US-039-spec.md
    version: 1
  - path: docs/specifications/US-071-spec.md
    version: 2
supersedes: null
---

# US-072 Specification: Control Plane error page without the language switcher

No lookups were delegated; the sources and the code were read in this stage.

## 1. Overview

The Control Plane's language switcher decides whether to render only from two facts: the user is signed in and the endpoint requires sign-in. `ErrorController.Page` can answer under a protected endpoint, so the error page sometimes renders the switcher, whose return-path fallback copies the raw path and query into a hidden field. This Story gives the Control Plane the installation's rule (US-071 FR-010): the error page never has a switcher, whatever endpoint it answers under.

## 2. Business Goal

A refused request's address is never copied into the page, and both hosts follow one rule. This is tidiness and consistency, not a fix of a serious exposure: the value is encoded, the page is the Owner's own, the Control Plane is on a private network and none of its query strings holds a search text or teaching data (US-071 SECURITY_REVIEW INFO-2).

It closes the Owner's decision on the deferred findings US-071/API_DESIGN/m-1 and US-071/SECURITY_REVIEW/INFO-2.

## 3. Business Flow

1. A signed-in Owner sends a request the Control Plane refuses, or asks for something that does not exist.
2. The host answers with its one error page, in the Owner's language.
3. The page holds its translated text, and its back link where it has one. It holds no switcher and nothing from the request's address.
4. An Owner who wants another language first leaves the error page, by the back link or by address, and uses the switcher on an ordinary page.

## 4. Functional Requirements

### FR-001 No switcher on the error page, under any endpoint

The Control Plane's error page has no language switcher on any path that reaches it:

| Path to the page | Endpoint it answers under | Today |
|---|---|---|
| Antiforgery refusal (`GlobalAntiforgeryFilter`) on a protected `POST` | the protected endpoint | switcher, raw query in the hidden field |
| Refused language code (`UiLanguageController`) | `POST /account/language`, protected | switcher, raw query in the hidden field |
| Re-execution of a status code (`/error/{0}`) | `ErrorController`, anonymous | no switcher |
| Exception handler (`/error/500`) | `ErrorController`, anonymous | no switcher |
| Access denied (`/error/403`) and a direct `GET /error/{code}` | `ErrorController`, anonymous | no switcher |

The first two rows are the defect. They are the only callers of `ErrorController.Page` outside `ErrorController`.

### FR-002 The page is recognised by what it is

The rule recognises the error page by its model (`ErrorPageModel`), which every path above uses through `ErrorController.Page`, as the installation's `PageHeaderRules.IsErrorPage` does. No caller sets a flag, so a later caller of `ErrorController.Page` cannot forget one.

The switcher renders only when all three hold: the user is signed in, the endpoint requires sign-in, and the page is not the error page.

The rule lives in the Control Plane's own code. The hosts share no library for it (section 10). The Control Plane has no header search field, so the rule covers the switcher alone.

### FR-003 The error page otherwise stays as it is

Its status codes, translated text, back link and language do not change. A signed-in Owner still sees it in the Owner's chosen language (`trebovaniya.md` section 8, v64).

### FR-004 Other pages stay as they are

Every ordinary Owner page keeps the switcher, and it works as before. Its return-path fallback (the raw path and query of the page) stays, as it does in the installation. Sign-in and first-run setup pages still have no switcher.

### FR-005 The switcher's comment states the rule as it is

The comment in the Control Plane's `_LanguageSwitcher.cshtml` says the error page never shows the switcher because of its anonymous endpoint, which is not so under a protected endpoint. It is reworded to state the FR-002 rule, citing this Story, as the installation's comment does.

## 5. Acceptance Criteria

| Id | Criterion | Story |
|---|---|---|
| AC-001 | A signed-in Owner sends a `POST` to a protected endpoint with a marker text in its query string, refused (a) for a bad antiforgery token and (b) for an unknown language code. The response is `400`; the marker is nowhere in the page; the page holds no switcher; it keeps its text and back link. The tests are shown red by a request before the fix, because the defect was established by reading the code. | AC-001 |
| AC-002 | A signed-in Owner sends a `GET` with a marker in its query string that is answered `404`. The error page holds neither the marker nor the switcher. This criterion guards existing behaviour (I-2). | AC-002 |
| AC-003 | An Owner who chose English gets the error page in English with no switcher, shown on a refused `POST` under a protected endpoint (I-3). | AC-003 |
| AC-004 | The Owner's ordinary pages have the switcher and it works as before: the existing `OwnerUiLanguageTests` stay green with no change to that file. | AC-004 |

## 6. Validation Rules

No input is added or changed. The refusals keep their existing validation: the antiforgery token (SC-4, API-7) and the language code and return path of US-039 VR-001 and VR-002.

## 7. Security Requirements

- **Requirement served:** the error page shows only translated text, no details (`trebovaniya.md` section 8, v64, v65). A hidden field copied from the request is such a detail.
- **Authorization:** no change. No endpoint, policy or anonymous entry is added or changed (SC-4).
- **Logging:** no change. The refused value and the marker are neither echoed nor logged (SC-10); the existing test on the refused code stays.
- **Removed from the page:** with the switcher, the error page no longer carries its language forms or their antiforgery tokens. Nothing on the error page posts.
- **Audit:** no action listed in SC-11 is added or changed.
- **Outbound data:** none (SC-13).

## 8. Error Handling

No new error. Refusals keep their status codes and texts (FR-003).

## 9. Non-Functional Requirements

- No new user-visible text and no translation entry (NFR-073).
- No inline style or new markup (SC-15, NFR-076). The header on the error page is empty, as it already is on the sign-in pages.
- The change is limited to Control Plane presentation code and tests. The installation does not change.

## 10. Out of Scope

- The switcher's return path on other pages: their fallback stays, as in the installation.
- Any new text.
- A shared library between the hosts.
- Any change to the installation.
- The back link's target (I-4).

## 11. Interpretations

- **I-1 Which rows of FR-001 change.** The Story names two refusals. Reading the code confirms they are the only `ErrorController.Page` calls under a protected endpoint; the other paths answer under the anonymous `ErrorController` and already have no switcher. The rule still covers them all, because it looks at the page and not at the path.
- **I-2 AC-002 passes before the fix.** A re-executed `404` answers under the anonymous `ErrorController` and its address is `/error/404`, so neither the switcher nor the marker appears today. The Story asks for this criterion, so it stays as a regression guard. Its test passing in the red phase is expected and is not evidence of the fix; AC-001 and AC-003 carry that.
- **I-3 AC-003 is shown on a refused `POST`.** The Story does not say which error page. On a re-executed page English and no switcher already hold (`AnonymousPages_HaveNoSwitcher_AndStayUkrainian`), so the test would prove nothing. Under a protected endpoint the page has the switcher today, so the test is red until the fix.
- **I-4 "Keeps its back link" means the link it has today.** On these two refusals that link leads to `/`. `trebovaniya.md` section 8 says the antiforgery refusal links "to the same page"; the Control Plane's filter names `/setup` and `/sign-in` and sends everything else to `/`. This Story does not change that: its scope is the switcher, and it adds no behaviour.
- **I-5 "Stay green unchanged" is checked against the current tests.** No test in `OwnerUiLanguageTests` asserts a switcher on an error page under a protected endpoint: `AGetOrAPostWithoutToken_ChangesNothing` and `AnInvalidCode_IsRefused_NotStored_NotLogged` assert status, text, stored value and logs. New tests go in a new class.

## 12. Open Decisions

None open. The Owner decided on 2026-11-03, at US-071's approval, on a small Story right after US-071, before the pilot, giving the Control Plane's error page the installation's rule. The Story has no `OD-` items. `trebovaniya.md` section 7 holds no item this Story depends on.

## 13. Traceability

| Acceptance Criterion | Requirements |
|---|---|
| AC-001 | FR-001, FR-002, FR-003, section 7 |
| AC-002 | FR-001, FR-002 |
| AC-003 | FR-001, FR-003 |
| AC-004 | FR-004 |

FR-005 changes a comment only and no test proves it; IMPLEMENTATION_VERIFICATION checks it by reading.

Design note: this Story does not change public API behaviour: no endpoint, method, status code or response contract is added or changed; only the markup of the error page loses the switcher. It does not change persistence behaviour: no entity, schema, migration or read changes. `API_DESIGN` and `DB_DESIGN` therefore return NOT_APPLICABLE with this reason.
