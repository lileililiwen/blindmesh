# task-publication Specification

## Purpose
The task publication package owns the marketplace of work: one task model with explicit scope, modes, acceptance rules and deadlines, an atomic escrow of the maximum budget before a task opens, a discovery that never leaks a task that may not be seen, and a usable interface for the publisher and the candidate.
## Requirements
### Requirement: TASK-1 Publication

The system SHALL publish one configurable task model with explicit scope, modes, acceptance rules, deadlines and maximum escrow reserved atomically before Open status is visible.

#### Scenario: TASK-1 success

- **WHEN** an authorized owner publishes a valid task with sufficient balance
- **THEN** the detail shows Open and the full base-plus-bonus budget is reserved

#### Scenario: TASK-1 rejection

- **WHEN** required Verify steps are absent or the balance is insufficient
- **THEN** validation fails with no Open task or partial reservation

#### Scenario: TASK-1 boundary

- **WHEN** a publish request is retried after the response is lost
- **THEN** the same task and reservation are returned exactly once

### Requirement: TASK-2 Discovery

The system SHALL expose bounded marketplace discovery and authorized task detail while preserving parent visibility and excluding sensitive Access instructions from candidate summaries.

#### Scenario: TASK-2 success

- **WHEN** a visitor filters public tasks by mode and capability requirements
- **THEN** stable cursor pages show matching non-sensitive summaries

#### Scenario: TASK-2 rejection

- **WHEN** a visitor requests a private task or its export
- **THEN** the result is 404 without leaking project or contributor data

#### Scenario: TASK-2 boundary

- **WHEN** a task is cancelled while work remains active
- **THEN** admission closes and unused slots refund while active/submitted/disputed budgets remain protected

### Requirement: TASK-UX Usable and accessible journey

The system SHALL expose /tasks, /tasks/new, /tasks/{id}, /projects/{id}/tasks with authorized state and primary actions, loading/empty/validation/error/permission/success/recovery feedback, responsive layouts and keyboard/screen-reader support as defined in design.md and docs/architecture.md.

#### Scenario: TASK-UX complete journey

- **WHEN** an authorized actor completes the primary journey in a supported browser
- **THEN** the current server state, next action and committed result are visible and announced without reloading away draft input

#### Scenario: TASK-UX recover from failure

- **WHEN** a request fails validation, loses permission or encounters an unavailable dependency
- **THEN** the route shows an actionable accessible explanation, preserves safe input where applicable and does not announce success or reveal private data

#### Scenario: TASK-UX responsive keyboard operation

- **WHEN** the journey is exercised at 360, 768 and 1440 pixels in both themes using only the keyboard
- **THEN** controls remain reachable, focus is visible and restored, text and controls meet contrast requirements and no page-level horizontal scrolling occurs

