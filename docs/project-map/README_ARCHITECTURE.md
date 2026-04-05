# Architecture Entry Point

Status: active
Last updated: 2026-04-05

This is the single entry point for architecture and product-behavior documentation in `docs/project-map`.

## 1. Read in this order

1. `USER_APP_LOGIC.md`
- Product behavior from the user perspective.

2. `TARGET_ARCHITECTURE.md`
- Target architecture, boundaries, canonical flows, governance rules.

3. `APPLICATION_CONTRACTS.md`
- Application-layer contracts and guarantees.

4. `NFR_SLO.md`
- Non-functional expectations and reliability/responsiveness objectives.

5. `FAILURE_MATRIX.md`
- Expected behavior for known failure modes.

6. `ANTI_PATTERNS.md`
- Architectural guardrails (what must not be done).

7. `REGRESSION_CHECKLIST.md`
- Practical verification list after changes.

8. `OPEN_QUESTIONS.md`
- Ambiguities and pending clarifications. Do not implement speculative behavior.

9. `CURRENT_ARCHITECTURE_STATE.md`
- Snapshot of current implementation alignment.

10. `TECH_DEBT_BACKLOG.md`
- Deferred engineering cleanup and technical debt.

## 2. Decision records

- See `ADR/README.md` and accepted ADR files for fixed architectural decisions.

## 3. Reference docs

- `PROJECT_MAP.md` — codebase module map.
- `NUGET_PACKAGES_MAP.md` — dependency/reference map.
- `ONBOARDING.md` — quick onboarding checklist.

## 4. Archive policy

- Historical plans, closed transition reports, and old phase notes belong in `docs/project-map/archive/`.
- Files in `archive/` are not a source of truth for current implementation decisions.

## 5. Rule for ambiguity

If any requirement can be interpreted in more than one valid way:
1. record it in `OPEN_QUESTIONS.md`;
2. do not ship assumption-based behavior;
3. apply only approved behavior and update related docs in the same change set.
