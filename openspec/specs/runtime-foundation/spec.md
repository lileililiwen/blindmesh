# runtime-foundation Specification

## Purpose
The runtime foundation delivers the running shell of the product: the C# API, the worker, the PostgreSQL schema, the generated API client and the web application, with the real availability of the services visible to the user.
## Requirements
### Requirement: FOUND-1 Startup

The system SHALL start the C# API, worker, Next.js web, PostgreSQL, private object storage and mail sink using the documented local commands, expose GET /v1/health/live and /v1/health/ready, and display actual service availability without invented counters.

#### Scenario: FOUND-1 success

- **WHEN** a clean checkout has only documented prerequisites and example development configuration
- **THEN** the native build and Compose startup succeed, liveness returns 200, readiness returns 200 only after required dependencies are reachable, and the home page displays a real availability result

#### Scenario: FOUND-1 rejection

- **WHEN** PostgreSQL is unavailable
- **THEN** readiness returns 503, liveness stays 200, and the home page offers a labeled retry without claiming readiness

#### Scenario: FOUND-1 boundary

- **WHEN** APP_ENV is production with test auth enabled
- **THEN** startup fails before accepting requests

### Requirement: FOUND-2 Contracts

The system SHALL publish versioned API OpenAPI and generate packages/api-client reproducibly with standardized errors, pagination, visibility policies, request IDs and scoped transaction/outbox seams.

#### Scenario: FOUND-2 success

- **WHEN** the generation command runs twice without API changes
- **THEN** the generated TypeScript client bytes match and api:check succeeds

#### Scenario: FOUND-2 rejection

- **WHEN** a malformed or unauthorized fixture request is made
- **THEN** the response uses the architecture error envelope without SQL details or sensitive data

#### Scenario: FOUND-2 boundary

- **WHEN** an unsafe request has a mismatched Origin
- **THEN** the request is rejected before any side effect

### Requirement: FOUND-3 Portability

The system SHALL restore pinned .NET and frontend dependencies without sibling source references, adopt shared packages only after feed verification, and document isolated build evidence.

#### Scenario: FOUND-3 success

- **WHEN** NuGet restores with a fresh package cache and no user-level local feed
- **THEN** exact Platform.* versions resolve through Package Source Mapping and both native builds succeed

#### Scenario: FOUND-3 rejection

- **WHEN** the platform feed or a pinned version is unavailable
- **THEN** restore fails visibly and adoption remains blocked

#### Scenario: FOUND-3 boundary

- **WHEN** the project is copied outside the workspace
- **THEN** runtime manifests reference only committed files or versioned packages

### Requirement: FOUND-UX Usable and accessible journey

The system SHALL expose / with authorized state and primary actions, loading/empty/validation/error/permission/success/recovery feedback, responsive layouts and keyboard/screen-reader support as defined in design.md and docs/architecture.md.

#### Scenario: FOUND-UX complete journey

- **WHEN** an authorized actor completes the primary journey in a supported browser
- **THEN** the current server state, next action and committed result are visible and announced without reloading away draft input

#### Scenario: FOUND-UX recover from failure

- **WHEN** a request fails validation, loses permission or encounters an unavailable dependency
- **THEN** the route shows an actionable accessible explanation, preserves safe input where applicable and does not announce success or reveal private data

#### Scenario: FOUND-UX responsive keyboard operation

- **WHEN** the journey is exercised at 360, 768 and 1440 pixels in both themes using only the keyboard
- **THEN** controls remain reachable, focus is visible and restored, text and controls meet contrast requirements and no page-level horizontal scrolling occurs

