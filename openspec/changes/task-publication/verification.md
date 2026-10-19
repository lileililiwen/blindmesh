# Verification: TASK

Status: NOT RUN — application manifests/runtime do not exist at bootstrap. Every command below is a future behavioral requirement, not evidence of a passing implementation. foundation owns these command interfaces.

1. Run `dotnet restore Blindmesh.sln --locked-mode`, `dotnet format Blindmesh.sln --verify-no-changes`, `dotnet build Blindmesh.sln -c Release --no-restore` and `dotnet test Blindmesh.sln -c Release --no-build --filter FullyQualifiedName~TASK`.
2. Run `npm --prefix apps/web ci`, `npm --prefix apps/web run lint`, `npm --prefix apps/web run typecheck`, `npm --prefix apps/web run build` and `npm --prefix apps/web run api:check`.
3. Start `docker compose -f infra/compose.dev.yaml up -d --build`; run `dotnet run --project src/Blindmesh.Api -- migrate` against a dedicated test database. Use injected time and fictional alice (owner), bob (eligible contributor), cara (ineligible), dana (moderator), erin (administrator). Never enable test auth in production.
4. Run `npm --prefix apps/web run test:e2e -- --project=chromium --grep TASK` and `npm --prefix apps/web run test:a11y -- --grep TASK`. Cover /tasks, /tasks/new, /tasks/{id}, /projects/{id}/tasks; loading/empty/invalid/forbidden/unavailable/success/recovery, stale/duplicate requests, mobile overflow, keyboard focus and both themes. Release smoke also uses Firefox/WebKit.
5. Every scenario in traceability.md must assert its specific observable result; include rejected unauthorized reads/writes, real database state and unique effects. Test fresh migrations and upgrade from the previous accepted package. Capture only seeded non-sensitive data with global browser tooling per docs/assets/capture-plan.md.
6. Run `python3 scripts/verify_bootstrap.py`, `node scripts/check-openspec-change-names.mjs`, `openspec validate --all --strict --no-interactive` and the approved local `forge gate .`. Record Gate findings honestly; no unavailable check waives acceptance.

Only then check implementation tasks, archive normally without skipping canonical specs, and perform the exact two-commit completion workflow. Structural validation proves artifact consistency only.
