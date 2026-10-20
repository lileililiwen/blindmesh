# Delivery roadmap

The runtime foundation, the identity package, the project package, the capability package, the credit package, the task publication package, the participation execution package and the evidence package are implemented, verified and archived. The changes below that are not archived yet are proposed, unimplemented and unarchived. Version labels are targets, not releases. [Package map](openspec/package-map.md) is the dependency authority.

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
| 9 | v0.3 safety | moderation-arbitration | Reports and payment disputes reach auditable moderator decisions |
| 10 | v0.3 trust | reputation-history | Completed contributions produce non-purchasable, explainable reputation |
| 11 | v0.3 communication | notification-delivery | In-app and SMTP notifications survive retries without leaking private content |
| 12 | v0.4 pilot | pilot-operations | Operator rehearses deploy, rollback, backup/restore, and measured pilot release gates |

Foundation is deliberately before authentication; accounting is before publication. A task can never be published before its maximum budget is reserved. Baseline grant and expiry decisions are explicit in the credit change.

Public pilot requires every MVP package, especially arbitration, privacy, real browser evidence, and operational recovery. Intermediate changes are independently verifiable slices, not a launch claim.

## Deferred PRD scope

After pilot evidence: team organizations/invitations, scheduled real-time sessions, smart ranking and diversity quotas, regression campaigns, GitHub/Linear issue sync, data self-service/deletion automation, capability certification workflows, AI assistance, paid plans, and native clients. Scoped export/deletion requests are handled by the pilot operator runbook until automation exists. No vague deferred item is a hidden requirement of an active change.
