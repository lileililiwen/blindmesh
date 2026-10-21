current_spec: notification-delivery

# Blindmesh handoff

Date: 2026-10-21. Scope: the runtime foundation, the identity package, the project package, the capability package, the credit package, the task publication package, the participation execution package, the evidence package, the moderation package and the reputation package are implemented, verified and archived. The next change is selected but not started.

## Current state and next action

Ten packages are implemented and archived: the runtime foundation as `openspec/changes/archive/2026-10-19-runtime-foundation`, the identity package as `openspec/changes/archive/2026-10-20-identity-profiles`, the project package as `openspec/changes/archive/2026-10-20-project-workspaces`, the capability package as `openspec/changes/archive/2026-10-20-capability-directory`, the credit package as `openspec/changes/archive/2026-10-20-credit-ledger`, the task publication package as `openspec/changes/archive/2026-10-20-task-publication`, the participation execution package as `openspec/changes/archive/2026-10-20-participation-execution`, the evidence package as `openspec/changes/archive/2026-10-20-evidence-findings`, the moderation package as `openspec/changes/archive/2026-10-21-moderation-arbitration` and the reputation package as `openspec/changes/archive/2026-10-21-reputation-history`. Their generated canonical specs are `openspec/specs/runtime-foundation/spec.md`, `openspec/specs/identity-profiles/spec.md`, `openspec/specs/project-workspaces/spec.md`, `openspec/specs/capability-directory/spec.md`, `openspec/specs/credit-ledger/spec.md`, `openspec/specs/task-publication/spec.md`, `openspec/specs/participation-execution/spec.md`, `openspec/specs/evidence-findings/spec.md`, `openspec/specs/moderation-arbitration/spec.md` and `openspec/specs/reputation-history/spec.md`. The product now runs: a C# API, a worker, a PostgreSQL 17 schema, a generated TypeScript client and a Next.js 16 web application with a GitHub sign in, a public contributor profile, the project workspaces with their visibility and membership, the capability directory with its eligibility evaluation, the wallet with the conserved credit balances and the limited grant, the task marketplace with the publication that reserves the whole budget of a task before it opens, the seat of a contributor in a task with its application, its lease, its start and its deadlines, the evidence of a finished seat with its private quarantined files, its findings, its one correction and its settlement through the credit ledger, the moderation of the reports and of the credit disputes with an independent moderator, a reasoned award, a funded bonus, an appeal that compensates the paid amount and a ban of an account, and the reputation of a contributor that counts the accepted contributions and the confirmed findings, keeps the source of every event and hides a source that is no longer public.

Two packages remain proposed. `notification-delivery` is the only dependency-eligible change, because its prerequisite is archived. `pilot-operations` waits for it. `openspec/package-map.md` and `openspec/change-map.json` agree.

The sole first-line current_spec is the machine-readable selected active package. Advance it after verified implementation and archive; remove it when the active queue becomes empty.

MIT license is preserved. Source PRD SHA256 remains 718d1f7bb8884c803fb7aa6b8b8fc88c5afba6b9688884dad187f6eb3d7ec91b. Git is on main.

## Verification evidence

