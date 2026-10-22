## ADDED Requirements

### Requirement: MATCH-1 Reach

The system SHALL make the matched scope mean exactly that: a task of a matched project and a matched task are readable by the accounts that satisfy every hard condition of the task, and by nobody else.

#### Scenario: MATCH-1 success

- **WHEN** a contributor whose declared capabilities satisfy every hard condition of an open task of a matched project
- **THEN** the task appears in the task list of the contributor and the contributor may apply for a seat of it

#### Scenario: MATCH-1 rejection

- **WHEN** a contributor whose declarations do not satisfy every hard condition reads such a task
- **THEN** the task is not found for the contributor and the eligibility of it names no condition of it

#### Scenario: MATCH-1 boundary

- **WHEN** a project leaves the matched scope or a matched task closes
- **THEN** the reach ends at once and a seat that was already taken is untouched

### Requirement: MATCH-2 Candidates

The system SHALL let the owner of a project read the accounts that fit a task with the condition that each declaration satisfied, and SHALL keep every private field of an account and of a declaration out of that answer.

#### Scenario: MATCH-2 success

- **WHEN** the owner of a project reads the candidates of a task of that project
- **THEN** every account that fits is listed with the declaration and the value that satisfied each hard condition

#### Scenario: MATCH-2 rejection

- **WHEN** an account that is not a member of the project reads the candidates of a task
- **THEN** the request is refused and the answer holds neither a candidate nor a condition

#### Scenario: MATCH-2 boundary

- **WHEN** a declaration of a candidate is withdrawn, private or unavailable
- **THEN** the candidate leaves the list and no private detail of a declaration is ever part of the answer

### Requirement: MATCH-3 Fairness

The system SHALL place an account without an accepted contribution before an account with one among the accounts that fit, and SHALL never claim a quality, a score or a rank.

#### Scenario: MATCH-3 success

- **WHEN** the candidates of a task hold an account without an accepted contribution and an account with one
- **THEN** the account without an accepted contribution comes first and the answer names the reason of the order

#### Scenario: MATCH-3 rejection

- **WHEN** an account does not satisfy every hard condition of a task
- **THEN** the account is never listed, whatever the reputation of the account is

#### Scenario: MATCH-3 boundary

- **WHEN** the candidates of a task are read
- **THEN** the answer carries no score, no weight, no percentage and no quality claim, and the order is stable for the same state

### Requirement: MATCH-UX Usable and accessible journey

The system SHALL expose /tasks/{id}/candidates with authorized state and primary actions, loading/empty/validation/error/permission/success/recovery feedback, responsive layouts and keyboard/screen-reader support as defined in design.md and docs/architecture.md.

#### Scenario: MATCH-UX complete journey

- **WHEN** an authorized actor completes the primary journey in a supported browser
- **THEN** the current server state, next action and committed result are visible and announced without reloading away draft input

#### Scenario: MATCH-UX recover from failure

- **WHEN** a request fails validation, loses permission or encounters an unavailable dependency
- **THEN** the route shows an actionable accessible explanation, preserves safe input where applicable and does not announce success or reveal private data

#### Scenario: MATCH-UX responsive keyboard operation

- **WHEN** the journey is exercised at 360, 768 and 1440 pixels in both themes using only the keyboard
- **THEN** controls remain reachable, focus is visible and restored, text and controls meet contrast requirements and no page-level horizontal scrolling occurs
