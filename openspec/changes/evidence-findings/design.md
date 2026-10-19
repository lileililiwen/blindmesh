# Design: Submit protected evidence and settle reviewed work

## Context and implementation boundary

Findings module; submissions, submission_revisions, findings, finding_history and attachments. POST /v1/participations/{id}/submissions; GET /v1/submissions/{id}; POST /v1/submissions/{id}/accept, /request-changes, /dispute; PATCH /v1/findings/{id}; POST /v1/attachments/intents and /complete; GET /v1/attachments/{id}/download. Submission has summary 1..5000, scope_completed boolean, steps/results for Verify, tested areas/clean-result explanation for Explore; findings category bug/ux/performance/security/accessibility/compatibility/documentation, severity info/low/medium/high/critical, title <=140, reproduction <=10000, expected/actual <=5000. Text/log <=2MiB, PNG/JPEG/WebP <=10MiB, maximum ten attachments per revision; reject executable, SVG/HTML and archive uploads. Private quarantine, MIME/signature check, malware scan before readable; signed URLs <=60s. Reviewer can accept explicit bonus <=cap or request one correction. Credits is called inside the shared transaction; finding classification never decides earned base pay.

Exact backend boundaries: src/Blindmesh.Domain/Findings, src/Blindmesh.Application/Findings, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Findings. UI: apps/web App Router for /work/{id}/submit, /projects/{id}/reviews, /findings/{id}. Tests: tests/Blindmesh.Tests/Findings and tests/e2e/evidence-findings. Apply EF Core migrations only through Infrastructure/Persistence/Migrations; the API composition root wires services and the worker calls the same application methods. Alter only this package's modules and required composition/test hooks.

## Language, runtime and shared ownership

C# 14, .NET SDK 10.0.400/net10.0, ASP.NET Core 10, EF Core/Npgsql 10, PostgreSQL 17; Next.js 16/React 19/TypeScript 5, Node.js 24/npm. See docs/architecture.md for immutable ledger, lock order, API errors/pagination and session policies. Consume foundation's exact shared PlatformPackageVersion/NuGet mapping and token receipt; no sibling ProjectReference, local feed path or copied platform implementation. Product rules stay in Blindmesh. No second backend language or frontend.

## User experience and interface

Routes: /work/{id}/submit, /projects/{id}/reviews, /findings/{id}. Initial entry checks session/visibility, then loads the current state; anonymous private routes redirect to sign-in with an allowlisted return path or return a safe unavailable page. Skeletons announce loading without invented counts. Empty views explain the eligible next action. Inline validation associates messages with fields and focuses the first error. 401 offers sign-in; invisible resources return 404; visible forbidden actions explain the restriction. 409 prompts refresh while preserving draft input; 422 shows field/funds errors; 429 gives retry guidance; 503 offers bounded user retry. Disable duplicate pending submissions but rely on server idempotency. Confirm destructive consequences, show success after server commit and restore focus to the trigger. Never leak private fields in HTML metadata, client caches, notification previews or attachments.

EVID-UX: use the common shell and semantic tokens, light/dark pre-paint theme, 44px minimum targets, logical headings/landmarks, associated labels and aria-live status. At 360px use one column and labeled rows; at 768px collapse navigation; at 1440px use readable content width. No horizontal page overflow; maintain keyboard tab order, dialog focus containment/restoration, contrast and reduced-motion. Use shared primitives rather than bespoke auth/forms.

## Behavioral model, contracts and failures

- EVID-1 Evidence: The system SHALL accept valid structured submissions and findings with private validated attachments, immutable submitted revisions and independent finding status.
- EVID-2 Review: The system SHALL settle accepted work through Credits, support one 48h correction request, and auto-accept base after 72h only when no correction or dispute is open.

HTTP native /v1 and same-origin browser /api/v1; snake_case JSON with GUID/UTC fields and integer long credits. Actor policy is rechecked on every command, list, detail, export and file lookup. Persist expected_version and state history. Mutating financial/publication/application/submission/review/moderation operations use scoped Idempotency-Key records in the same transaction. Lock task, participation/slot, sorted accounts, then ledger; retry serialization/deadlock at most three times. Never run network side effects inside the transaction. Save outbox effects atomically. Cross-module writes call the owning application service with the same scoped DbContext; only Credits writes ledger entries. Return 400/401/403/404/409/413/422/429/503 as defined in architecture, without provider/SQL details.

Migration introduces only package-owned tables/indexes/constraints and preserves prior rows. Verify fresh and upgraded PostgreSQL schemas. Failed commands leave no partial state; recovery uses a forward migration or compensating transaction, never rewrites settled data. Retryable reads show recovery controls; an ambiguous command timeout retries using its original key/payload.

## Verification oracle

Use the commands and seeded actors in verification.md. The acceptance boundary includes each success, rejection and boundary scenario in specs/evidence-findings/spec.md, the UI requirement and its mobile/keyboard/permission checks. Tests run against isolated real PostgreSQL; financial/concurrency checks assert journal sums and unique effects, not just HTTP responses. Browser tests exercise the committed state through generated clients. No test or browser claim is made during proposal authoring.

## Decision ledger

Chosen: product-local modular monolith and shared technical packages after foundation verification. Rejected: sibling runtime coupling, generic workflow engines, four task backends and ad hoc balance editing. Deferred: only the explicit non-goals/roadmap items. Dependencies must be verified and archived before this package is implemented. No material product decision is delegated to an implementer; an unavailable feed/runtime is recorded as a blocker with its exact retry command.
