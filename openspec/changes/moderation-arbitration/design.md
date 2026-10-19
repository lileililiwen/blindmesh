# Design: Resolve reports and credit disputes with accountable moderation

## Context and implementation boundary

Moderation module; reports, disputes, dispute_evidence, moderator_assignments and moderation_audit. POST /v1/reports; POST /v1/submissions/{id}/disputes; GET /v1/disputes/{id}; POST /v1/admin/disputes/{id}/request-evidence, /resolve, /close; POST /v1/disputes/{id}/appeal; POST /v1/admin/users/{id}/ban. Report/dispute reason 20..5000, resolution award_base boolean and bonus within funded cap, reason >=20. One active dispute per submission; freeze settlement until resolution. Moderator must have no project membership or participant/account conflict. Appeal once within seven days, different moderator, no historical ledger edits; any award change uses a compensating balanced transaction with non-negative accounts.

Exact backend boundaries: src/Blindmesh.Domain/Moderation, src/Blindmesh.Application/Moderation, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Moderation. UI: apps/web App Router for /reports/new, /disputes/{id}, /admin/reports, /admin/disputes. Tests: tests/Blindmesh.Tests/Moderation and tests/e2e/moderation-arbitration. Apply EF Core migrations only through Infrastructure/Persistence/Migrations; the API composition root wires services and the worker calls the same application methods. Alter only this package's modules and required composition/test hooks.

## Language, runtime and shared ownership

C# 14, .NET SDK 10.0.400/net10.0, ASP.NET Core 10, EF Core/Npgsql 10, PostgreSQL 17; Next.js 16/React 19/TypeScript 5, Node.js 24/npm. See docs/architecture.md for immutable ledger, lock order, API errors/pagination and session policies. Consume foundation's exact shared PlatformPackageVersion/NuGet mapping and token receipt; no sibling ProjectReference, local feed path or copied platform implementation. Product rules stay in Blindmesh. No second backend language or frontend.

## User experience and interface

Routes: /reports/new, /disputes/{id}, /admin/reports, /admin/disputes. Initial entry checks session/visibility, then loads the current state; anonymous private routes redirect to sign-in with an allowlisted return path or return a safe unavailable page. Skeletons announce loading without invented counts. Empty views explain the eligible next action. Inline validation associates messages with fields and focuses the first error. 401 offers sign-in; invisible resources return 404; visible forbidden actions explain the restriction. 409 prompts refresh while preserving draft input; 422 shows field/funds errors; 429 gives retry guidance; 503 offers bounded user retry. Disable duplicate pending submissions but rely on server idempotency. Confirm destructive consequences, show success after server commit and restore focus to the trigger. Never leak private fields in HTML metadata, client caches, notification previews or attachments.

MOD-UX: use the common shell and semantic tokens, light/dark pre-paint theme, 44px minimum targets, logical headings/landmarks, associated labels and aria-live status. At 360px use one column and labeled rows; at 768px collapse navigation; at 1440px use readable content width. No horizontal page overflow; maintain keyboard tab order, dialog focus containment/restoration, contrast and reduced-motion. Use shared primitives rather than bespoke auth/forms.

## Behavioral model, contracts and failures

- MOD-1 Arbitration: The system SHALL expose reasoned role-restricted dispute resolution that preserves escrow until a conflict-free moderator commits a funded Credits outcome.
- MOD-2 Safety: The system SHALL support private abuse reporting, evidence-limited moderation, auditable bans and safe content restriction without leaking report identities or editing balances.

HTTP native /v1 and same-origin browser /api/v1; snake_case JSON with GUID/UTC fields and integer long credits. Actor policy is rechecked on every command, list, detail, export and file lookup. Persist expected_version and state history. Mutating financial/publication/application/submission/review/moderation operations use scoped Idempotency-Key records in the same transaction. Lock task, participation/slot, sorted accounts, then ledger; retry serialization/deadlock at most three times. Never run network side effects inside the transaction. Save outbox effects atomically. Cross-module writes call the owning application service with the same scoped DbContext; only Credits writes ledger entries. Return 400/401/403/404/409/413/422/429/503 as defined in architecture, without provider/SQL details.

Migration introduces only package-owned tables/indexes/constraints and preserves prior rows. Verify fresh and upgraded PostgreSQL schemas. Failed commands leave no partial state; recovery uses a forward migration or compensating transaction, never rewrites settled data. Retryable reads show recovery controls; an ambiguous command timeout retries using its original key/payload.

## Verification oracle

Use the commands and seeded actors in verification.md. The acceptance boundary includes each success, rejection and boundary scenario in specs/moderation-arbitration/spec.md, the UI requirement and its mobile/keyboard/permission checks. Tests run against isolated real PostgreSQL; financial/concurrency checks assert journal sums and unique effects, not just HTTP responses. Browser tests exercise the committed state through generated clients. No test or browser claim is made during proposal authoring.

## Decision ledger

Chosen: product-local modular monolith and shared technical packages after foundation verification. Rejected: sibling runtime coupling, generic workflow engines, four task backends and ad hoc balance editing. Deferred: only the explicit non-goals/roadmap items. Dependencies must be verified and archived before this package is implemented. No material product decision is delegated to an implementer; an unavailable feed/runtime is recorded as a blocker with its exact retry command.
