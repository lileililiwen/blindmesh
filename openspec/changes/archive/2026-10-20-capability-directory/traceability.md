# Requirement traceability

Outcome: Declare capabilities and evaluate hard matching conditions. Every row is implemented and verified; the scenario names are normative and the tests use their requirement prefix. No scenario is waived by API-only evidence.

| Requirement | Outcome / UX | Design decision and implementation boundary | Scenarios | Tasks | Verification oracle |
|---|---|---|---|---|---|
| CAP-1 | Declaration; /capabilities, /settings/capabilities, /tasks/{id}/eligibility | design.md; src/Blindmesh.Domain/Capabilities, src/Blindmesh.Application/Capabilities, src/Blindmesh.Infrastructure/Capabilities, src/Blindmesh.Api/Endpoints/Capabilities; apps/web/src/app/capabilities, apps/web/src/app/settings/capabilities, apps/web/src/app/tasks/[id]/eligibility | CAP-1 success; rejection; boundary | 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2 | verification.md; `Cap1DeclarationTests` (6 tests) + the browser journeys of `tests/e2e/capability-directory/journey.spec.ts` |
| CAP-2 | Eligibility; /capabilities, /settings/capabilities, /tasks/{id}/eligibility | design.md; src/Blindmesh.Domain/Capabilities/CapabilityHardCondition.cs, src/Blindmesh.Application/Capabilities/CapabilityService.cs, src/Blindmesh.Application/Capabilities/ITaskConditionReader.cs, src/Blindmesh.Infrastructure/Capabilities, src/Blindmesh.Api/Endpoints/Capabilities; apps/web/src/components/capability-eligibility.tsx | CAP-2 success; rejection; boundary | 1.1, 1.2, 2.2, 3.1, 3.2, 4.1, 4.2 | verification.md; `Cap2EligibilityTests` (6 tests) + the browser journeys of `tests/e2e/capability-directory/journey.spec.ts` |
| CAP-UX | Accessible journey; /capabilities, /settings/capabilities, /tasks/{id}/eligibility | design.md; apps/web/src/components/capability-list.tsx, apps/web/src/components/capability-settings.tsx, apps/web/src/components/capability-eligibility.tsx, apps/web/src/lib/capabilities.ts | CAP-UX complete journey; recover from failure; responsive keyboard operation | 1.1, 1.2, 2.3, 3.1, 3.2, 4.1, 4.2 | verification.md; 12 browser journeys and 5 axe runs in `tests/e2e/capability-directory` |

## Scenario to test

| Scenario | Test |
|---|---|
| CAP-1 success | `Cap1DeclarationTests.CAP1SuccessAContributorDeclaresADeviceCapabilityAndTheDirectoryHoldsIt`; `journey.spec.ts`: the contributor declares a capability and finds it in the directory |
| CAP-1 rejection | `Cap1DeclarationTests.CAP1RejectionACredentialInADeclarationIsRefusedWithoutAStoreAndWithoutAnEcho`; `Cap1DeclarationTests.CAP1RejectionAContributorMayNotReadOrChangeAPrivateDeclarationOfAnotherContributor`; `Cap1DeclarationTests.CAP1RejectionAContributorWithoutASessionMayNotDeclareACapability`; `journey.spec.ts`: a private declaration of another contributor stays invisible |
| CAP-1 boundary | `Cap1DeclarationTests.CAP1BoundaryAWithdrawnDeclarationKeepsItsRowAndItsHistoryAndLeavesTheMatch`; `Cap1DeclarationTests.CAP1BoundaryAStaleVersionIsRefusedAndTheDeclarationKeepsItsValue`; `journey.spec.ts`: the contributor withdraws a declaration and it keeps its history |
| CAP-2 success | `Cap2EligibilityTests.CAP2SuccessAnEligibleContributorMeetsEveryHardCondition`; `journey.spec.ts`: the eligibility check names the satisfied and the unmet condition |
| CAP-2 rejection | `Cap2EligibilityTests.CAP2RejectionAContributorWithoutTheRequiredRegionGetsAnActionableReason`; `Cap2EligibilityTests.CAP2RejectionAConditionWithAnUnknownKindIsRefused`; `Cap2EligibilityTests.CAP2RejectionAContributorWithoutASessionMayNotEvaluateAnEligibility` |
| CAP-2 boundary | `Cap2EligibilityTests.CAP2BoundaryTheEvaluationUsesTheCommittedStateAndNotACachedPreview`; `Cap2EligibilityTests.CAP2BoundaryTheEvaluationOfATaskIsNotFoundBecauseNoTaskIsPublished` |
| CAP-UX complete journey | `journey.spec.ts`: the contributor declares a capability and finds it in the directory, the contributor withdraws a declaration and it keeps its history, the eligibility route shows the honest state of a task that is not published, the eligibility check names the satisfied and the unmet condition, the own list of a contributor without a declaration explains the next action |
| CAP-UX recover from failure | `journey.spec.ts`: an invalid declaration is marked and takes the focus, the settings route offers the sign in to a visitor without a session, the failure state explains the failure and offers a retry |
| CAP-UX responsive keyboard operation | `journey.spec.ts`: the journey has no horizontal overflow at 360, 768 and 1440 pixels, the journey works with the keyboard only, the journey works in the dark theme; `accessibility.a11y.ts` (5 runs) |
