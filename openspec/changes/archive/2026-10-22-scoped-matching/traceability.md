# Requirement traceability

Outcome: Let a project find the contributors who fit its tasks, and let the matched scope mean exactly that. Every scenario names the test that proves it. A scenario is proven by a test of the backend, by a journey of the browser, or by both. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| MATCH-1 | Reach; the task list, the task page, the eligibility | design.md; src/Blindmesh.Domain/Matching, src/Blindmesh.Application/Matching, the scope rule of the task module and of the project module, src/Blindmesh.Api/Endpoints/Matching; apps/web | MATCH-1 success; rejection; boundary | 1.1, 1.2, 1.3, 2.1, 2.2, 2.3, 2.4, 2.8, 2.9, 2.10, 3.4 | verification.md; MATCH xUnit/integration + browser/axe assertions |
| MATCH-2 | Candidates; /tasks/{id}/candidates | design.md; src/Blindmesh.Domain/Matching, src/Blindmesh.Application/Matching, src/Blindmesh.Infrastructure, src/Blindmesh.Api/Endpoints/Matching; apps/web | MATCH-2 success; rejection; boundary | 1.1, 1.4, 1.5, 2.1, 2.2, 2.3, 2.5, 2.7, 2.8, 2.9, 2.10, 3.5 | verification.md; MATCH xUnit/integration + browser/axe assertions |
| MATCH-3 | Fairness; the order of the candidate list | design.md; src/Blindmesh.Domain/Matching, src/Blindmesh.Application/Matching, the seam of the reputation; apps/web | MATCH-3 success; rejection; boundary | 1.1, 1.5, 2.1, 2.2, 2.3, 2.6, 2.8, 2.10 | verification.md; MATCH xUnit/integration + browser/axe assertions |
| MATCH-UX | Accessible journey; /tasks/{id}/candidates | design.md; apps/web | MATCH-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 2.8, 2.9, 2.10, 3.2 | verification.md; MATCH xUnit/integration + browser/axe assertions |

## The tests of every scenario

| Scenario of the specification | Test of the backend | Test of the browser |
|---|---|---|
| MATCH-1 success | `tests/Blindmesh.Tests/Matching/MATCH1ReachTests.cs` → `MATCH1SuccessTheCandidateThatFitsTheConditionsSeesTheTaskOfTheMatchedProject` | `tests/e2e/scoped-matching/journey.spec.ts` → `MATCH-UX the task of a matched project reaches the candidate that fits it` |
| MATCH-1 rejection | `tests/Blindmesh.Tests/Matching/MATCH1ReachTests.cs` → `MATCH1RejectionTheCandidateThatDoesNotFitNeverSeesTheTask` | `tests/e2e/scoped-matching/journey.spec.ts` → `MATCH-UX the task of a matched project stays away from the candidate that does not fit it` |
| MATCH-1 boundary | `tests/Blindmesh.Tests/Matching/MATCH1ReachTests.cs` → `MATCH1BoundaryTheReachEndsWithTheScopeAndKeepsTheTakenSeat` | `tests/e2e/scoped-matching/journey.spec.ts` → `MATCH-UX the matched project that closes its scope takes the task away` |
| MATCH-2 success | `tests/Blindmesh.Tests/Matching/MATCH2CandidateTests.cs` → `MATCH2SuccessTheOwnerReadsTheCandidatesWithTheConditionThatEachDeclarationSatisfied` | `tests/e2e/scoped-matching/journey.spec.ts` → `MATCH-UX the owner of a project reads the candidates of its task` |
| MATCH-2 rejection | `tests/Blindmesh.Tests/Matching/MATCH2CandidateTests.cs` → `MATCH2RejectionTheAccountOutsideTheProjectLearnsNothing` | `tests/e2e/scoped-matching/journey.spec.ts` → `MATCH-UX the account outside the project is refused the candidates` |
| MATCH-2 boundary | `tests/Blindmesh.Tests/Matching/MATCH2CandidateTests.cs` → `MATCH2BoundaryTheWithdrawnOrPrivateDeclarationLeavesTheListAndNothingPrivateIsShown` | `tests/e2e/scoped-matching/journey.spec.ts` → `MATCH-UX the withdrawn declaration leaves the list of the candidates` |
| MATCH-3 success | `tests/Blindmesh.Tests/Matching/MATCH3FairnessTests.cs` → `MATCH3SuccessTheAccountWithoutAnAcceptedContributionComesFirstAndTheReasonIsNamed` | `tests/e2e/scoped-matching/journey.spec.ts` → `MATCH-UX the account without an accepted contribution comes first in the list` |
| MATCH-3 rejection | `tests/Blindmesh.Tests/Matching/MATCH3FairnessTests.cs` → `MATCH3RejectionTheAccountThatDoesNotFitIsNeverListed` | `tests/e2e/scoped-matching/journey.spec.ts` → `MATCH-UX the account that does not fit is never in the list` |
| MATCH-3 boundary | `tests/Blindmesh.Tests/Matching/MATCH3FairnessTests.cs` → `MATCH3BoundaryTheAnswerCarriesNoScoreAndTheOrderIsStable` | `tests/e2e/scoped-matching/journey.spec.ts` → `MATCH-UX the list of the candidates carries no score and no rank` |
| MATCH-UX complete journey | the tests of the backend named above | `tests/e2e/scoped-matching/journey.spec.ts` → `MATCH-UX the owner of a project reads the candidates of its task` |
| MATCH-UX recover from failure | `MATCH2RejectionTheAccountOutsideTheProjectLearnsNothing` (the refusal) | `tests/e2e/scoped-matching/journey.spec.ts` → `MATCH-UX no candidate fits the conditions today and the page says so`, `MATCH-UX an unavailable API explains the failure and offers a retry` |
| MATCH-UX responsive keyboard operation | — | `tests/e2e/scoped-matching/journey.spec.ts` → `MATCH-UX the pages of the matching have no horizontal overflow at 360, 768 and 1440 pixels`, `MATCH-UX the journey of the matching works with the keyboard only`, `MATCH-UX the journey of the matching works in the dark theme`; `tests/e2e/scoped-matching/accessibility.a11y.ts` → the runs of axe |

