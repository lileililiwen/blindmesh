# PRD coverage and scope decisions

Source: requirements.md v1.0, 2026-10-19. Preserved unchanged. All rows below describe proposed work, not implemented features. Stable requirement/scenario IDs and exact task/test mappings are in each change's traceability.md.

| PRD section / Epic | Owning change and requirement IDs | Boundary / disposition |
|---|---|---|
| §1 brand | Bootstrap docs/branding.md | Blindmesh selected; naming/domain/trademark not verified |
| §2 scenarios and four combined modes | task-publication TSK-001..004 | Unified model; Explore/Verify primary MVP; Access/Review configure same lifecycle |
| §3.1–3.2 / EP-01 | identity-profiles IDN-001..004 | GitHub, pseudonyms, profile/visibility/rules; Google/email auth and portfolios deferred |
| §3.3 projects / EP-02 | project-workspaces PRJ-001..004 | Ownership, members, versions and lifecycle; organizations/owner transfer deferred |
| §3.4 tasks / EP-02 | task-publication TSK-001..004 | Simple/advanced authoring, scope, maximum reserve, discovery/cancellation |
| §4.1 participation / EP-03 | participation-execution PAR-001..004 | Independent seats, eligibility, start/withdrawal/deadlines; later submission owner FDB |
| §4.2–4.3 feedback / EP-04 | evidence-findings FDB-001..004 | Scope reports, zero-bug validity, findings, version history, Markdown/JSON/CSV export |
| §4.4 resource collaboration | capability-directory CAP-001..004; TSK-001; PAR-002 | Owner-operated/official delegated scope; revocation acknowledgment; no credential custody or video service |
| §5 capability/matching / EP-06 | capability-directory CAP-001..004; PAR-001..002 | Typed declarations and hard eligibility; soft weights, AI, quotas and certification automation deferred |
| §6 credits / EP-05 | credit-ledger CRD-001..004; TSK-002; FDB-003; MOD-002 | Conservation, lots, grants, reservation, Pending, settlement/refund/appeal hold |
| §6.4 anti-abuse | MOD-001..004; OPS-003 | Self/member exclusion, rate limits, anomaly queue, reports; no automatic guilt from heuristics |
| §7 technology | runtime-foundation FOUND-1..3, FOUND-UX; identity-profiles IDENT-1..2, IDENT-UX; project-workspaces PROJ-1..2, PROJ-UX; capability-directory CAP-1..2, CAP-UX; credit-ledger CRED-1..2, CRED-UX; task-publication TASK-1..2, TASK-UX; participation-execution PART-1..2, PART-UX; evidence-findings EVID-1..2, EVID-UX; moderation-arbitration MOD-1..2, MOD-UX; reputation-history REP-1..2, REP-UX; notification-delivery NOTIF-1..2, NOTIF-UX (implemented) | C#/Next/Postgres modular monolith; no speculative microservices/AI process |
| §8 database constraints | D3 of every owning change; CRD-001..003 | One migration stream, uniqueness/locking/authorization; no full unused schema scaffold |
| §9 API | All D4 contracts; FOUND-2 | REST actions, OpenAPI, generated client, consistent errors/idempotency |
| §10 pages and dashboard | Every package's D5 and fourth requirement; the project pages `/projects`, `/projects/new`, `/projects/{id}`, `/projects/{id}/settings`, the capability pages `/capabilities`, `/settings/capabilities`, `/tasks/{id}/eligibility`, the task pages `/tasks`, `/tasks/new`, `/tasks/{id}`, `/projects/{id}/tasks`, the evidence pages `/work/{id}/submit`, `/projects/{id}/reviews`, `/findings/{id}` the moderation pages `/reports/new`, `/disputes/{id}`, `/admin/reports`, `/admin/disputes`, the reputation pages `/people/{username}/history`, `/settings/reputation` and the notification pages `/notifications`, `/settings/notifications` (implemented) | Coherent UI per capability; no fictional task/credit dashboard |
| §11.1 notifications / EP-08 | notification-delivery NOTIF-1..2, NOTIF-UX | In-app/SMTP, preferences/retry; no live push/calendar |
| §11.2 communication | FDB-003, MOD-001 | Structured correction and case evidence; conversational chat/session scheduling deferred |
| §11.3 admin | moderation-arbitration MOD-001..004; pilot-operations OPS-001..004 | Cases, restrictions, manual capability verification, ledger reconcile, risk/ops; no raw balance editing |
| §12 AI | Deferred follow-up after pilot | No AI may mint credits, judge final completion, or operate resource accounts |
| §13 privacy/security | Shared architecture; IDN/FDB/MOD/OPS | Scoped access, quarantine, retention/export/delete runbook; automation/legal-compliance certification excluded |
| §14 engineering gates | FOUND-1..3, FOUND-UX; IDENT-1..2, IDENT-UX; PROJ-1..2, PROJ-UX; CAP-1..2, CAP-UX; CRED-1..2, CRED-UX; TASK-1..2, TASK-UX; PART-1..2, PART-UX; EVID-1..2, EVID-UX; MOD-1..2, MOD-UX; REP-1..2, REP-UX; NOTIF-1..2, NOTIF-UX; OPS-001..004; all verification.md | Real DB concurrency, browser/axe, generated client, backups, measurable performance |
| §15 milestones/MVP | openspec/package-map.md, ROADMAP.md | Ledger moved before public publication to satisfy funded-task invariant |
| §16 commercial/cold start | CRD-002, OPS-003 | Bounded once-per-subject grants and pilot invitations; paid credits/subscriptions deferred |
| §17 product validation metrics | REP-001/004, OPS-003/004 | Observed sample sizes and rates; PRD targets remain hypotheses, not fabricated evidence |
| §18 epics/DoD | Twelve package traceability files; completion gates | Split independent domains and preserve usable interface per outcome |
| §19 core constraints | TSK-001, FDB-001, CAP-001..003, CRD-001..003 | Unknown blind spots, diverse lawful resources, auditable exchange |

