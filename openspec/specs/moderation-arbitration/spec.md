# moderation-arbitration Specification

## Purpose
The moderation package keeps the marketplace accountable: a report that a user can raise against a task, a submission or a finding, a dispute that can suspend the escrow of a seat, and an independent moderator who is neither the publisher, the contributor, a project member nor the reporter and who can award, withhold or grant a funded bonus, close a case with a reason, reopen it on appeal and compensate the paid amount without rewriting the settled ledger.
## Requirements
### Requirement: MOD-1 Arbitration

The system SHALL expose reasoned role-restricted dispute resolution that preserves escrow until a conflict-free moderator commits a funded Credits outcome.

#### Scenario: MOD-1 success

- **WHEN** an independent moderator resolves an eligible dispute with a reason
- **THEN** the parties see the decision and matching immutable ledger result

#### Scenario: MOD-1 rejection

- **WHEN** a moderator with project/account conflict or ordinary user attempts resolution
- **THEN** the operation returns 403 and escrow remains unchanged

#### Scenario: MOD-1 boundary

- **WHEN** an appeal is made within seven days for the first time
- **THEN** a different moderator receives the reopened case and prior audit/ledger rows remain immutable

### Requirement: MOD-2 Safety

The system SHALL support private abuse reporting, evidence-limited moderation, auditable bans and safe content restriction without leaking report identities or editing balances.

#### Scenario: MOD-2 success

- **WHEN** a user reports visible abusive content
- **THEN** the reporter gets a receipt and only authorized moderation staff can inspect the report

#### Scenario: MOD-2 rejection

- **WHEN** a banned account attempts a new command with an old session
- **THEN** the command is denied before any domain or monetary effect

#### Scenario: MOD-2 boundary

- **WHEN** a report references content that becomes private
- **THEN** moderator access is justified by the case while unrelated users and notification previews reveal nothing

### Requirement: MOD-UX Usable and accessible journey

The system SHALL expose /reports/new, /disputes/{id}, /admin/reports, /admin/disputes with authorized state and primary actions, loading/empty/validation/error/permission/success/recovery feedback, responsive layouts and keyboard/screen-reader support as defined in design.md and docs/architecture.md.

#### Scenario: MOD-UX complete journey

- **WHEN** an authorized actor completes the primary journey in a supported browser
- **THEN** the current server state, next action and committed result are visible and announced without reloading away draft input

#### Scenario: MOD-UX recover from failure

- **WHEN** a request fails validation, loses permission or encounters an unavailable dependency
- **THEN** the route shows an actionable accessible explanation, preserves safe input where applicable and does not announce success or reveal private data

#### Scenario: MOD-UX responsive keyboard operation

- **WHEN** the journey is exercised at 360, 768 and 1440 pixels in both themes using only the keyboard
- **THEN** controls remain reachable, focus is visible and restored, text and controls meet contrast requirements and no page-level horizontal scrolling occurs

