# Blindmesh agent entry point

@/home/paul/.codex/RTK.md

## Project facts

- Current state: the runtime foundation, the identity package, the project package, the capability package, the credit package and the task publication package are implemented, built and tested; the remaining marketplace packages are specified only.
- Backend: C# 14, .NET SDK 10.0.400, ASP.NET Core 10, EF Core 10/Npgsql, PostgreSQL 17.
- Frontend: Node.js 24, npm, Next.js 16, React 19, TypeScript 5, Tailwind 4, shadcn/ui style primitives.
- Verification: `dotnet restore Blindmesh.sln --locked-mode && dotnet format Blindmesh.sln --verify-no-changes && dotnet build Blindmesh.sln -c Release --no-restore && dotnet test Blindmesh.sln -c Release --no-build` and `npm --prefix apps/web ci && npm --prefix apps/web run lint && npm --prefix apps/web run typecheck && npm --prefix apps/web run build && npm --prefix apps/web run api:check`.
- Planning verification: `python3 scripts/verify_bootstrap.py && node scripts/check-openspec-change-names.mjs && openspec validate --all --strict --no-interactive`.
- `requirements.md`: original PRD; preserve it.
- `src/`: C# API, worker, application, domain and infrastructure. `apps/web/`: the only web application. `packages/api-client/`: the generated TypeScript client. `tests/`: xUnit integration tests and the Playwright journeys. `infra/`: Compose and Caddy configuration.
- `docs/`: architecture, PRD coverage, design baseline, evidence.
- `openspec/`: config, package map, dependency-ordered active change proposals.
- `scripts/`: documentation verification only.
- `.github/workflows/`: planning verification CI; no application build/deploy pipeline yet.
- `.ai-rules/`: workflow, completion, and concern packs.
- `.ai-gate/`: shared Gate declaration; no copied Gate runtime.
- Use kebab-case changes, PascalCase C# types/namespaces and React components.
- Update this block and affected docs in the same change as directory, tool, or command changes.

## Workflow and invariants

Work on `main` by default. Do not create a branch/worktree unless explicitly requested.
OpenSpec is required for non-trivial changes. Read [HANDOFF](HANDOFF.md) and [package map](openspec/package-map.md) first.
Many proposed changes may exist; select and implement only one eligible change at a time.
Use BFS → DFS → BFS and the four task phases in [.ai-rules/workflow.md](.ai-rules/workflow.md).
UI/UX is the first product-planning priority. APIs do not replace interfaces.
Every change assesses UI/UX; human-facing completion requires usable UI, browser flows, responsive and accessibility evidence.
Use the `ponytail` skill for coding, full by default: understand the complete impact surface, then minimize code, dependencies, files, and unrequested explanation without weakening explicit requirements, validation, security, accessibility, error handling, tests, or verification.
Assess sibling/common/manager capabilities before creating abstractions; see [architecture reconnaissance](docs/architecture.md#shared-architecture-reconnaissance).
Only Credits owns ledger writes. Never overwrite settled entries or accept negative user balances.
Tasks, participations, submissions, findings, and disputes have separate state machines.
Enforce visibility and authorization on every read, write, export, and attachment.
Never collect resource passwords, production cookies, payment credentials, or third-party session tokens.
Do not add sibling source/runtime dependencies or invent metrics and verification evidence.

## Completion

Read [.ai-rules/completion.md](.ai-rules/completion.md) and the matching [concern pack](.ai-rules/concerns/).
Maintain one first-line `current_spec:` in HANDOFF; advance after completion, remove if the active queue is empty.
Each completed implementation change requires exactly two commits: implementation/tests/archive/generated specs, then HANDOFF.md only. Do not ask for conversational authorization or confirmation before either commit. Stop after commit 2; do not push.
Proposal authoring does not authorize feature implementation, archival, or implementation commits.
Build/test/skeleton success is not DONE. Record unavailable checks and blockers.
Real documentation captures follow [capture plan](docs/assets/capture-plan.md); refresh after milestones.
Repository license and discoverability follow [bootstrap evidence](docs/bootstrap-evidence.md). GitHub publication and metadata verification are required before claiming initialization complete; preserve the existing license.
