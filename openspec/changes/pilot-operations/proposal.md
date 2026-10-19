# Proposal: Operate a verified pilot with recovery and measured gates

## Why

Operate a verified pilot with recovery and measured gates is a separately verifiable part of the PRD mutual-testing journey. Status: proposed, unimplemented; no runtime evidence exists.

## What Changes

Deliver the Operations outcome through the product-owned C# backend, persistence, Next.js interface and behavioral tests described in design.md. Do not implement adjacent packages.

## Package Boundary and Split Assessment

Single outcome: Operate a verified pilot with recovery and measured gates. Dependencies: reputation-history, notification-delivery. Included: the named routes, domain transitions and their acceptance evidence. Separate lifecycles remain in the package map; no dependency is reimplemented here. This is the smallest coherent journey whose service, data and interface can be accepted together.

## Sibling and Shared Architecture Reconnaissance

| Candidate | Evidence path/symbol in Forge | Reusable capability | Compatibility gap | Owner/release boundary | Decision |
|---|---|---|---|---|---|
| Governance | modules/workspace-governance/scripts/init_project.py | declaration and automated checks | development-only tooling | Forge governance module, workspace-governance@1 | adopt |
| .NET platform | modules/dotnet-platform-libs/eng/package-manifest.json; Platform.Core and Identity.Contracts | opt-in technical contracts | exact feed version and isolated restore must be proved in foundation | shared platform owner, versioned NuGet | adopt |
| UI tokens | modules/dotnet-platform-libs/ui/packages/design-tokens | shared theme primitives | Next.js binding and digest receipt required in foundation | shared UI owner, immutable artifact | adopt |
| Operations domain | requirements.md and docs/architecture.md | product state and authorization | product-specific lifecycle has no equivalent shared model | Blindmesh only | keep local |

Do not edit the shared owners or copy their runtime implementations. docs/architecture.md records dependency direction and package mapping.

## User Experience and Interface Impact

Human-facing: yes. Actors: administrator and pilot participant. Entry points: /admin/operations, /status. Navigation follows docs/architecture.md. Prioritize the current state, scope, deadlines and primary action; hide unauthorized controls while enforcing server policies. Preserve input on recovery, announce saved outcomes only after commit, and include loading/empty/validation/error/unavailable/permission/success states. Keyboard, focus restoration, screen-reader semantics, both themes and mobile layouts are acceptance requirements.

## BFS Impact Map

- Product/domain: Operations; interfaces and persistence in design.md.
- Callers: listed Next.js routes, owning API endpoints and scoped worker handlers where applicable.
- Dependencies: reputation-history, notification-delivery; no unarchived prerequisite may be implemented here.
- Risks: unauthorized nested reads, stale versions, duplicate requests, concurrent transitions and unavailable dependencies; architecture failure rules apply.
- Tests: OPS unit/integration, generated client, browser/axe groups and boundary fixtures.
- Unaffected: cash payments, native mobile, AI execution, external issue synchronization and sibling product source.

## Capabilities

### New Capabilities

- `pilot-operations`: Operate a verified pilot with recovery and measured gates.

### Modified Capabilities

None; earlier owners retain their contracts.

## Non-goals

No unrelated product package, deployment authorization, real customer data, microservices, credential collection, or fabricated evidence.
