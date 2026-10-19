## ADDED Requirements

### Requirement: CAP-1 Declaration

The system SHALL let contributors create, edit and withdraw non-sensitive capability declarations without accepting passwords, production cookies, payment credentials or third-party session tokens.

#### Scenario: CAP-1 success

- **WHEN** a contributor saves a permitted device/skill declaration
- **THEN** their settings and permitted directory views show its self-declared status

#### Scenario: CAP-1 rejection

- **WHEN** a credential field or disallowed metadata key is submitted
- **THEN** validation rejects it without persisting or echoing its value

#### Scenario: CAP-1 boundary

- **WHEN** a declaration is withdrawn while a participation uses a lease
- **THEN** future eligibility excludes it while existing participation history stays intact

### Requirement: CAP-2 Eligibility

The system SHALL evaluate hard requirements deterministically and explain unmet conditions without disclosing private capability details or inventing certification.

#### Scenario: CAP-2 success

- **WHEN** an eligible contributor views matching conditions
- **THEN** each required condition is satisfied by an available permitted declaration

#### Scenario: CAP-2 rejection

- **WHEN** a contributor lacks a mandatory region/device/role condition
- **THEN** application eligibility is false with an actionable reason

#### Scenario: CAP-2 boundary

- **WHEN** a capability is changed after an eligibility preview
- **THEN** the authoritative application command rechecks eligibility rather than trusting cached client data

### Requirement: CAP-UX Usable and accessible journey

The system SHALL expose /capabilities, /settings/capabilities, /tasks/{id}/eligibility with authorized state and primary actions, loading/empty/validation/error/permission/success/recovery feedback, responsive layouts and keyboard/screen-reader support as defined in design.md and docs/architecture.md.

#### Scenario: CAP-UX complete journey

- **WHEN** an authorized actor completes the primary journey in a supported browser
- **THEN** the current server state, next action and committed result are visible and announced without reloading away draft input

#### Scenario: CAP-UX recover from failure

- **WHEN** a request fails validation, loses permission or encounters an unavailable dependency
- **THEN** the route shows an actionable accessible explanation, preserves safe input where applicable and does not announce success or reveal private data

#### Scenario: CAP-UX responsive keyboard operation

- **WHEN** the journey is exercised at 360, 768 and 1440 pixels in both themes using only the keyboard
- **THEN** controls remain reachable, focus is visible and restored, text and controls meet contrast requirements and no page-level horizontal scrolling occurs
