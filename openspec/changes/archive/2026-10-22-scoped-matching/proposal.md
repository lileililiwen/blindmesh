# Proposal: Let a project find the contributors who fit its tasks

## Why

The matching scope of the marketplace is a promise without a mechanism, and the PRD asks for a search the product does not have.

The PRD states three levels of visibility for a project and for a task: public, visible only to the matched, and private. The product accepts the level "matched" and then treats it as "private" for a project and as "public" for a task:

- A project with the visibility `matched` is invisible to every candidate. The only accounts that may read it are its owner and its members, so a matching candidate can never find it and the owner never learns why.
- A task with the visibility `matched` inside a public project is visible to every visitor, so the level promises a restriction that the product does not enforce.

The PRD also asks for the active search of §5.2: "the system must also provide an active search, instead of all tasks depending on an algorithmic push. That includes a project finding people, a task finding people, and a user finding the tasks that suit them." The user-facing half exists: a contributor finds the tasks that fit them through the hard conditions of the task. The owner-facing half does not exist: an owner cannot ask which contributors fit a task, so the project never finds people.

The PRD names the bias of §5.3 in the same breath: "if the system always recommends the person with the highest reputation, a newcomer never receives a task." The product has the reputation of a contributor now, so the rule can be written, but no rule exists.

Status: proposed, unimplemented; no runtime evidence exists.

## What Changes

Deliver the Matching outcome through the product-owned C# backend, persistence, Next.js interface and behavioral tests described in design.md. Do not implement adjacent packages.

## Package Boundary and Split Assessment

Single outcome: Let a project find the contributors who fit its tasks, and let the matched scope mean exactly that. Dependencies: capability-directory, task-publication, participation-execution, reputation-history. Included: the named routes, the scope rule, the candidate list, the exploration order and their acceptance evidence. Separate lifecycles remain in the package map; no dependency is reimplemented here. The evaluation of the hard conditions stays with the capability module, the conditions stay with the task module, the reputation stays with the reputation module; this package owns the match, the reach and the order. This is the smallest coherent journey whose service, data and interface can be accepted together.

## Sibling and Shared Architecture Reconnaissance

| Candidate | Evidence path/symbol in Forge | Reusable capability | Compatibility gap | Owner/release boundary | Decision |
|---|---|---|---|---|---|
| Governance | modules/workspace-governance/scripts/init_project.py | declaration and automated checks | development-only tooling | Forge governance module, workspace-governance@1 | adopt |
| .NET platform | modules/dotnet-platform-libs/eng/package-manifest.json; Platform.Core and Identity.Contracts | opt-in technical contracts | exact feed version and isolated restore must be proved in foundation | shared platform owner, versioned NuGet | adopt |
| UI tokens | modules/dotnet-platform-libs/ui/packages/design-tokens | shared theme primitives | Next.js binding and digest receipt required in foundation | shared UI owner, immutable artifact | adopt |
| Matching domain | requirements.md and docs/architecture.md | product state and authorization | product-specific lifecycle has no equivalent shared model | Blindmesh only | keep local |

Do not edit the shared owners or copy their runtime implementations. docs/architecture.md records dependency direction and package mapping.

## User Experience and Interface Impact

Human-facing: yes. Actors: the owner of a project, the candidate whose capabilities fit, and the visitor. Entry points: /tasks/{id}/candidates, the task list, the task page. Navigation follows docs/architecture.md. Prioritize the current state, scope, deadlines and primary action; hide unauthorized controls while enforcing server policies. Preserve input on recovery, announce saved outcomes only after commit, and include loading/empty/validation/error/unavailable/permission/success states. Keyboard, focus restoration, screen-reader semantics, both themes and mobile layouts are acceptance requirements.

## BFS Impact Map

- Product/domain: Matching; interfaces and persistence in design.md.
- Callers: listed Next.js routes, owning API endpoints and the scope rule of the task and the project lists.
- Dependencies: capability-directory, task-publication, participation-execution, reputation-history; no unarchived prerequisite may be implemented here.
- Risks: unauthorized nested reads, a scope that leaks a private project, a stale candidate list, duplicate requests, concurrent transitions and unavailable dependencies; architecture failure rules apply.
- Tests: MATCH unit/integration, generated client, browser/axe groups and boundary fixtures.
- Unaffected: cash payments, native mobile, AI execution, external issue synchronization and sibling product source.

## Capabilities

### New Capabilities

- `scoped-matching`: Let a project find the contributors who fit its tasks, and let the matched scope mean exactly that.

### Modified Capabilities

- `capability-directory`: the evaluation of the hard conditions of a task gains the evaluation of another account's declarations for the owner of the task, through a seam. The rules of the evaluation, the vocabulary of the kinds and the privacy of a declaration do not change.
- `task-publication`: the scope rule of the task list, of the task detail and of the eligibility gains the matched level. The task model, the budget, the escrow and the state machine do not change.
- `reputation-history`: the accepted contribution count of an account becomes readable by the matching module through a seam. The projection, the events and the privacy of the history do not change.

## Non-goals

No unrelated product package, deployment authorization, real customer data, microservices, credential collection, or fabricated evidence.

The soft weights of the PRD §5.2 stay unimplemented. The PRD calls them "initial hypotheses" that "must be calibrated with real performance data and must not be used directly as a quality guarantee", so this package computes no score, no weight, no percentage and no quality claim. The quota of the new users and the diversity control of §5.3 stay out of scope beyond the single exploration rule of MATCH-3. The push of a matched task inside 15 minutes belongs to the notification package and is not implemented here. The kinds of a hard condition stay the three that the capability module owns: region, device and role.
