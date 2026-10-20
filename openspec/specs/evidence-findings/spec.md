# evidence-findings Specification

## Purpose
The evidence and findings package owns the proof of the completed work: a structured submission with immutable revisions and a private, quarantined and validated attachment, a finding that carries its own classification, and a review that settles the accepted seat through the credit ledger — an explicit bonus within the cap, one correction within 48 hours, and an automatic acceptance of the base after 72 hours when no correction and no dispute is open.
## Requirements
### Requirement: EVID-1 Evidence

The system SHALL accept valid structured submissions and findings with private validated attachments, immutable submitted revisions and independent finding status.

#### Scenario: EVID-1 success

- **WHEN** a running participant submits complete scope evidence, including a valid clean exploration
- **THEN** base credits enter pending and reviewers can read only authorized scanned evidence

#### Scenario: EVID-1 rejection

- **WHEN** a file is oversized, mismatched, unsafe or read by another participant
- **THEN** the request is rejected or remains quarantined without a public URL or metadata leak

#### Scenario: EVID-1 boundary

- **WHEN** visibility or membership changes before a signed URL is requested
- **THEN** current authorization is rechecked and expired URLs cannot be renewed by a former reader

### Requirement: EVID-2 Review

The system SHALL settle accepted work through Credits, support one 48h correction request, and auto-accept base after 72h only when no correction or dispute is open.

#### Scenario: EVID-2 success

- **WHEN** the owner accepts valid completed work with a bonus within cap
- **THEN** pending/base and selected bonus become earned, unused bonus refunds, and UI displays the committed result

#### Scenario: EVID-2 rejection

- **WHEN** the owner rejects a finding to withhold completed base pay
- **THEN** the finding decision does not confiscate pay and completion disagreement opens arbitration

#### Scenario: EVID-2 boundary

- **WHEN** a timeout acceptance races a manual acceptance or dispute
- **THEN** one legal serialized result commits without duplicate payment or refund

### Requirement: EVID-UX Usable and accessible journey

The system SHALL expose /work/{id}/submit, /projects/{id}/reviews, /findings/{id} with authorized state and primary actions, loading/empty/validation/error/permission/success/recovery feedback, responsive layouts and keyboard/screen-reader support as defined in design.md and docs/architecture.md.

#### Scenario: EVID-UX complete journey

- **WHEN** an authorized actor completes the primary journey in a supported browser
- **THEN** the current server state, next action and committed result are visible and announced without reloading away draft input

#### Scenario: EVID-UX recover from failure

- **WHEN** a request fails validation, loses permission or encounters an unavailable dependency
- **THEN** the route shows an actionable accessible explanation, preserves safe input where applicable and does not announce success or reveal private data

#### Scenario: EVID-UX responsive keyboard operation

- **WHEN** the journey is exercised at 360, 768 and 1440 pixels in both themes using only the keyboard
- **THEN** controls remain reachable, focus is visible and restored, text and controls meet contrast requirements and no page-level horizontal scrolling occurs

