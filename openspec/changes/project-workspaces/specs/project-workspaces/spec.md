## ADDED Requirements

### Requirement: PROJ-1 Lifecycle

The system SHALL let project owners create Draft projects, activate/pause them, record versions and archive them through the architecture state machine with optimistic concurrency.

#### Scenario: PROJ-1 success

- **WHEN** an owner activates a valid Draft and adds a version
- **THEN** the project page shows the committed state and version history

#### Scenario: PROJ-1 rejection

- **WHEN** a viewer edits content or an owner uses an illegal transition
- **THEN** the mutation is rejected and existing data remains unchanged

#### Scenario: PROJ-1 boundary

- **WHEN** a project with historical activity is archived
- **THEN** activity is retained and new tasks are prohibited

### Requirement: PROJ-2 Visibility

The system SHALL enforce project visibility on every query, nested object, export and membership operation, with owner-only membership and visibility administration.

#### Scenario: PROJ-2 success

- **WHEN** an owner adds an existing user as a viewer
- **THEN** that viewer can read private project data but cannot edit it

#### Scenario: PROJ-2 rejection

- **WHEN** an unrelated user requests private project data
- **THEN** the response is 404 without title, counts or attachment details

#### Scenario: PROJ-2 boundary

- **WHEN** the owner removes membership or makes the project private
- **THEN** the next read and dependent notification/attachment access honor the changed policy

### Requirement: PROJ-UX Usable and accessible journey

The system SHALL expose /projects, /projects/new, /projects/{id}, /projects/{id}/settings with authorized state and primary actions, loading/empty/validation/error/permission/success/recovery feedback, responsive layouts and keyboard/screen-reader support as defined in design.md and docs/architecture.md.

#### Scenario: PROJ-UX complete journey

- **WHEN** an authorized actor completes the primary journey in a supported browser
- **THEN** the current server state, next action and committed result are visible and announced without reloading away draft input

#### Scenario: PROJ-UX recover from failure

- **WHEN** a request fails validation, loses permission or encounters an unavailable dependency
- **THEN** the route shows an actionable accessible explanation, preserves safe input where applicable and does not announce success or reveal private data

#### Scenario: PROJ-UX responsive keyboard operation

- **WHEN** the journey is exercised at 360, 768 and 1440 pixels in both themes using only the keyboard
- **THEN** controls remain reachable, focus is visible and restored, text and controls meet contrast requirements and no page-level horizontal scrolling occurs
