# Completion gates

UI/UX is the first product-planning priority. Every change assesses it; APIs do not replace interfaces.

## Gate 1 — entry

- Read handoff, list active proposals, select exactly one dependency-eligible change, and set current_spec.
- Map BFS impact, requirements/scenarios, UI flows/states, shared-owner decisions, contracts, and verification oracles.
- Confirm material architecture and failure policies are resolved; load relevant concern packs.

## Gate 2 — in-flight

- Implement only the selected change through all layers and its usable interface.
- Verify each scenario and check boxes only when there is evidence.
- A structural pass is SKELETON_READY, never DONE. Preserve unrelated files.

## Gate 3 — exit

- `python3 scripts/verify_bootstrap.py`, `node scripts/check-openspec-change-names.mjs` and strict OpenSpec validation pass; invalid change names block selection and archive.
- Required native tests/builds and local shared Gate run; record actual verdict and attributable failures.
- Human-facing outcomes have real browser/device flows, responsive checks, keyboard/focus and accessibility evidence.
- Ledger conservation, concurrent transitions, visibility, attachment access, and migrations are checked when affected.
- Project facts, README claims, traceability, and the complete impact map match the delivered tree.
- No secrets, invented metrics, silent errors, broad staging, or hand-edited lockfiles.
- Archive normally, without skipping canonical specs, only after evidence supports completion.
- Each completed implementation change requires exactly two commits: implementation/tests/archive/generated specs, then HANDOFF.md only. Do not ask for conversational authorization or confirmation before either commit. Stop after commit 2; do not push.

## Special checks

Security/privacy: threat boundary, unauthorized access, redaction, CSRF/session behavior, and sensitive evidence removal.
Persistence: fresh install and upgrade migration, rollback/forward recovery, concurrent writes, and audit preservation.
Public contracts: generated-client reproducibility, compatibility fixtures, pagination/errors/idempotency.
Dependencies: compatibility and vulnerability checks, provenance, license, lockfiles, and clean restore without sibling checkouts.
UI: all applicable states, both themes, 360/768/1440px layouts, contrast, axe, keyboard and focus restoration.

Planning-only checks do not waive any implementation requirement. Report BLOCKED with exact retry steps for missing tools, authority, environment, or product evidence.

Initialization also requires a preserved LICENSE, source-backed discoverability metadata, authenticated gh, authorized publication and verified remote metadata. A missing runtime capture is recorded as blocked in docs/assets/capture-plan.md; tokens and mockups are not product browser evidence.
