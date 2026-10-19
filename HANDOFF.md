current_spec: identity-profiles

# Blindmesh handoff

Date: 2026-10-20. Scope: the runtime foundation is implemented, verified and archived. The next change is selected but not started.

## Current state and next action

The runtime foundation is implemented and archived as `openspec/changes/archive/2026-10-19-runtime-foundation`; the generated canonical spec is `openspec/specs/runtime-foundation/spec.md`. The product now runs: a C# API, a worker, a PostgreSQL 17 schema, a generated TypeScript client and a Next.js 16 web application, started by `docker compose -f infra/compose.dev.yaml up -d --build`.

Eleven packages remain proposed. They hold 33 requirements, 102 scenarios and 99 unchecked implementation tasks. `identity-profiles` is the only dependency-eligible change, because its only prerequisite is archived. `openspec/package-map.md` and `openspec/change-map.json` agree.

The sole first-line current_spec is the machine-readable selected active package. Advance it after verified implementation and archive; remove it when the active queue becomes empty.

MIT license is preserved. Source PRD SHA256 remains 718d1f7bb8884c803fb7aa6b8b8fc88c5afba6b9688884dad187f6eb3d7ec91b. Git is on main.

## Verification evidence

- `dotnet restore Blindmesh.sln --locked-mode`: PASS. Six projects restore from the committed `packages.lock.json` files. Every restore also prints `warning NU1900` for the public platform feed, because that feed is unreachable. The warning is the visible evidence of the blocked platform adoption.
- `dotnet format Blindmesh.sln --verify-no-changes`: PASS. No file needs formatting.
- `dotnet build Blindmesh.sln -c Release --no-restore`: PASS. Five product projects and one test project, no compiler warning.
- `dotnet test Blindmesh.sln -c Release --no-build`: PASS. 21 tests, 0 failed, against an isolated PostgreSQL 17 container started by Testcontainers.
- `npm --prefix apps/web ci`: PASS. 391 packages, the lock file is committed.
- `npm --prefix apps/web run lint`, `run typecheck`, `run build`: PASS. No warning, no error, Next.js 16.4.2.
- `npm --prefix apps/web run api:check`: PASS. The committed `packages/api-client/openapi.json` and `src/generated/schema.ts` match a fresh generation, byte for byte.
- `docker compose -f infra/compose.dev.yaml up -d --build`: PASS. PostgreSQL 17.11, the object storage, the mail sink, the API, the worker, the web application and the Caddy proxy start. `dotnet run --project src/Blindmesh.Api -- migrate` applies the migration.
- `npm --prefix apps/web run test:e2e -- --project=chromium --grep FOUND`: PASS. 8 browser journeys against the compose stack.
- `npm --prefix apps/web run test:a11y -- --grep FOUND`: PASS. 4 axe runs, no violation in the light theme, the dark theme, at 360 pixels and in the unavailable state.
- `python3 scripts/verify_bootstrap.py`: PASS. 12 changes, 37 requirements, 111 scenarios, 99 unchecked tasks; the shared token SHA256 and the 12 contrast pairs are verified.
- `node scripts/check-openspec-change-names.mjs`: PASS.
- `openspec validate --all --strict --no-interactive`: 12 passed, 0 failed.
- `python3 ../forge/modules/workspace-governance/scripts/ui_conformance.py . --json`: PASS, no finding. 9 product UI files are in scope. This is not browser accessibility evidence; the axe runs above are.
- The Gate manifest now binds the native checks: `dotnet build`, `dotnet test`, the NuGet vulnerability scan and the frontend production audit. Each of them passes when run directly.
- The real rendered captures of the home page are in `docs/assets/`; the exact commands are in `docs/assets/capture-plan.md`.

## Blockers and remaining boundaries

The shared `Platform.*` packages are not adopted. The public platform feed answers 404, so the restore of a platform package fails visibly and the adoption stays blocked. The product does not replace the platform packages with a local copy. The probe, the blocked state and the retry command are in `docs/foundation/platform-adoption.md`.

`forge gate .` did not run: the Forge binary is not on PATH in this environment. The local project checks above are the evidence. The workspace governance check still reports the pre-existing central inventory and gate debt findings of the bootstrap session; they are separate work and were not suppressed from the product.

The release smoke on Firefox and WebKit did not run. Only the Chromium browser is installed in this environment.

`npm audit` reports high severity advisories in `braces`, `fast-glob` and `micromatch` through the dev-only `eslint-config-next` chain. The latest `braces` release is still affected, so there is no patched version to move to. The shipped production dependency set audits clean; the finding is recorded rather than hidden.

The session bound CSRF token check belongs to the identity package. Until then an unsafe request without an `Origin` header is treated as a non-browser client and is not blocked by the origin guard.

The marketplace features are not implemented. The home page says so instead of showing a fictional dashboard. Browser flows, responsive behavior and rendered accessibility exist for the runtime only.

## Workflow

Work directly on main. Before selecting a package, run the change-name preflight; implement only one dependency-eligible package using BFS → DFS → BFS and all four task phases. Assess UI/UX in every change; APIs do not replace interfaces. Human-facing completion requires usable UI, browser journeys, responsive checks and accessibility evidence.

Each completed implementation change requires exactly two commits: implementation/tests/archive/generated specs, then HANDOFF.md only. Do not ask for conversational authorization or confirmation before either commit. Stop after commit 2; do not push.
