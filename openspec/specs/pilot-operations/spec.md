# pilot-operations Specification

## Purpose
The pilot operations package makes the product operable and honest about its own readiness: a documented deployment and rollback path, a daily backup with a restore rehearsal into an isolated database that reconciles the schema, the data, the objects and the ledger before any promotion is claimed, an authorized deletion request that removes the identity while it keeps the minimum audit provenance, and a gate that reports the measured reality of the product and never a target as a measurement.
## Requirements
### Requirement: OPS-1 Recovery

The system SHALL provide a documented deployment/rollback and daily backup/isolated restore workflow with reconciled credits, authorized file access and redacted observability.

#### Scenario: OPS-1 success

- **WHEN** the operator restores a selected backup into an isolated environment
- **THEN** schema/data/object checks and ledger reconciliation pass before any promotion claim

#### Scenario: OPS-1 rejection

- **WHEN** a migration or backup integrity check fails
- **THEN** promotion is blocked and the documented previous image/recovery path remains available

#### Scenario: OPS-1 boundary

- **WHEN** an authorized deletion request affects settled financial records
- **THEN** identity is removed or pseudonymized while minimum audit/ledger provenance is retained

### Requirement: OPS-2 PilotGate

The system SHALL gate public pilot readiness on real supported-browser journeys, WCAG 2.2 AA evidence, responsive layouts, negative authorization tests and measured reliability/performance.

#### Scenario: OPS-2 success

- **WHEN** seeded pilot journeys are exercised in Chromium/Firefox/WebKit with both themes and 360/768/1440px layouts
- **THEN** actual pass/fail, screenshots, axe results and measurements are recorded for the source revision

#### Scenario: OPS-2 rejection

- **WHEN** required flows, authorization checks or restore evidence are unavailable
- **THEN** release remains blocked with the exact failed command and next action

#### Scenario: OPS-2 boundary

- **WHEN** light-load API latency and availability targets are reported
- **THEN** P95 below 500ms and 99.5 percent availability remain targets until their measured observation windows are documented

### Requirement: OPS-UX Usable and accessible journey

The system SHALL expose /admin/operations, /status with authorized state and primary actions, loading/empty/validation/error/permission/success/recovery feedback, responsive layouts and keyboard/screen-reader support as defined in design.md and docs/architecture.md.

#### Scenario: OPS-UX complete journey

- **WHEN** an authorized actor completes the primary journey in a supported browser
- **THEN** the current server state, next action and committed result are visible and announced without reloading away draft input

#### Scenario: OPS-UX recover from failure

- **WHEN** a request fails validation, loses permission or encounters an unavailable dependency
- **THEN** the route shows an actionable accessible explanation, preserves safe input where applicable and does not announce success or reveal private data

#### Scenario: OPS-UX responsive keyboard operation

- **WHEN** the journey is exercised at 360, 768 and 1440 pixels in both themes using only the keyboard
- **THEN** controls remain reachable, focus is visible and restored, text and controls meet contrast requirements and no page-level horizontal scrolling occurs

