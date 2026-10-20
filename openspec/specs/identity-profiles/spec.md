# identity-profiles Specification

## Purpose
The identity package gives every contributor a safe sign in and a public profile: the GitHub identity with a one-use state, the opaque session with its CSRF token, the validated onboarding, the editable profile and the privacy filtered public page.
## Requirements
### Requirement: IDENT-1 Session

The system SHALL authenticate GitHub identity using one-use state, issue the secure opaque cookie/session specified in architecture, enforce CSRF/Origin on unsafe methods and revoke access on logout or ban.

#### Scenario: IDENT-1 success

- **WHEN** a visitor completes a valid GitHub callback
- **THEN** the dashboard opens under their stable identity and the raw provider token is discarded

#### Scenario: IDENT-1 rejection

- **WHEN** state is expired, mismatched or replayed
- **THEN** authentication fails safely and no session is created

#### Scenario: IDENT-1 boundary

- **WHEN** a banned user reuses a previously valid cookie
- **THEN** the next request is refused and private cached data is cleared

### Requirement: IDENT-2 Profile

The system SHALL support validated onboarding, editable contributor profiles and privacy-filtered public pages without exposing email, OAuth identity details or session data.

#### Scenario: IDENT-2 success

- **WHEN** a signed-in user saves a valid unique username and profile
- **THEN** the public profile displays allowed fields and the settings screen confirms the committed version

#### Scenario: IDENT-2 rejection

- **WHEN** another user or a stale expected_version attempts to edit
- **THEN** the server rejects the operation with 403 or 409 and the screen preserves recoverable input

#### Scenario: IDENT-2 boundary

- **WHEN** a visitor opens a missing or non-public profile
- **THEN** the same safe 404 page appears without sensitive metadata

### Requirement: IDENT-UX Usable and accessible journey

The system SHALL expose /sign-in, /onboarding, /settings/profile, /people/{username} with authorized state and primary actions, loading/empty/validation/error/permission/success/recovery feedback, responsive layouts and keyboard/screen-reader support as defined in design.md and docs/architecture.md.

#### Scenario: IDENT-UX complete journey

- **WHEN** an authorized actor completes the primary journey in a supported browser
- **THEN** the current server state, next action and committed result are visible and announced without reloading away draft input

#### Scenario: IDENT-UX recover from failure

- **WHEN** a request fails validation, loses permission or encounters an unavailable dependency
- **THEN** the route shows an actionable accessible explanation, preserves safe input where applicable and does not announce success or reveal private data

#### Scenario: IDENT-UX responsive keyboard operation

- **WHEN** the journey is exercised at 360, 768 and 1440 pixels in both themes using only the keyboard
- **THEN** controls remain reachable, focus is visible and restored, text and controls meet contrast requirements and no page-level horizontal scrolling occurs

