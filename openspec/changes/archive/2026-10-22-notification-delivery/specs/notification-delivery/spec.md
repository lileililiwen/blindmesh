## ADDED Requirements

### Requirement: NOTIF-1 Delivery

The system SHALL deliver durable event-driven in-app notifications and retryable SMTP email with unique delivery identities, bounded retries and actionable operator failures.

#### Scenario: NOTIF-1 success

- **WHEN** a committed acceptance or dispute event is processed
- **THEN** one in-app item appears and an eligible email is sent with a stable Message-ID

#### Scenario: NOTIF-1 rejection

- **WHEN** SMTP times out after an uncertain acceptance
- **THEN** retry state persists and logs state the uncertainty without claiming exactly-once email

#### Scenario: NOTIF-1 boundary

- **WHEN** the worker exhausts five retry delays
- **THEN** the delivery is dead-lettered with a redacted operator alert while other deliveries continue

### Requirement: NOTIF-2 Preferences

The system SHALL expose accessible read/unread and preference controls while rechecking current authorization on notification previews and deep links.

#### Scenario: NOTIF-2 success

- **WHEN** a recipient reads an item and changes an optional email preference
- **THEN** unread state and preferences persist with server-confirmed feedback

#### Scenario: NOTIF-2 rejection

- **WHEN** another account requests the item or marks it read
- **THEN** the server refuses without exposing summary or actor identity

#### Scenario: NOTIF-2 boundary

- **WHEN** membership is revoked before a queued email sends
- **THEN** the message is suppressed or reduced to a safe generic account notice without private content

### Requirement: NOTIF-UX Usable and accessible journey

The system SHALL expose /notifications, /settings/notifications with authorized state and primary actions, loading/empty/validation/error/permission/success/recovery feedback, responsive layouts and keyboard/screen-reader support as defined in design.md and docs/architecture.md.

#### Scenario: NOTIF-UX complete journey

- **WHEN** an authorized actor completes the primary journey in a supported browser
- **THEN** the current server state, next action and committed result are visible and announced without reloading away draft input

#### Scenario: NOTIF-UX recover from failure

- **WHEN** a request fails validation, loses permission or encounters an unavailable dependency
- **THEN** the route shows an actionable accessible explanation, preserves safe input where applicable and does not announce success or reveal private data

#### Scenario: NOTIF-UX responsive keyboard operation

- **WHEN** the journey is exercised at 360, 768 and 1440 pixels in both themes using only the keyboard
- **THEN** controls remain reachable, focus is visible and restored, text and controls meet contrast requirements and no page-level horizontal scrolling occurs
