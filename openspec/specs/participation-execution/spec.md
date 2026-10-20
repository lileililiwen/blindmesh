# participation-execution Specification

## Purpose
The participation execution package owns the seat of a contributor in a task: an application that holds no slot, an acceptance that leases exactly one slot under concurrency, a start that moves the base reward of the slot into the pending bucket of the contributor, and the deadlines of the architecture — 48 hours to apply, 24 hours to start and seven days of execution — that keep every other participant and every escrow intact.
## Requirements
### Requirement: PART-1 Seat

The system SHALL separate application, acceptance and execution with authorized transitions, hard-condition rechecks and unique slot leases under concurrent acceptance.

#### Scenario: PART-1 success

- **WHEN** an owner accepts an eligible applicant for an available slot
- **THEN** that participant can start and sees their own instructions/deadline

#### Scenario: PART-1 rejection

- **WHEN** two acceptance commands contend for the last slot
- **THEN** exactly one wins and the other returns 409 without duplicate reservation or lease

#### Scenario: PART-1 boundary

- **WHEN** a project member attempts to apply to earn credits
- **THEN** the application is rejected regardless of public capability matches

### Requirement: PART-2 Deadlines

The system SHALL apply the architecture's 48h application, 24h start and bounded execution deadlines, preserving other participants and submitted/disputed budgets.

#### Scenario: PART-2 success

- **WHEN** an accepted contributor starts in time
- **THEN** the visible deadline is min(task deadline, start plus seven days) and task status is recomputed

#### Scenario: PART-2 rejection

- **WHEN** a deadline worker races a start/submit command
- **THEN** row locks produce one legal transition and one idempotent financial outcome

#### Scenario: PART-2 boundary

- **WHEN** the owner cancels a task with running participants
- **THEN** existing work retains its allowed submission grace and escrow while admissions close

### Requirement: PART-UX Usable and accessible journey

The system SHALL expose /tasks/{id}/apply, /work, /work/{id} with authorized state and primary actions, loading/empty/validation/error/permission/success/recovery feedback, responsive layouts and keyboard/screen-reader support as defined in design.md and docs/architecture.md.

#### Scenario: PART-UX complete journey

- **WHEN** an authorized actor completes the primary journey in a supported browser
- **THEN** the current server state, next action and committed result are visible and announced without reloading away draft input

#### Scenario: PART-UX recover from failure

- **WHEN** a request fails validation, loses permission or encounters an unavailable dependency
- **THEN** the route shows an actionable accessible explanation, preserves safe input where applicable and does not announce success or reveal private data

#### Scenario: PART-UX responsive keyboard operation

- **WHEN** the journey is exercised at 360, 768 and 1440 pixels in both themes using only the keyboard
- **THEN** controls remain reachable, focus is visible and restored, text and controls meet contrast requirements and no page-level horizontal scrolling occurs

