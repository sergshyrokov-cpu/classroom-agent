---
id: US-072
epic: EPIC-6
title: Control Plane error page without the language switcher
slug: control-plane-error-page-without-switcher
priority: LOW
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-11-03 from the Owner's decision at US-071's approval.
# Closes the deferred findings US-071/API_DESIGN/m-1 and US-071/SECURITY_REVIEW/INFO-2.
# No requirements change: US-039 I-5 and trebovaniya.md section 8 (v64). Before the pilot.
---

# User Story

As the **Owner**

I want the Control Plane's error page to hold no language switcher, as the installation's does

So that a refused request's address is never copied into the page, and both hosts follow one rule.

---

# Business Value

Tidiness and one rule on both hosts, not a fix of a serious exposure. The risk is low (US-071
SECURITY_REVIEW INFO-2): the value is encoded, the page is the Owner's own, the Control Plane is on a
private network and no query string of its holds a search text or teaching data.

---

# Scope

**In scope:**

1. **No switcher on the error page, under any endpoint.** The Control Plane's rule looks only at whether
   the endpoint requires sign-in, so `ErrorController.Page` answered under a protected endpoint still
   renders the switcher, whose fallback copies the raw path and query into a hidden field. Two refusals
   do this, both on `POST`: the antiforgery refusal (`GlobalAntiforgeryFilter`) and the refused language
   code (`UiLanguageController`).
2. **The same rule as the installation** (US-071 FR-010): the page is recognised by what it is, so no
   caller has to remember a flag.
3. **The switcher's comment** states the rule as it now is; today it says the error page never shows the
   switcher, which is not so.

**Out of scope:** the switcher's return path on other pages (their fallback stays, as in the
installation), any new text, a shared library between the hosts, any change to the installation.

---

# Acceptance Criteria

## AC-001 No raw query on the error page

**Given** a signed-in Owner and a `POST` to a protected endpoint with a marker text in its query string,
refused (a) for a bad antiforgery token and (b) for an unknown language code

**When** the error page is shown

**Then** the marker is nowhere in the page, the page holds no switcher, and it keeps its text and back
link. The test first shows the defect by a request: it was established by reading the code.

## AC-002 Error page by re-execution

**Given** a signed-in Owner and a `GET` with a marker in its query string answered `404`

**When** the error page is shown

**Then** it holds neither the marker nor the switcher.

## AC-003 Language stays

**Given** an Owner who chose English

**When** the error page is shown

**Then** it is in English, with no switcher.

## AC-004 Other pages unchanged

**Given** the Owner's ordinary pages

**When** opened

**Then** the switcher is there and works as before: the existing `OwnerUiLanguageTests` stay green
unchanged.

---

# Open Decisions

None open. The Owner decided on 2026-11-03, at US-071's approval: a small Story right after US-071,
before the pilot, giving the Control Plane's error page the installation's rule.

---

# Notes

- **Reference:** `PageHeaderRules` and `ErrorPageHeaderTests` in the installation (US-071 FR-010, I-13,
  api-design D-3).
- **A signed-in Owner who wants another language first leaves the error page**, as in the installation.
- No new endpoint, schema or text is expected: API and database design should be empty deltas.
