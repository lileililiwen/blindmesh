## ADDED Requirements

### Requirement: CRED-1 Conservation

The system SHALL maintain balanced immutable ledger entries, non-negative ordinary user buckets, atomic idempotent effects and independently reconstructable available/reserved/pending balances.

#### Scenario: CRED-1 success

- **WHEN** a valid reservation or settlement commits
- **THEN** entry sums are zero and cached balances reconcile to journal balances

#### Scenario: CRED-1 rejection

- **WHEN** concurrent operations would overspend or reuse a key with altered payload
- **THEN** one valid operation commits and the invalid operation returns a conflict without partial entries

#### Scenario: CRED-1 boundary

- **WHEN** an operation times out after commit and is retried with the same payload/key
- **THEN** the original result is returned without additional entries

### Requirement: CRED-2 Grant

The system SHALL issue at most one 60-credit grant per eligible GitHub subject, enforce the global 6000-per-24h cap and 30-day expiry, and preserve reserved grant provenance.

#### Scenario: CRED-2 success

- **WHEN** an eligible verified-email account at least seven days old accepts rules and claims within limits
- **THEN** exactly 60 credits appear with the expiry date

#### Scenario: CRED-2 rejection

- **WHEN** the subject already claimed or the global/IP limits are exhausted
- **THEN** no issuance occurs and the wallet explains the eligibility or retry condition

#### Scenario: CRED-2 boundary

- **WHEN** reserved grant credits expire before refund or payment
- **THEN** payment becomes non-expiring earned credits and expired refunds go to the expiry sink, never negative balance

### Requirement: CRED-UX Usable and accessible journey

The system SHALL expose /wallet, /wallet/history with authorized state and primary actions, loading/empty/validation/error/permission/success/recovery feedback, responsive layouts and keyboard/screen-reader support as defined in design.md and docs/architecture.md.

#### Scenario: CRED-UX complete journey

- **WHEN** an authorized actor completes the primary journey in a supported browser
- **THEN** the current server state, next action and committed result are visible and announced without reloading away draft input

#### Scenario: CRED-UX recover from failure

- **WHEN** a request fails validation, loses permission or encounters an unavailable dependency
- **THEN** the route shows an actionable accessible explanation, preserves safe input where applicable and does not announce success or reveal private data

#### Scenario: CRED-UX responsive keyboard operation

- **WHEN** the journey is exercised at 360, 768 and 1440 pixels in both themes using only the keyboard
- **THEN** controls remain reachable, focus is visible and restored, text and controls meet contrast requirements and no page-level horizontal scrolling occurs
