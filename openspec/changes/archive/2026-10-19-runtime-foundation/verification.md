# Verification: FOUND

Status: RUN — the runtime foundation is implemented and the commands below ran against the real services. Every result is the observed output of the command, not a structural pass. The unavailable checks are named.

## Commands and results

| Command | Result |
|---|---|
| `dotnet restore Blindmesh.sln --locked-mode` | PASS. Six projects restored from the committed `packages.lock.json` files. The public platform feed stays unreachable, so every restore also prints `warning NU1900` for that source. The warning is the visible evidence of the blocked platform adoption. |
| `dotnet format Blindmesh.sln --verify-no-changes` | PASS. No file needs formatting. |
| `dotnet build Blindmesh.sln -c Release --no-restore` | PASS. Five product projects and one test project, no compiler warning. |
| `dotnet test Blindmesh.sln -c Release --no-build --filter FullyQualifiedName~FOUND` | PASS. 21 tests, 0 failed. They run against an isolated PostgreSQL 17 container started by Testcontainers. |
| `npm --prefix apps/web ci` | PASS. 391 packages, `package-lock.json` committed. |
| `npm --prefix apps/web run lint` | PASS. No warning. |
| `npm --prefix apps/web run typecheck` | PASS. No error. |
| `npm --prefix apps/web run build` | PASS. Next.js 16.4.2, routes `/` and `/api/[...path]`. |
| `npm --prefix apps/web run api:check` | PASS. The committed `packages/api-client/openapi.json` and `src/generated/schema.ts` match a fresh generation, byte for byte. |
| `docker compose -f infra/compose.dev.yaml up -d --build` | PASS. PostgreSQL 17.11, the object storage, the mail sink, the API, the worker, the web application and the Caddy proxy started. |
| `dotnet run --project src/Blindmesh.Api -- migrate` | PASS. The migration `InitialRuntime` created `outbox_messages` and `idempotency_records` with their constraints and indexes. |
| `npm --prefix apps/web run test:e2e -- --project=chromium --grep FOUND` | PASS. 8 browser journeys. |
| `npm --prefix apps/web run test:a11y -- --grep FOUND` | PASS. 4 axe runs, no violation in the light theme, the dark theme, at 360 pixels and in the unavailable state. |
| `python3 scripts/verify_bootstrap.py` | PASS. 12 changes, 37 requirements, 111 scenarios, 109 unchecked tasks in the remaining proposals; the shared token pin and the 12 contrast pairs are verified. |
| `node scripts/check-openspec-change-names.mjs` | PASS. |
| `openspec validate --all --strict --no-interactive` | PASS. 12 changes validated, 0 failed. |
| `forge gate .` | NOT RUN. The Forge binary is not on PATH in this environment. The evidence of the local checks is in this file and in `HANDOFF.md`. |

## Observed behaviour

- `GET /v1/health/live` answers 200 with `{"status":"alive","checked_at":"..."}` and no dependency state.
- `GET /v1/health/ready` answers 200 with the three real dependencies and 503 with the same list when PostgreSQL is unreachable. The liveness stays 200 in that case.
- `POST /v1/runtime/heartbeat` without an `Idempotency-Key` answers 400 with `invalid_idempotency_key` and the field error. The same key with the same payload answers 201 with the original body. The same key with another payload answers 409 with `idempotency_key_reuse`. The outbox holds exactly one row for one accepted command.
- An unsafe request with an origin outside `Api:AllowedOrigins` answers 403 before the handler runs, and no outbox row is written.
- The served OpenAPI document is byte identical on two requests. The generation command writes the same bytes twice.
- The worker claims the pending outbox row with `FOR UPDATE SKIP LOCKED` and marks it processed.
- The home page shows the real availability, explains the unavailable, unreachable, forbidden and empty states, keeps the focus on the retry button and has no horizontal overflow at 360, 768 and 1440 pixels.

## Unavailable checks and boundaries

- The shared `Platform.*` packages are not adopted: the public platform feed answers 404. The adoption stays blocked and the reason is recorded in `docs/foundation/platform-adoption.md`. The product does not replace the platform packages with a local copy.
- `forge gate .` did not run because the Forge binary is not installed in this environment.
- The release smoke on Firefox and WebKit did not run: only the Chromium browser was installed. The Chromium journeys and the axe runs are the evidence.
- The screenshots in `docs/assets/` are the real rendered home page of the compose stack, in the light theme, the dark theme and at 360 pixels. The capture commands are in `docs/assets/capture-plan.md`.
- The session bound CSRF token check belongs to the identity package. Until then an unsafe request without an `Origin` header is treated as a non-browser client and is not blocked by the origin guard.
- The marketplace features are not implemented. The page says so instead of showing a fictional dashboard.

Only then check implementation tasks, archive normally without skipping canonical specs, and perform the exact two-commit completion workflow. Structural validation proves artifact consistency only.
