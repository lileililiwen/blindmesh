current_spec: runtime-foundation

# Blindmesh handoff

Date: 2026-10-19. Scope: initial project governance/documentation bootstrap from the preserved PRD. No feature implementation or archival is authorized by proposal authoring.

## Current state and next action

Backend selected by the user: C# 14/.NET SDK 10.0.400, ASP.NET Core 10, EF Core/Npgsql 10, PostgreSQL 17. Frontend: Next.js 16/React 19/TypeScript 5, Node 24/npm. See docs/architecture.md.

Twelve proposed changes contain 37 requirements, 111 scenarios and 109 unchecked implementation tasks. No .csproj, solution, application package.json, product tests, database migrations or running service exists. The only apps asset is the pinned shared UI token file. No change is archived. runtime-foundation is the first dependency-eligible change; openspec/package-map.md and openspec/change-map.json agree.

The sole first-line current_spec is the machine-readable selected active package. Advance it after verified implementation/archive; remove it when the active queue becomes empty.

MIT license is preserved. Source PRD SHA256 remains 718d1f7bb8884c803fb7aa6b8b8fc88c5afba6b9688884dad187f6eb3d7ec91b. Git is on main; staging succeeded in this session, so an older read-only Git limitation is no longer current.

## Verification evidence

- python3 scripts/verify_bootstrap.py: PASS; 12 changes, 37 requirements, 111 scenarios, 109 unchecked tasks; shared token SHA256 and 12 contrast pairs verified.
- node scripts/check-openspec-change-names.mjs: PASS.
- openspec validate --all --strict --no-interactive: 12 passed, 0 failed.
- .project.json validates against the Forge governance JSON Schema. Stack/profile are dotnet/dotnet-product; lifecycle is planning; deployable is false.
- python3 ../forge/modules/workspace-governance/scripts/ui_conformance.py . --json: PASS, no findings. Only a vendored token asset exists; zero product UI files are in scope. This is not browser accessibility evidence.
- Forge import with --profile aspnet-web --accept succeeded in the local default registry. Current declared maturity is L0, target L3; the import preview's inferred L1 is not observed product maturity.
- .github/workflows/planning.yaml defines pinned OpenSpec planning checks. Hosted execution has not run.
- The final local Forge Gate after path reconciliation is BLOCKED: native build/tests/security are unbound and governance-quality fails on twelve check-debt findings. Planning verification, change names, repository, source-size and UI-conformance checks pass. Evidence is stored in ignored .forge/gate/blindmesh/evidence.json and the temporary verification registry; it is not release approval.
- GitHub authentication succeeded outside the sandbox; network restrictions caused the initial sandbox failure. No credentials were read or copied.

## Blockers and remaining boundaries

The local governance audit reports unregistered central workspace inventory, unverified publication and required build/security/test command debt. It also reports unrelated discovered sibling inventory. Local declaration/schema and Forge registration do not prove the central governance registry is updated. Do not modify another project's registry or suppress its findings as part of product code.

Native build/test/security checks require runtime-foundation's actual manifests and commands. Do not bind documentation checks as substitutes for product build/tests or turn off required checks to manufacture a passing Gate. A successful planning check is not DONE.

GitHub identity is proposed as lileililiwen/blindmesh. Private is provisional local discoverability metadata pending the user's visibility answer. No origin, push or GitHub metadata write has been made. Publication and remote metadata verification remain blocked on the visibility decision; follow the authenticated bootstrap flow after that answer.

Browser flows, responsive behavior, rendered accessibility, production readiness and documentation screenshots are unavailable because no application exists. docs/assets/capture-plan.md names the real capture commands for the foundation milestone. Token measurements are not rendered UI evidence.

Exact tool setup: use Node 24's global bin and the built Forge binary on PATH; OpenSpec 1.6.0 and .NET SDK 10.0.400 were verified. Keep machine-specific paths out of tracked runtime/setup configuration. See docs/bootstrap-evidence.md.

## Workflow

Work directly on main. Before selecting a package, run the change-name preflight; implement only one dependency-eligible package using BFS → DFS → BFS and all four task phases. Assess UI/UX in every change; APIs do not replace interfaces. Human-facing completion requires usable UI, browser journeys, responsive checks and accessibility evidence.

Each completed implementation change requires exactly two commits: implementation/tests/archive/generated specs, then HANDOFF.md only. Do not ask for conversational authorization or confirmation before either commit. Stop after commit 2; do not push. This lifecycle is for completed implementation, not this initial documentation/proposal bootstrap.