## The routes and their evidence

| Route | Backend | Browser |
|---|---|---|
| `GET /v1/tasks/{id}/candidates` | `MATCH2SuccessTheOwnerReadsTheCandidatesWithTheConditionThatEachDeclarationSatisfied`, `MATCH2RejectionTheAccountOutsideTheProjectLearnsNothing`, `MATCH2BoundaryTheWithdrawnOrPrivateDeclarationLeavesTheListAndNothingPrivateIsShown`, `MATCH3SuccessTheAccountWithoutAnAcceptedContributionComesFirstAndTheReasonIsNamed`, `MATCH3RejectionTheAccountThatDoesNotFitIsNeverListed`, `MATCH3BoundaryTheAnswerCarriesNoScoreAndTheOrderIsStable` | `MATCH-UX the owner of a project reads the candidates of its task`, `MATCH-UX the account outside the project is refused the candidates`, `MATCH-UX no candidate fits the conditions today and the page says so` |
| `GET /v1/tasks` and `GET /v1/tasks/{id}` and `GET /v1/tasks/{id}/eligibility` | `MATCH1SuccessTheCandidateThatFitsTheConditionsSeesTheTaskOfTheMatchedProject`, `MATCH1RejectionTheCandidateThatDoesNotFitNeverSeesTheTask`, `MATCH1BoundaryTheReachEndsWithTheScopeAndKeepsTheTakenSeat` | `MATCH-UX the task of a matched project reaches the candidate that fits it`, `MATCH-UX the task of a matched project stays away from the candidate that does not fit it` |

## The limits of the evidence

- The rule of the scope is applied by one method of the matching module, `ITaskMatchSeam.MatchesAsync`. The list of the tasks, the detail of a task, the eligibility, the seat of a task, the detail of a project and the list of the projects ask that method, so a task that the list of the marketplace does not show is never readable through another route.
- The package owns no row and no table. The candidate list is computed from the declarations of the capability module, from the conditions of the task module and from the accepted contributions of the reputation module, so the list changes when one of them changes and it holds no stale copy of its own.
- The evaluation of the conditions belongs to the capability module and the conditions belong to the task module. The four seams keep the modules apart and no seam creates a cycle at runtime: the matching module reads the conditions of a task through `ITaskRequirementsReader` and never through the reader that applies the scope, and it reads the accounts through `ICandidateMatchReader` and through `IReputationStandingReader`, which are readers of their own modules and not their services.
- The order of the list is a rule of order and not a quality claim. The answer carries no score, no weight, no percentage and no rank, and the reason of the place of an account is named in words.
- The soft weights of the PRD §5.2 are not implemented, because the PRD calls them hypotheses that must be calibrated with real data and must not be used as a quality guarantee.
- The push of a matched task is not implemented. The account that the list names is not told that it was listed; a push belongs to the notification package.
- The kinds of a hard condition stay the three of the capability module: region, device and role. The package owns no new vocabulary of a condition.
- An account whose public profile is closed is not named in the list, because the list shows the public profile of the candidate. A task without a hard condition has no match, so the matched scope never opens for it.
- Only Chromium is proven. The Firefox and the WebKit runtimes are not installed on this machine, so the journeys of the package and the runs of axe ran with the Chromium browser only.
