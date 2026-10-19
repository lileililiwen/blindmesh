# Change workflow

## Breadth and scope

Work directly on main; create no branch/worktree unless requested. Read HANDOFF, `openspec list`, the package map, current canonical specs, and the selected change. Multiple proposals may be active, but exactly one change is selected for implementation. Honor dependencies; do not implement an unarchived prerequisite inside a consumer change.

Before selection or status/instructions/archive, run `node scripts/check-openspec-change-names.mjs`; invalid lowercase kebab-case names block the cycle. Run it again before strict validation. Do not use numeric/date-leading change names.

1. **BFS — Baseline and impact coverage:** map actors, goals, routes, UI states, contracts, data, integrations, callers, failures, security, tests, and shared owners. Confirm language/module ownership and the implementation-handoff gate.
2. **DFS — Requirement-by-requirement implementation:** implement each requirement through persistence, service, API, usable UI, and meaningful verification before moving on.
3. **BFS — Cross-surface regression and completeness:** revisit every actor, boundary, caller, state, invariant, responsive behavior, accessibility obligation, and dependent contract.
4. **Verification:** run local formatting/build/tests/browser/accessibility, strict OpenSpec, and shared Gate. Record evidence before changing task boxes or claiming completion.

UI/UX is the first product-planning priority. Every package includes an applicability assessment. APIs do not replace interfaces. Supporting internal changes must name the product consumer; a human-facing package includes its own coherent interface and browser oracle.

## Handoff and commit lifecycle

Set the sole first-line `current_spec: <active-name>` before implementation. After verifying and archiving the selected change, advance to the next dependency-eligible active change. Remove the pointer when no active change remains; never use none or TBD.

Each completed implementation change uses **exactly two commits**:
1. implementation, tests, archive, and generated canonical specs;
2. HANDOFF.md only, containing evidence, limitations, and next pointer.

Do not ask for conversational authorization or confirmation before either commit. Stop after commit 2; do not push. This lifecycle applies to completed implementation, not proposal authoring. No proposal is archived merely because it validates.

## Maintenance

Keep Project facts and affected docs aligned with actual files and commands in the same change.
Use shared owners through immutable artifacts or narrow adapters; never use sibling source paths.
Load concern packs before security/privacy, migrations, public API, dependencies, or UI changes.
When a real decision error occurs, add a decidable prohibition at the lowest governing layer and date the cause in HANDOFF.
Refresh real documentation captures after implemented milestones. Missing runtime/tooling is recorded as blocked; a mock screenshot is not product evidence.
