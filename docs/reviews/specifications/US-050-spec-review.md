---
artifact_type: specification_review
story: US-050
version: 1
status: APPROVED
created_at: 2026-10-16T07:53:05Z
updated_at: 2026-10-16T07:53:05Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-050-browser-security-headers.md
    version: null
  - path: trebovaniya.md
    version: 96
  - path: docs/specifications/US-050-spec.md
    version: 1
  - path: docs/specifications/US-050-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-050-open-decisions.md
    version: 1
supersedes: null
---

# US-050 Specification Review

## Summary

Verdict **PASS**. The Specification restates `trebovaniya.md` §8 "Защитные
заголовки браузера" (v95) and SC-15 faithfully, covers all six Acceptance
Criteria, invents no rule, and lists its four interpretations; the digest tells
the Owner all four plus the manual check in plain Russian. One Minor finding
(the inline-code scan does not look for `<style>` blocks). Nothing was
delegated: every source and code fact below was read directly.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | docs/stories/US-050-browser-security-headers.md | (unversioned) |
| Requirements | trebovaniya.md | 96 |
| Specification | docs/specifications/US-050-spec.md | 1 |
| Digest | docs/specifications/US-050-spec-digest.ru.md | 1 |
| Open Decisions | docs/decisions/US-050-open-decisions.md | 1 |

Inputs are current: the Specification names the Story and `trebovaniya.md` v96;
the digest names Specification v1.

## Fidelity

Checked against `trebovaniya.md` lines 1878–1897 (§8, v95) and
`security-conventions.md` SC-13, SC-14, SC-15.

- FR-002 (every response, every port, static files included) = §8 «на всех
  портах, включая статические файлы, страницу ошибки, выгружаемые файлы и ответы
  REST API». The deliberate departure from the SC-14 static-file exemption is
  correct (I-3).
- FR-003 values `DENY`, `nosniff`, `same-origin` = §8 / SC-15 verbatim.
- FR-004 CSP: `script-src`/`style-src`/`img-src`/`font-src 'self'`,
  `object-src 'none'`, `base-uri 'self'`, `form-action 'self'` (+ Google in the
  installation), `frame-ancestors 'none'` = SC-15 item by item. No
  `'unsafe-inline'`/`'unsafe-eval'`, matching SC-15's finding list.
  `default-src 'self'` is an addition (I-1), stricter only.
- FR-004 Google origin: verified in code. `InstallationSecurityServices.cs`
  (Google options, ~line 265) sets only `OnRedirectToAuthorizationEndpoint`,
  which redirects to `context.RedirectUri` plus an `hd` hint; no
  `AuthorizationEndpoint` override exists in `src/`, so the ASP.NET Core default
  on `accounts.google.com` applies. Google OAuth is an SC-13 destination; no
  report endpoint is added (FR-004, §7).
- FR-001 one host-wide rule = §8 «одно правило на хост». FR-002's placement
  "beside `NoStoreMiddleware`, before `PublicPortMiddleware`" matches both
  `Program.cs` pipelines (Web line 109–131, ControlPlane line 113–116), so the
  HTTP→HTTPS redirect, exception handler, status re-execution and static files
  all run after the rule.
- FR-005 one value per header: antiforgery's `SAMEORIGIN` would contradict
  §8 `DENY`; replacing it follows the requirement rather than adding to it.
- FR-006 = §8 «ни блоков `<script>` со скриптом внутри, ни атрибутов `onclick`
  и подобных, ни атрибутов `style`». Verified: a case-insensitive search of all
  `.cshtml` for `<style`, `style=`, `on…=`, `data:` and `http(s)://` finds
  nothing; `wwwroot` holds exactly the five scripts and two stylesheets FR-006
  lists; the only `fetch` calls go to `/api/v1/...` on the same host.
- §7 business, permission, audit, localization and read-only rules are not
  touched; the Specification says so (§1, §9) and that is accurate.

No invented rule; no contradiction.

## Story Coverage

AC-001 … AC-006 are all present with their Story ids and wording preserved;
AC-004 splits into an automated part (CSP on the redirect, `Location` origin =
`form-action` origin) and the manual browser check the Story's Notes require.
Out-of-scope items match the Story (SC-14, SC-2, page content) and add only
headers §8 does not name and CSP reporting — both correctly excluded. Nothing
beyond the Story.

## Consistency

- SC-14 / US-040: unchanged, including its static-file exemption (FR-008).
- SC-2: HSTS/redirection on the public port only, untouched; the Specification
  rightly omits `upgrade-insecure-requests` because the private port is plain
  HTTP.
- SC-4: no anonymous route added.
- US-010 spec I-5 and the comments in the export scripts already anticipated a
  CSP without `'unsafe-inline'`; FR-006/FR-007 agree with them.
- Permission matrix, BR-026, NFR-073, SC-11: not touched.

## Open Items

`trebovaniya.md` §7 (v96) has no item on headers, CSP, framing or inline code.
The Open Decisions artifact correctly records none.

## Interpretations

1. **I-1** `default-src 'self'` added for directives §8 does not name. Listed in
   Specification §11 and digest §2.
2. **I-2** Google listed as the origin `https://accounts.google.com`, not the
   full path; another blocked origin goes back to the Specification, not into
   code. Listed in both.
3. **I-3** Static files carry the headers (no SC-14 exemption). Listed in both.
4. **I-4** Antiforgery's `X-Frame-Options: SAMEORIGIN` replaced by `DENY`.
   Listed in both.
5. **AC-004 manual part** (real-browser sign-in recorded in the implementation
   report). Required by the Story's Notes; listed in digest §2.
6. Technical choices left open on purpose (FR-004 constant vs. derived origin
   with a test pinning it; FR-005 overwrite vs.
   `SuppressXFrameOptionsHeader`; VR-001 exact string incl. directive order) —
   implementation mechanics with a fixed observable result; not product
   interpretations, correctly not in the digest.

No unlisted interpretation found.

## Digest Fidelity

Every statement in digest §1 traces to FR-002…FR-007; §2 names I-1…I-4 and the
manual check without softening (I-2's "not added on the fly" is stated); §3 "no
open questions" and §4 match Specification §10–11. The language is plain
Russian. No finding.

## Findings

| # | Severity | Location | Checked against | Finding |
|---|---|---|---|---|
| m-1 | Minor | Spec FR-006, VR-003, AC-005 | `trebovaniya.md` §8 «скрипты и стили — только отдельными файлами»; SC-15 "no inline style" | The view scan looks for `<script>` bodies, `on…=` and `style=` but not for a `<style>…</style>` block. §8 requires styles only as files, and `style-src 'self'` would silently drop such a block in the browser. No view has one today, so the gap is in the guard test, not in behaviour. Suggest adding `<style` with content to VR-003 (TEST_WRITING may cover it as part of AC-005 without a new Specification version). |

## Verdict

**PASS** — no Critical or Major finding. m-1 is advisory and goes to the Owner
at `HUMAN_SPEC_APPROVAL`.
