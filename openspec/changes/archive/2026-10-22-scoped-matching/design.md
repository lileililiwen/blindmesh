# Design: Let a project find the contributors who fit its tasks

## Context and implementation boundary

Matching module; a computed view, no table of its own. The scope rule of the task list, of the task detail and of the eligibility, the candidate list of a task and the exploration order of that list. The evaluation of the hard conditions stays with the capability module, the conditions of a task stay with the task module, the reputation of an account stays with the reputation module. This module owns the match, the reach and the order, and it owns no row.

GET /v1/tasks/{id}/candidates returns the accounts whose declared capabilities satisfy every hard condition of the task, with the condition that each declaration satisfied. The list is ordered so that an account without an accepted contribution comes first, and the answer never carries a score, a weight, a percentage or a quality claim.

The scope rule: an account that is not an insider of the project of a task reads the task when the project is public and the task is public, or when the account satisfies every hard condition of the task and either the task is `matched` or the project is `matched`. An insider reads everything of its project. A private project stays with its insiders. The rule is applied by the same code for the list, for the detail, for the eligibility and for the project detail, so a task that the list does not show is never readable through another route.

Exact backend boundaries: src/Blindmesh.Domain/Matching, src/Blindmesh.Application/Matching, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Matching. UI: apps/web App Router for /tasks/{id}/candidates. Tests: tests/Blindmesh.Tests/Matching and tests/e2e/scoped-matching. Apply EF Core migrations only through Infrastructure/Persistence/Migrations; the API composition root wires services. Alter only this package's modules and required composition/test hooks.

## Language, runtime and shared ownership

C# 14, .NET SDK 10.0.400/net10.0, ASP.NET Core 10, EF Core/Npgsql 10, PostgreSQL 17; Next.js 16/React 19/TypeScript 5, Node.js 24/npm. See docs/architecture.md for immutable ledger, lock order, API errors/pagination and session policies. Consume foundation's exact shared PlatformPackageVersion/NuGet mapping and token receipt; no sibling ProjectReference, local feed path or copied platform implementation. Product rules stay in Blindmesh. No second backend language or frontend.

The seams between the modules are the pattern of the seam of the dispute of the evidence: an interface in the application layer, the implementation in the module that owns the data, the registration in the composition root. The seams are:

| Seam | Declared in | Implemented by | Answers |
|---|---|---|---|
| `ITaskRequirementsReader` | Application/Tasks | the task module | the hard conditions of a task, without a scope rule |
| `ITaskMatchSeam` | Application/Tasks | the matching module | the tasks of a project that an account satisfies, and the single test of one task |
| `ICandidateMatchReader` | Application/Capabilities | the capability module | the matchable declarations of every account and the evaluation of one account against a list of conditions |
| `IReputationStandingReader` | Application/Reputation | the reputation module | the number of the accepted contributions of an account |

No seam creates a cycle at runtime: the matching module reads the conditions through `ITaskRequirementsReader` and never through the reader of the scope, and the capability module keeps the evaluation of the conditions.

## User experience and interface

Routes: /tasks/{id}/candidates. Initial entry checks session/visibility, then loads the current state; anonymous private routes redirect to sign-in with an allowlisted return path or return a safe unavailable page. Skeletons announce loading without invented counts. Empty views explain the eligible next action. Inline validation associates messages with fields and focuses the first error. 401 offers sign-in; invisible resources return 404; visible forbidden actions explain the restriction. 409 prompts refresh while preserving draft input; 422 shows field/funds errors; 429 gives retry guidance; 503 offers bounded user retry. Disable duplicate pending submissions but rely on server idempotency. Confirm destructive consequences, show success after server commit and restore focus to the trigger. Never leak private fields in HTML metadata, client caches, notification previews or attachments.

