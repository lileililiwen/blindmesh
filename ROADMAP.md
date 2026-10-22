# Delivery roadmap

All thirteen packages are implemented, verified and archived. Version labels are targets, not releases. [Package map](openspec/package-map.md) is the dependency authority.

| Order | Target | Change | Independently accepted outcome |
|---|---|---|---|
| 1 | v0.1 foundation | runtime-foundation (archived) | Real web/API/database/worker bootstrap with accessible shell and health failure behavior |
| 2 | v0.1 identity | identity-profiles (archived) | GitHub sign-in, safe sessions, onboarding, and visible profiles |
| 3 | v0.1 projects | project-workspaces (archived) | Owners manage project/version/visibility lifecycles |
| 4 | v0.1 supply | capability-directory (archived) | Contributors declare and withdraw capabilities with truthful verification status |
| 5 | v0.1 accounting | credit-ledger (archived) | Users see audited balances; limited grants and escrow primitives conserve credits |
| 6 | v0.2 marketplace | task-publication (archived) | Owners publish budget-backed unified tasks; visitors discover eligible work |
| 7 | v0.2 execution | participation-execution (archived) | Qualified contributors apply, occupy independent slots, start, and withdraw safely |
| 8 | v0.2 feedback | evidence-findings (archived) | Contributors submit protected evidence; owners review and settle work |
| 9 | v0.3 safety | moderation-arbitration (archived) | Reports and payment disputes reach auditable moderator decisions |
| 10 | v0.3 trust | reputation-history (archived) | Completed contributions produce non-purchasable, explainable reputation |
| 11 | v0.3 communication | notification-delivery (archived) | In-app and SMTP notifications survive retries without leaking private content |
| 12 | v0.4 pilot | pilot-operations (archived) | Operator rehearses deploy, rollback, backup/restore, and measured pilot release gates |
| 13 | v0.4 discovery | scoped-matching (archived) | The three visibility levels work; the owner of a task reads its candidates; the order of the list is a fair exploration rule |

Foundation is deliberately before authentication; accounting is before publication. A task can never be published before its maximum budget is reserved. Baseline grant and expiry decisions are explicit in the credit change.

Public pilot requires every MVP package, especially arbitration, privacy, real browser evidence, and operational recovery. Intermediate changes are independently verifiable slices, not a launch claim.

The scoped matching package is the last of the delivery order. It closes the gap between the three visibility levels of the PRD and the product: a project or a task of the `matched` visibility is now readable by the accounts that fit its conditions and by nobody else, the owner of a task reads the list of the accounts that fit it, and the order of that list is a named exploration rule that puts the accounts without an accepted contribution first. The soft weights of the PRD stay unimplemented on purpose, because the PRD calls them hypotheses to calibrate and never a quality promise.

## Deferred PRD scope

After pilot evidence: team organizations/invitations, scheduled real-time sessions, smart ranking and diversity quotas, regression campaigns, GitHub/Linear issue sync, data self-service/deletion automation, capability certification workflows, AI assistance, paid plans, and native clients. Scoped export/deletion requests are handled by the pilot operator runbook until automation exists. No vague deferred item is a hidden requirement of an active change.
