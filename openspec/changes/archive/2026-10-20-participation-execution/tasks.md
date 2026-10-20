## 1. BFS — Baseline and impact coverage

- [x] 1.1 Read handoff, check change names and archived dependencies (task-publication); map every PART requirement/scenario to actor, UI, modules, contracts and failure fixtures.
- [x] 1.2 Confirm C#/.NET and Next.js ownership, shared-package provenance, permission matrix, migration design and implementation-handoff gate; record baseline commands before code.

## 2. DFS — Requirement-by-requirement implementation

- [x] 2.1 Implement PART-1 Seat through owning persistence/application/API and usable interface, including its success/rejection/boundary tests.
- [x] 2.2 Implement PART-2 Deadlines through owning persistence/application/API and usable interface, including its success/rejection/boundary tests.
- [x] 2.3 Implement PART-UX route states, responsive navigation, semantics, keyboard/focus and theme behavior with browser/axe checks.

## 3. BFS — Cross-surface regression and completeness

- [x] 3.1 Exercise all listed journeys and dependent callers; check visibility, stale versions, duplicate/concurrent commands, private files and transactional invariants; remove current-change placeholders.
- [x] 3.2 Verify both themes and 360/768/1440px layouts, keyboard/assistive behavior, input recovery, migration upgrade and prior package regression; reconcile traceability.md and docs.

## 4. Verification

- [x] 4.1 Run verification.md commands against isolated fixtures; record exact output and unavailable checks without treating them as pass.
- [x] 4.2 Run local project checks/Gate and strict OpenSpec validation before normal archive; commit implementation/tests/archive/specs, then HANDOFF.md only, and stop without pushing.