## Explicit decisions beyond PRD examples

The PRD's sample reward/time formula and matching weights are illustrative. MVP uses owner-selected integer rewards and exact hard filtering. Grant60/expiry30 days, review72h, one correction48h, slot start24h, task execution7 days, grace24h and dispute appeal7 days are concrete conservative initial policy decisions in architecture, not measured optimal values.

Publication reserves maximum bonus per slot. Reserved grants retain provenance; refunds after grant expiry go to expiry sink. Pending is funded on submission. Ordinary acceptance pays immediately; disputed decisions hold funds through appeal and pay/refund only on Close. Confirmed findings count separately from task completion; no payment by finding count.

Scope-based completion accepts a useful report with zero bugs. Workflow disagreements become disputes, not unilateral confiscation. A partial task may complete other participants independently; partial base payment within a single participation is deferred in favor of all-base-or-refund arbitration. A moderation decision may add a bonus only inside the funded cap of the task; an appeal compensates the paid amount with new journal rows and never overwrites a settled entry.

Raw screenshots/logs are supported; video, external issue sync, real-time scheduling and automatic account certification are deferred. This is a deliberate smallest coherent MVP, not omission disguised as readiness.

## Implementation boundary

The runtime foundation, the identity package, the project package, the capability package, the credit package, the task publication package, the participation execution package and the evidence package are implemented, built and tested; their evidence is in the verification.md of every change, the archived ones under `openspec/changes/archive/`. The other four packages are specified only: 37 requirements and 111 scenarios in total, 36 unchecked tasks.

Strict validation and this coverage table cannot establish successful exchanges, pilot safety or deployment. Only the runtime foundation, the identity package, the project package, the capability package, the credit package, the task publication package, the participation execution package and the evidence package have native, browser and accessibility evidence.
