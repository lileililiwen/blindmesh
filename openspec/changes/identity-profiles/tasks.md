## 1. BFS — Baseline and impact coverage

- [ ] 1.1 Read handoff, check change names and archived dependencies (runtime-foundation); map every IDENT requirement/scenario to actor, UI, modules, contracts and failure fixtures.
- [ ] 1.2 Confirm C#/.NET and Next.js ownership, shared-package provenance, permission matrix, migration design and implementation-handoff gate; record baseline commands before code.

## 2. DFS — Requirement-by-requirement implementation

- [ ] 2.1 Implement IDENT-1 Session through owning persistence/application/API and usable interface, including its success/rejection/boundary tests.
- [ ] 2.2 Implement IDENT-2 Profile through owning persistence/application/API and usable interface, including its success/rejection/boundary tests.
- [ ] 2.3 Implement IDENT-UX route states, responsive navigation, semantics, keyboard/focus and theme behavior with browser/axe checks.

## 3. BFS — Cross-surface regression and completeness

- [ ] 3.1 Exercise all listed journeys and dependent callers; check visibility, stale versions, duplicate/concurrent commands, private files and transactional invariants; remove current-change placeholders.
- [ ] 3.2 Verify both themes and 360/768/1440px layouts, keyboard/assistive behavior, input recovery, migration upgrade and prior package regression; reconcile traceability.md and docs.

## 4. Verification

- [ ] 4.1 Run verification.md commands against isolated fixtures; record exact output and unavailable checks without treating them as pass.
- [ ] 4.2 Run local project checks/Gate and strict OpenSpec validation before normal archive; commit implementation/tests/archive/specs, then HANDOFF.md only, and stop without pushing.
