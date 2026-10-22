# Tasks: scoped-matching

## 1. BFS — Baseline and impact coverage

- [x] 1.1 Read the handoff, the package map and the archive of the four dependencies (capability-directory, task-publication, participation-execution, reputation-history); map every MATCH requirement and scenario to an actor, a route, a module, a contract and a failure fixture.
- [x] 1.2 Record the shared-architecture reconnaissance: the seam of the conditions of a task, the seam of the match, the seam of the candidate declarations and the seam of the accepted contributions. No sibling source dependency, no new NuGet package, no copied platform code.
- [x] 1.3 Fix the scope rule of the project and of the task as one rule and name every read that it touches: the task list, the task detail, the eligibility, the project detail and the project list.
- [x] 1.4 Map the privacy boundary: what the candidate list may show (the public profile, the public declaration, the value of the condition) and what it may never show (the private details of a declaration, the email, the address, a party of another case, the private details of a capability).
- [x] 1.5 Load the concern packs of the privacy, the persistence and the UI and record the failure policies: a missing reputation, a withdrawn declaration, a task that closed, a project that left the matched scope, an account that is not an insider.

## 2. DFS — Requirement-by-requirement implementation

- [x] 2.1 Domain: src/Blindmesh.Domain/Matching with the condition outcome, the candidate match and the order reason of the exploration rule.
- [x] 2.2 Seams: declare ITaskRequirementsReader in Application/Tasks, ITaskMatchSeam in Application/Tasks, ICandidateMatchReader in Application/Capabilities and IReputationStandingReader in Application/Reputation; implement each in the module that owns the data and register it in the composition root.
- [x] 2.3 Application: src/Blindmesh.Application/Matching with the service that answers the candidates of a task and the match of one account against the conditions of one task.
- [x] 2.4 MATCH-1 Reach: apply the rule of the scope to the task list, the task detail, the eligibility, the project detail and the project list, and make every read of a task use the same rule.
- [x] 2.5 MATCH-2 Candidates: add GET /v1/tasks/{id}/candidates for the members of the project of the task, with the condition that each declaration satisfied, and refuse every other account with 403 and no name and no condition.
- [x] 2.6 MATCH-3 Fairness: order the list with the exploration rule, name the reason of the order and carry no score, no weight, no percentage and no rank.
- [x] 2.7 Infrastructure: the store of the matching module and the registration of the module; no table of its own and no migration.
- [x] 2.8 Web: apps/web/src/lib/matching.ts, the components of the candidate list, the route /tasks/{id}/candidates, the link of the owner on the task page, the regenerated API client and the hand-written methods of the client.
- [x] 2.9 The states of the page: the skeleton while loading, the empty state that names the conditions, the error with a retry, the permission error, the unread of a private project and the dark theme, at 360, 768 and 1440 pixels.
- [x] 2.10 The xUnit tests of the three requirements and the browser journeys of the package, with the titles that start with MATCH-UX.

## 3. BFS — Cross-surface regression and completeness

- [x] 3.1 Run the whole .NET chain, the whole web chain and the planning checks; every command must pass.
- [x] 3.2 Run the browser journeys of every package of the delivery order on its own fresh database, because the scope rule touches the task list of every package.
- [x] 3.3 Check that the ledger of every scenario still balances, that no balance is negative and that no transaction of a settled seat was rewritten.
- [x] 3.4 Check the visibility of every read that the scope rule touches: a task that the list does not show is not readable through the detail, through the eligibility, through a project page or through the API of the client.
- [x] 3.5 Check the redaction of the candidate list with a private and a withdrawn declaration, with a private account and with an account that is not a member of the project.
- [x] 3.6 Update docs/architecture.md, the coverage of the requirements, the package map, the change map, the README and the handoff with the rule of the scope and with the honest limits of the package.

## 4. Verification

- [x] 4.1 Record the real output of every command in openspec/changes/scoped-matching/verification.md, with the counts of the tests, of the journeys and of the runs of axe, and with the checks that did not run.
- [x] 4.2 Fill openspec/changes/scoped-matching/traceability.md with the requirement, the scenario, the task and the test of every row.
- [x] 4.3 Name the limits of the package: the soft weights are not implemented, the push of a matched task is not implemented, the kinds of a condition stay the three of the capability module, and only Chromium is proven.
