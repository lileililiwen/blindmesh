# Requirement traceability

Outcome: Explain non-purchasable reputation from accepted outcomes. Every scenario of the specification names the test that proves it. A scenario is proven by a test of the backend, by a journey of the browser, or by both. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| REP-1 | Provenance; /people/{username}/history, /settings/reputation | design.md; src/Blindmesh.Domain/Reputation, src/Blindmesh.Application/Reputation, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Reputation; apps/web | REP-1 success; rejection; boundary | 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2 | verification.md; REP xUnit/integration + browser/axe assertions |
| REP-2 | Privacy; /people/{username}/history, /settings/reputation | design.md; src/Blindmesh.Domain/Reputation, src/Blindmesh.Application/Reputation, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Reputation; apps/web | REP-2 success; rejection; boundary | 1.1, 1.2, 2.2, 3.1, 3.2, 4.1, 4.2 | verification.md; REP xUnit/integration + browser/axe assertions |
| REP-UX | Accessible journey; /people/{username}/history, /settings/reputation | design.md; src/Blindmesh.Domain/Reputation, src/Blindmesh.Application/Reputation, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Reputation; apps/web | REP-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 1.2, 2.3, 3.1, 3.2, 4.1, 4.2 | verification.md; REP xUnit/integration + browser/axe assertions |

## The tests of every scenario

| Scenario of the specification | Test of the backend | Test of the browser |
|---|---|---|
| REP-1 success | `tests/Blindmesh.Tests/Reputation/REP1ProvenanceTests.cs` → `REP1SuccessTheAcceptedContributionIsCountedOnceAndTheProfileExplainsItsCategory` | `tests/e2e/reputation-history/journey.spec.ts` → `REP-UX the accepted contribution of a seat appears in the history of the contributor` |
| REP-1 rejection | `tests/Blindmesh.Tests/Reputation/REP1ProvenanceTests.cs` → `REP1RejectionTheSameEventDeliveredTwiceChangesNothing` | `tests/e2e/reputation-history/journey.spec.ts` → `REP-UX the accepted contribution of a seat appears in the history of the contributor` (the history of the contributor holds one line for the contribution, not two) |
| REP-1 boundary | `tests/Blindmesh.Tests/Reputation/REP1ProvenanceTests.cs` → `REP1BoundaryTheReversalCorrectsTheProjectionAndKeepsTheHistory` | — (the reversal has no page of its own yet; the command of the arbitration writes the event of the outbox) |
| REP-2 success | `tests/Blindmesh.Tests/Reputation/REP2PrivacyTests.cs` → `REP2SuccessThePublicHistoryShowsOnlyThePermittedReferences` | `tests/e2e/reputation-history/journey.spec.ts` → `REP-UX the accepted contribution of a seat appears in the history of the contributor` (the public history of the contributor names the source of the contribution and its category) |
| REP-2 rejection | `tests/Blindmesh.Tests/Reputation/REP2PrivacyTests.cs` → `REP2RejectionThePublicRequestRevealsNoPrivateField` | `tests/e2e/reputation-history/journey.spec.ts` → `REP-UX a source that becomes private disappears from the public history` (the public history names no private source) |
| REP-2 boundary | `tests/Blindmesh.Tests/Reputation/REP2PrivacyTests.cs` → `REP2BoundaryASourceThatBecamePrivateDisappearsFromThePublicHistory` | `tests/e2e/reputation-history/journey.spec.ts` → `REP-UX a source that becomes private disappears from the public history` |
| REP-UX complete journey | the six tests of the backend named above | `tests/e2e/reputation-history/journey.spec.ts` → `REP-UX the accepted contribution of a seat appears in the history of the contributor`, `REP-UX a source that becomes private disappears from the public history` |
| REP-UX recover from failure | `REP2RejectionThePublicRequestRevealsNoPrivateField` (the export of the own history is refused) | `tests/e2e/reputation-history/journey.spec.ts` → `REP-UX the history of a contributor without a contribution explains the next action`, `REP-UX an unavailable API explains the failure and offers a retry` |
| REP-UX responsive keyboard operation | — | `tests/e2e/reputation-history/journey.spec.ts` → `REP-UX the pages of the reputation have no horizontal overflow at 360, 768 and 1440 pixels`, `REP-UX the journey of the reputation works with the keyboard only`, `REP-UX the journey of the reputation works in the dark theme`; `tests/e2e/reputation-history/accessibility.a11y.ts` → the five runs of axe |

## The routes and their evidence

| Route | Backend | Browser |
|---|---|---|
| `GET /v1/people/{username}/reputation` | `REP1SuccessTheAcceptedContributionIsCountedOnceAndTheProfileExplainsItsCategory` | the summary of the contributor on `/people/{username}/history` |
| `GET /v1/people/{username}/reputation/history` | `REP1SuccessTheAcceptedContributionIsCountedOnceAndTheProfileExplainsItsCategory`, `REP2SuccessThePublicHistoryShowsOnlyThePermittedReferences`, `REP2RejectionThePublicRequestRevealsNoPrivateField`, `REP2BoundaryASourceThatBecamePrivateDisappearsFromThePublicHistory` | `REP-UX the accepted contribution of a seat appears in the history of the contributor`, `REP-UX a source that becomes private disappears from the public history`, `REP-UX the history of a contributor without a contribution explains the next action` |
| `GET /v1/reputation/history` | `REP2RejectionThePublicRequestRevealsNoPrivateField` (the export of a visitor is refused), `REP2BoundaryASourceThatBecamePrivateDisappearsFromThePublicHistory` (the own history keeps the line of the contributor) | `REP-UX the accepted contribution of a seat appears in the history of the contributor` (the own history on `/settings/reputation`) |

## The limits of the evidence

- The package owns no command of its own. The contribution of a seat and its reversal are written by the findings and the moderation packages inside the transaction of their own command, and the worker of the product applies them. The tests of the backend register the real handlers and the real claim cycle of the worker, so the effect of an event is produced by the code that runs in production.
- The API exposes no route for one single line of the reputation. A visitor that asks for a private source event reads the public history of the contributor, and the answer holds neither the line of the private source nor a field of it.
- A contributor cannot earn credits in a private project, because a member of a project never earns from it. A source that is private for a visitor therefore always was public when the contribution was written, and the boundary of the package is the moment at which the owner closes the project.
- The browser journeys ran with the Chromium browser only, because the Firefox and the WebKit runtimes are not installed on this machine.
