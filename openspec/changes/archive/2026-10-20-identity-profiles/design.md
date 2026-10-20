# Design: Sign in safely and manage a public contributor profile

## Context and implementation boundary

Modules: Domain/Identity, Application/Identity, Infrastructure/Identity, Api/Endpoints/Identity; UI routes listed above. Tables users, external_identities, sessions, oauth_states; unique provider+subject and case-folded username. POST /v1/auth/github/start, GET /v1/auth/github/callback, GET /v1/session, POST /v1/logout, GET/PATCH /v1/profile; public GET /v1/people/{username}. Profile input: username 3..30 ASCII letters/digits/hyphen, display_name 1..80, bio <=1000, timezone valid IANA, public skill/language tags <=20 each. OAuth verifies state and email before grant eligibility, never retaining provider tokens.

Exact backend boundaries: src/Blindmesh.Domain/Identity, src/Blindmesh.Application/Identity, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Identity. UI: apps/web App Router for /sign-in, /onboarding, /settings/profile, /people/{username}. Tests: tests/Blindmesh.Tests/Identity and tests/e2e/identity-profiles. Apply EF Core migrations only through Infrastructure/Persistence/Migrations; the API composition root wires services and the worker calls the same application methods. Alter only this package's modules and required composition/test hooks.

## Language, runtime and shared ownership

C# 14, .NET SDK 10.0.400/net10.0, ASP.NET Core 10, EF Core/Npgsql 10, PostgreSQL 17; Next.js 16/React 19/TypeScript 5, Node.js 24/npm. See docs/architecture.md for immutable ledger, lock order, API errors/pagination and session policies. Consume foundation's exact shared PlatformPackageVersion/NuGet mapping and token receipt; no sibling ProjectReference, local feed path or copied platform implementation. Product rules stay in Blindmesh. No second backend language or frontend.

## User experience and interface

Routes: /sign-in, /onboarding, /settings/profile, /people/{username}. Initial entry checks session/visibility, then loads the current state; anonymous private routes redirect to sign-in with an allowlisted return path or return a safe unavailable page. Skeletons announce loading without invented counts. Empty views explain the eligible next action. Inline validation associates messages with fields and focuses the first error. 401 offers sign-in; invisible resources return 404; visible forbidden actions explain the restriction. 409 prompts refresh while preserving draft input; 422 shows field/funds errors; 429 gives retry guidance; 503 offers bounded user retry. Disable duplicate pending submissions but rely on server idempotency. Confirm destructive consequences, show success after server commit and restore focus to the trigger. Never leak private fields in HTML metadata, client caches, notification previews or attachments.

IDENT-UX: use the common shell and semantic tokens, light/dark pre-paint theme, 44px minimum targets, logical headings/landmarks, associated labels and aria-live status. At 360px use one column and labeled rows; at 768px collapse navigation; at 1440px use readable content width. No horizontal page overflow; maintain keyboard tab order, dialog focus containment/restoration, contrast and reduced-motion. Use shared primitives rather than bespoke auth/forms.

## Behavioral model, contracts and failures

- IDENT-1 Session: The system SHALL authenticate GitHub identity using one-use state, issue the secure opaque cookie/session specified in architecture, enforce CSRF/Origin on unsafe methods and revoke access on logout or ban.
- IDENT-2 Profile: The system SHALL support validated onboarding, editable contributor profiles and privacy-filtered public pages without exposing email, OAuth identity details or session data.

HTTP native /v1 and same-origin browser /api/v1; snake_case JSON with GUID/UTC fields and integer long credits. Actor policy is rechecked on every command, list, detail, export and file lookup. Persist expected_version and state history. Mutating financial/publication/application/submission/review/moderation operations use scoped Idempotency-Key records in the same transaction. Lock task, participation/slot, sorted accounts, then ledger; retry serialization/deadlock at most three times. Never run network side effects inside the transaction. Save outbox effects atomically. Cross-module writes call the owning application service with the same scoped DbContext; only Credits writes ledger entries. Return 400/401/403/404/409/413/422/429/503 as defined in architecture, without provider/SQL details.

Migration introduces only package-owned tables/indexes/constraints and preserves prior rows. Verify fresh and upgraded PostgreSQL schemas. Failed commands leave no partial state; recovery uses a forward migration or compensating transaction, never rewrites settled data. Retryable reads show recovery controls; an ambiguous command timeout retries using its original key/payload.

## Verification oracle

Use the commands and seeded actors in verification.md. The acceptance boundary includes each success, rejection and boundary scenario in specs/identity-profiles/spec.md, the UI requirement and its mobile/keyboard/permission checks. Tests run against isolated real PostgreSQL; financial/concurrency checks assert journal sums and unique effects, not just HTTP responses. Browser tests exercise the committed state through generated clients. No test or browser claim is made during proposal authoring.

## Decision ledger

Chosen: product-local modular monolith and shared technical packages after foundation verification. Rejected: sibling runtime coupling, generic workflow engines, four task backends and ad hoc balance editing. Deferred: only the explicit non-goals/roadmap items. Dependencies must be verified and archived before this package is implemented. No material product decision is delegated to an implementer; an unavailable feed/runtime is recorded as a blocker with its exact retry command.