- `dotnet restore Blindmesh.sln --locked-mode`: PASS. Six projects restore from the committed `packages.lock.json` files. Every restore also prints `warning NU1900` for the public platform feed, because that feed is unreachable. The warning is the visible evidence of the blocked platform adoption.
- `dotnet format Blindmesh.sln --verify-no-changes`: PASS. No file needs formatting.
- `dotnet build Blindmesh.sln -c Release --no-restore`: PASS. Five product projects and one test project, no compiler warning.
- `dotnet test Blindmesh.sln -c Release --no-build`: PASS. 126 tests, 0 failed, in 32 seconds, against an isolated PostgreSQL 17 container started by Testcontainers. The 13 tests of the participation package run alone with the filter `FullyQualifiedName~PART`, the 8 tests of the evidence package with the filter `FullyQualifiedName~EVID`, the 14 tests of the moderation package with the filter `FullyQualifiedName~MOD` and the 6 tests of the reputation package with the filter `FullyQualifiedName~Reputation`.
- `npm --prefix apps/web ci`: PASS. The lock file is committed and unchanged.
- `npm --prefix apps/web run lint`, `run typecheck`, `run build`: PASS. No warning, no error, Next.js 16.4.2. The build lists `/tasks`, `/tasks/new`, `/tasks/[id]`, `/projects/[id]/tasks`, `/tasks/[id]/apply`, `/work`, `/work/[id]`, `/work/[id]/submit`, `/projects/[id]/reviews`, `/findings/[id]`, `/reports/new`, `/disputes/[id]`, `/admin/reports`, `/admin/disputes`, `/people/[username]/history` and `/settings/reputation`.
- `npm --prefix apps/web run api:check`: PASS. The committed `packages/api-client/openapi.json` and `src/generated/schema.ts` match a fresh generation, byte for byte.
- `docker compose -f infra/compose.dev.yaml up -d --build`: PASS. PostgreSQL 17.11, the object storage, the mail sink, the API, the worker, the web application and the Caddy proxy start. `dotnet run --project src/Blindmesh.Api -- migrate` applies the whole migration stream, the last migration of the stream being `20261021154542_AddReputationHistory`.
- `npm --prefix apps/web run test:e2e -- --project=chromium --grep FOUND`: PASS. 8 journeys against the compose stack.
- `npm --prefix apps/web run test:e2e -- --project=chromium --grep IDENT`: PASS. 12 journeys. The sign in needs the local OAuth fixture, so the API runs with `APP_ENV=test` and the web application points at it. The exact commands are in `openspec/changes/archive/2026-10-20-identity-profiles/verification.md` and in the README.
- `npm --prefix apps/web run test:e2e -- --project=chromium --grep PROJ`: PASS. 15 journeys, 13 of the project package and 2 of the task package, because their titles hold the word "project". The exact commands are in `openspec/changes/archive/2026-10-20-project-workspaces/verification.md`.
- `npm --prefix apps/web run test:e2e -- --project=chromium --grep CAP`: PASS. 12 journeys. The exact commands are in `openspec/changes/archive/2026-10-20-capability-directory/verification.md`.
- `npm --prefix apps/web run test:e2e -- --project=chromium --grep CRED`: PASS. 8 journeys of the wallet and the wallet history. The exact commands are in `openspec/changes/archive/2026-10-20-credit-ledger/verification.md`.
- `npm --prefix apps/web run test:e2e -- --project=chromium --grep TASK`: PASS. 14 journeys, 13 of the task package and 1 of the capability package, because its title holds the word "task". The exact commands are in `openspec/changes/archive/2026-10-20-task-publication/verification.md`.
- `npm --prefix apps/web run test:e2e -- --project=chromium --grep PART`: PASS. 8 journeys of the participation package. The exact commands are in `openspec/changes/archive/2026-10-20-participation-execution/verification.md`.
- `npm --prefix apps/web run test:e2e -- --project=chromium --grep EVID`: PASS. 10 journeys of the evidence package. The exact commands are in `openspec/changes/archive/2026-10-20-evidence-findings/verification.md`.
- `npm --prefix apps/web run test:e2e -- --project=chromium --grep MOD`: PASS. 13 journeys of the moderation package. The exact commands are in `openspec/changes/archive/2026-10-21-moderation-arbitration/verification.md`.
- `npm --prefix apps/web run test:e2e -- --project=chromium --grep REP`: PASS. 10 journeys, 7 of the reputation package and 3 of the moderation package, because their titles hold the word "report". The exact commands are in `openspec/changes/archive/2026-10-21-reputation-history/verification.md`.
- `npm --prefix apps/web run test:a11y -- --grep FOUND`, `--grep IDENT`, `--grep PROJ`, `--grep CAP`, `--grep CRED`, `--grep TASK`, `--grep PART`, `--grep EVID`, `--grep MOD` and `--grep REP`: PASS. 4, 6, 7, 5, 5, 9, 6, 4, 8 and 9 axe runs, no violation. The PROJ group holds one run more than before, because the axe run of the reviews of a project of the evidence package carries the word "project" in its title.
- `python3 scripts/verify_bootstrap.py`: PASS. 12 changes, 37 requirements, 111 scenarios, 36 unchecked tasks; the shared token SHA256 and the 12 contrast pairs are verified.
- `node scripts/check-openspec-change-names.mjs`: PASS.
- `openspec validate --all --strict --no-interactive`: 12 passed, 0 failed.
- `python3 ../forge/modules/workspace-governance/scripts/ui_conformance.py . --json`: PASS, no finding. The product UI files are in scope. This is not browser accessibility evidence; the axe runs above are.

