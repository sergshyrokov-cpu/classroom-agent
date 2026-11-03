---
artifact_type: test_strategy
story: US-072
version: 1
status: DRAFT
created_at: 2026-11-03T20:40:15Z
updated_at: 2026-11-03T20:40:15Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-072-control-plane-error-page-without-switcher.md
    version: null
  - path: docs/specifications/US-072-spec.md
    version: 1
  - path: docs/designs/api/US-072-api-design.md
    version: 1
  - path: docs/designs/database/US-072-db-design.md
    version: 1
  - path: docs/decisions/US-072-open-decisions.md
    version: 1
supersedes: null
---

# US-072 Test Strategy

## Scope

The Control Plane's error page holds no language switcher on any path that reaches it, and ordinary pages keep theirs. All tests are in the new class `tests/ClassroomAgent.Tests/ControlPlane/Security/OwnerErrorPageHeaderTests.cs`; `OwnerUiLanguageTests` does not change (AC-004, spec I-5).

## Test levels

| Level | Use |
|---|---|
| Integration (HTTP) | Every scenario: a signed-in Owner against the real Control Plane host and PostgreSQL through `ControlPlaneTestHost` (TC-2). The behaviour is a rendered page, so a lower level would prove nothing |
| Unit, contract | None. The rule has no logic worth testing apart from the page, and there is no API change (API_DESIGN NOT_APPLICABLE) |

## Scenarios

**Positive (the other side of the rule).** An ordinary page keeps the switcher and the raw path and query fallback (FR-004). `EveryOwnerPage_RendersTheSwitcher` and the rest of `OwnerUiLanguageTests` stay green unchanged.

**Negative.**

- Antiforgery refusal with a marker in the query on two protected `POST`s, `/installations` and `/account/language` (AC-001 a).
- Refused language code with a marker in the query, the code being `de` or a marker of its own (AC-001 b).
- `404` with a marker, on an unmatched address and on a protected card that does not exist (AC-002). Regression guards, expected green before the fix (spec I-2).

**Boundary.** A form refused by validation also answers `400` under a protected endpoint, yet is an ordinary page and keeps its switcher. This rejects a rule built on the status code instead of the page (FR-002).

**Validation.** No input is added (spec section 6). The refused code is the only validation case, and it serves AC-001 b.

**Security.** On each refused or not-found page: the marker is nowhere in the body; there is no switcher form and no `aria-current`; there is no `<form` and no antiforgery field (spec section 7). The refused code is not echoed. The logging of the refused code is already proved by `AnInvalidCode_IsRefused_NotStored_NotLogged`, and no endpoint or policy changes, so no new role tests apply.

**Language.** An Owner who chose English gets the refusal in English with no switcher, both by antiforgery refusal and by refused code, under a protected endpoint (AC-003, spec I-3).

**Persistence.** None (DB_DESIGN NOT_APPLICABLE).

## Fixtures

`PostgreSqlFixture`, `ControlPlaneTestHost`, `FormClient`, `UiLanguageTestData`, `SearchTestData.Hrefs`, `Html`. No new helper, no Google port involved.

## Excluded, with reason

| Scenario | Reason |
|---|---|
| Exception handler (`/error/500`) and access denied (`/error/403`) with a signed-in Owner | FR-001 rows 4 and 5 answer under the anonymous `ErrorController`, as row 3 does; `ANotFoundAnswer_…` and `ErrorPageTests` cover that path. The rule looks at the page, not the path |
| A test that the rule uses `ErrorPageModel` and that no caller sets a flag (FR-002) | Internal structure. `AFormRefusedByValidation_KeepsItsSwitcher` rejects the wrong rule by behaviour; the mechanism is checked by reading at IMPLEMENTATION_VERIFICATION |
| The comment in `_LanguageSwitcher.cshtml` (FR-005) | A comment; spec section 13 assigns it to IMPLEMENTATION_VERIFICATION by reading |
| Translation keys | No new text (spec section 9) |
| Read-only mode | The Control Plane has none, and no write is added |

## Known limitations

In the red phase the six refusal cases stop at the marker line, the first assertion of `AssertBareErrorPage`. The later assertions on the same page (no switcher, no form, no token) first run after the fix. The same assertions are exercised now by the green `ANotFoundAnswer_…` cases, so they are known to be valid.

## Open Decisions affecting testing

None.
