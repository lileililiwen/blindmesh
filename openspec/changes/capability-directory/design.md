# Design: Declare capabilities and evaluate hard matching conditions

## Context and implementation boundary

Modules Capabilities; tables capabilities, capability_tags and capability_status_history. GET/POST /v1/capabilities; PATCH /v1/capabilities/{id}; POST /v1/capabilities/{id}/withdraw. Data kind account/device/skill/region/network/role, public label 1..100, tags <=20, visibility public/private, available boolean, metadata allowlist of non-sensitive OS/browser/device/region/provider identifiers. Verification defaults self_declared; only moderator evidence can change verified status. Eligibility evaluates published task hard conditions against public Available capabilities and account restrictions; no weighted ranking or remote account login.

Exact backend boundaries: src/Blindmesh.Domain/Capabilities, src/Blindmesh.Application/Capabilities, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Capabilities. UI: apps/web App Router for /capabilities, /settings/capabilities, /tasks/{id}/eligibility. Tests: tests/Blindmesh.Tests/Capabilities and tests/e2e/capability-directory. Apply EF Core migrations only through Infrastructure/Persistence/Migrations; the API composition root wires services and the worker calls the same application methods. Alter only this package's modules and required composition/test hooks.

## Language, runtime and shared ownership

C# 14, .NET SDK 10.0.400/net10.0, ASP.NET Core 10, EF Core/Npgsql 10, PostgreSQL 17; Next.js 16/React 19/TypeScript 5, Node.js 24/npm. See docs/architecture.md for immutable ledger, lock order, API errors/pagination and session policies. Consume foundation's exact shared PlatformPackageVersion/NuGet mapping and token receipt; no sibling ProjectReference, local feed path or copied platform implementation. Product rules stay in Blindmesh. No second backend language or frontend.

## User experience and interface

Routes: /capabilities, /settings/capabilities, /tasks/{id}/eligibility. Initial entry checks session/visibility, then loads the current state; anonymous private routes redirect to sign-in with an allowlisted return path or return a safe unavailable page. Skeletons announce loading without invented counts. Empty views explain the eligible next action. Inline validation associates messages with fields and focuses the first error. 401 offers sign-in; invisible resources return 404; visible forbidden actions explain the restriction. 409 prompts refresh while preserving draft input; 422 shows field/funds errors; 429 gives retry guidance; 503 offers bounded user retry. Disable duplicate pending submissions but rely on server idempotency. Confirm destructive consequences, show success after server commit and restore focus to the trigger. Never leak private fields in HTML metadata, client caches, notification previews or attachments.

CAP-UX: use the common shell and semantic tokens, light/dark pre-paint theme, 44px minimum targets, logical headings/landmarks, associated labels and aria-live status. At 360px use one column and labeled rows; at 768px collapse navigation; at 1440px use readable content width. No horizontal page overflow; maintain keyboard tab order, dialog focus containment/restoration, contrast and reduced-motion. Use shared primitives rather than bespoke auth/forms.

## Behavioral model, contracts and failures

- CAP-1 Declaration: The system SHALL let contributors create, edit and withdraw non-sensitive capability declarations without accepting passwords, production cookies, payment credentials or third-party session tokens.
- CAP-2 Eligibility: The system SHALL evaluate hard requirements deterministically and explain unmet conditions without disclosing private capability details or inventing certification.

HTTP native /v1 and same-origin browser /api/v1; snake_case JSON with GUID/UTC fields and integer long credits. Actor policy is rechecked on every command, list, detail, export and file lookup. Persist expected_version and state history. Mutating financial/publication/application/submission/review/moderation operations use scoped Idempotency-Key records in the same transaction. Lock task, participation/slot, sorted accounts, then ledger; retry serialization/deadlock at most three times. Never run network side effects inside the transaction. Save outbox effects atomically. Cross-module writes call the owning application service with the same scoped DbContext; only Credits writes ledger entries. Return 400/401/403/404/409/413/422/429/503 as defined in architecture, without provider/SQL details.

Migration introduces only package-owned tables/indexes/constraints and preserves prior rows. Verify fresh and upgraded PostgreSQL schemas. Failed commands leave no partial state; recovery uses a forward migration or compensating transaction, never rewrites settled data. Retryable reads show recovery controls; an ambiguous command timeout retries using its original key/payload.

## Verification oracle

Use the commands and seeded actors in verification.md. The acceptance boundary includes each success, rejection and boundary scenario in specs/capability-directory/spec.md, the UI requirement and its mobile/keyboard/permission checks. Tests run against isolated real PostgreSQL; financial/concurrency checks assert journal sums and unique effects, not just HTTP responses. Browser tests exercise the committed state through generated clients. No test or browser claim is made during proposal authoring.

## Decision ledger

Chosen: product-local modular monolith and shared technical packages after foundation verification. Rejected: sibling runtime coupling, generic workflow engines, four task backends and ad hoc balance editing. Deferred: only the explicit non-goals/roadmap items. Dependencies must be verified and archived before this package is implemented. No material product decision is delegated to an implementer; an unavailable feed/runtime is recorded as a blocker with its exact retry command.