## Blockers and remaining boundaries

The shared `Platform.*` packages are not adopted. The public platform feed answers 404, so the restore of a platform package fails visibly and the adoption stays blocked. The product does not replace the platform packages with a local copy. The probe, the blocked state and the retry command are in `docs/foundation/platform-adoption.md`.

`forge gate .` did not run: the Forge binary is not on PATH in this environment. The Gate manifest binds the native build, test and security checks and each of them passes when run directly.

The test harness of the foundation package had a defect that the participation package found: the runner of a native command waited for the end of the output of the child, and the reused MSBuild workers kept the inherited handle of the pipe open, so the suite hung for ten minutes. The runner now waits for the exit of the process and bounds the read of the output, so the plain `dotnet test` command reports the whole suite in 28 seconds.

The release smoke on Firefox and WebKit did not run. Only the Chromium browser is installed in this environment.

`npm audit` reports high severity advisories in `braces`, `fast-glob` and `micromatch` through the dev-only `eslint-config-next` chain. The latest `braces` release is still affected, so there is no patched version to move to. The shipped production dependency set audits clean; the finding is recorded rather than hidden.

The grant eligibility of the seeded accounts belongs to the credit package. The identity package stores the provider email and does not decide the grant.

The matching scope of a project with the visibility `matched` belongs to a later package. A matched project is readable by its owner and its members until the matching exists.

The task publication package is implemented, so a published task with hard conditions exists. The eligibility route of a task answers with the conditions of that task through the seam `ITaskConditionReader`, whose implementation is now `PublishedTaskConditionReader`; a task that the actor may not see still answers 404.

The escrow primitives of the credit package — reserve, settle and refund — are internal methods of the credit service. There is no public transfer and no balance editor. The task publication package calls the reserve and the refund with the same database context and the same transaction as its own command, and the participation package calls the pending with the same rule. The evidence package now settles a seat with the same rule: a granted bonus moves from the reserved bucket of the publisher into the pending bucket of the contributor, the base and the bonus are paid out with the settle, and the unused bonus of the slot goes back to the publisher with the refund of an unstarted seat. The evidence package opens a dispute and holds the credits; the moderation package owns the case and the decision. The decision moves only the base of the seat. A bonus uses the pending primitive first, so it is refused with 409 when the cap of the task is exceeded, and an appeal compensates the paid amount with new journal rows. The signed sum of every transaction of a case is 0 and no account ever goes below zero.

The reputation of a contributor is derived from the events of the outbox, so the worker owns the projection. The evidence package writes the event of an accepted seat and the moderation package writes the event of a reversal, both inside the transaction of their own command. The worker registers the moderation module, because the findings module settles a seat through it, and the claim cycle counts only the messages it really applied, so an event without a handler does not keep the cycle busy.

The remaining marketplace features are not implemented. The pages say so instead of showing a fictional dashboard. Browser flows, responsive behavior and rendered accessibility exist for the runtime, for the identity, for the project workspaces, for the capability directory, for the wallet, for the task marketplace, for the seat of a contributor, for the evidence of a finished seat, for the moderation of the cases and for the reputation of a contributor only.

## Workflow

Work directly on main. Before selecting a package, run the change-name preflight; implement only one dependency-eligible package using BFS → DFS → BFS and all four task phases. Assess UI/UX in every change; APIs do not replace interfaces. Human-facing completion requires usable UI, browser journeys, responsive checks and accessibility evidence.

Each completed implementation change requires exactly two commits: implementation/tests/archive/generated specs, then HANDOFF.md only. Do not ask for conversational authorization or confirmation before either commit. Stop after commit 2; do not push.