MATCH-UX: use the common shell and semantic tokens, light/dark pre-paint theme, 44px minimum targets, logical headings/landmarks, associated labels and aria-live status. At 360px use one column and labeled rows; at 768px collapse navigation; at 1440px use readable content width. No horizontal page overflow; maintain keyboard tab order, dialog focus containment/restoration, contrast and reduced-motion. Use shared primitives rather than bespoke auth/forms.

The page of the candidates belongs to the owner of the task. It shows, for every account that fits, the public profile, the declaration that satisfied every condition with the value of the condition, and the order with its reason: either "no accepted contribution yet" or "an accepted contribution is on the account". The page never shows a score, a percentage, a rank number or a quality claim, and it never shows the private details of a declaration. The page states that the order is an exploration rule and not a ranking. The empty state says that no account fits the conditions today and names the conditions.

## Behavioral model, contracts and failures

- MATCH-1 Reach: The system SHALL make the matched scope mean exactly that: a task of a matched project or a matched task is readable by the accounts that satisfy every hard condition, and by nobody else.
- MATCH-2 Candidates: The system SHALL let the owner of a project read the accounts that fit a task with the condition that each declaration satisfied, and SHALL keep every private field of an account and of a declaration out of that answer.
- MATCH-3 Fairness: The system SHALL place an account without an accepted contribution before an account with one among the accounts that fit, and SHALL never claim a quality, a score or a rank.

HTTP native /v1 and same-origin browser /api/v1; snake_case JSON with GUID/UTC fields and integer long credits. Actor policy is rechecked on every command, list, detail, export and file lookup. Persist expected_version and state history. Mutating financial/publication/application/submission/review/moderation operations use scoped Idempotency-Key records in the same transaction. Lock task, participation/slot, sorted accounts, then ledger; retry serialization/deadlock at most three times. Never run network side effects inside the transaction. Save outbox effects atomically. Cross-module writes call the owning application service with the same scoped DbContext; only Credits writes ledger entries. Return 400/401/403/404/409/413/422/429/503 as defined in architecture, without provider/SQL details.

The candidate list is a read, so it is not a mutation and it needs no idempotency key. The scope rule runs on a read, so it must not write. The account that the list names is not told that it was listed: the list is the tool of the owner, and a push belongs to the notification package. The order of the list is stable for the same state, so two reads of the same state return the same order. The exploration rule reads the accepted contributions of an account from the reputation module; an account that the reputation module does not know has zero accepted contributions and is therefore an explorer.

The rule of the scope is applied by one method of the matching module, and the task module, the project module and the eligibility route all call it. A route that shows a task that the list hides is a leak, so the same seam answers every read.

Migration introduces only package-owned tables/indexes/constraints and preserves prior rows. This package owns no table, so it adds no migration. Verify fresh and upgraded PostgreSQL schemas. Failed commands leave no partial state; recovery uses a forward migration or compensating transaction, never rewrites settled data. Retryable reads show recovery controls; an ambiguous command timeout retries using its original key/payload.

## Verification oracle

Use the commands and seeded actors in verification.md. The acceptance boundary includes each success, rejection and boundary scenario in specs/scoped-matching/spec.md, the UI requirement and its mobile/keyboard/permission checks. Tests run against isolated real PostgreSQL; the checks assert the scope, the order and the redaction, not just HTTP responses. Browser tests exercise the committed state through generated clients. No test or browser claim is made during proposal authoring.

## Decision ledger

Chosen: product-local modular monolith and shared technical packages after foundation verification. Rejected: sibling runtime coupling, generic workflow engines, four task backends and ad hoc balance editing. Deferred: only the explicit non-goals/roadmap items. Dependencies must be verified and archived before this package is implemented. No material product decision is delegated to an implementer; an unavailable feed/runtime is recorded as a blocker with its exact retry command.

The soft weights of the PRD §5.2 are rejected for this package on purpose: the PRD calls them hypotheses that must be calibrated and must not be used as a quality guarantee, so a number that looks like a score would be a claim the product cannot back. The exploration rule of MATCH-3 is the only order the list has, and it is a fairness rule with a readable reason, not a ranking.
