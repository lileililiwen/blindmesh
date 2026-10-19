## ADDED Requirements

### Requirement: REP-1 Provenance

The system SHALL derive explainable non-purchasable reputation from final accepted outcomes with duplicate-event protection and traceable reversal events.

#### Scenario: REP-1 success

- **WHEN** a participation is finally accepted
- **THEN** the contribution count increases once and the profile explains its source category

#### Scenario: REP-1 rejection

- **WHEN** the same event is delivered repeatedly
- **THEN** the displayed count and stored provenance remain unchanged

#### Scenario: REP-1 boundary

- **WHEN** arbitration reverses a previously accepted contribution
- **THEN** a compensating event corrects the projection without deleting historical evidence

### Requirement: REP-2 Privacy

The system SHALL filter reputation history by current visibility and never reveal private project titles, findings, parties or attachment links to public viewers.

#### Scenario: REP-2 success

- **WHEN** a visitor opens a profile history
- **THEN** only permitted public references are shown alongside safe aggregate counts

#### Scenario: REP-2 rejection

- **WHEN** the visitor requests a private source event or export
- **THEN** the response does not reveal sensitive source fields

#### Scenario: REP-2 boundary

- **WHEN** a formerly public source becomes private
- **THEN** subsequent history reads and caches remove its identifying details

### Requirement: REP-UX Usable and accessible journey

The system SHALL expose /people/{username}/history, /settings/reputation with authorized state and primary actions, loading/empty/validation/error/permission/success/recovery feedback, responsive layouts and keyboard/screen-reader support as defined in design.md and docs/architecture.md.

#### Scenario: REP-UX complete journey

- **WHEN** an authorized actor completes the primary journey in a supported browser
- **THEN** the current server state, next action and committed result are visible and announced without reloading away draft input

#### Scenario: REP-UX recover from failure

- **WHEN** a request fails validation, loses permission or encounters an unavailable dependency
- **THEN** the route shows an actionable accessible explanation, preserves safe input where applicable and does not announce success or reveal private data

#### Scenario: REP-UX responsive keyboard operation

- **WHEN** the journey is exercised at 360, 768 and 1440 pixels in both themes using only the keyboard
- **THEN** controls remain reachable, focus is visible and restored, text and controls meet contrast requirements and no page-level horizontal scrolling occurs
