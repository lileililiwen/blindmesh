# Blindmesh

Find what you couldn't see. Build alone, test together.

Blindmesh is a mutual testing platform for independent developers. Contributors earn internal Credits for useful testing and spend them on exploration, directed verification, and authorized resource collaboration.

**Status: the runtime foundation runs.** The API, the worker, the PostgreSQL schema, the generated API client and the web application are implemented, built and tested. The marketplace features (identity, projects, tasks, credits, evidence, moderation) are still specified, not implemented. See [HANDOFF](HANDOFF.md) for the actual evidence and the remaining boundaries.

## Start here

- [Source PRD](requirements.md) — Chinese PRD v1.0, 2026-10-19; preserved unchanged.
- [Delivery order](ROADMAP.md) and [package map](openspec/package-map.md).
- [Architecture and shared contracts](docs/architecture.md).
- [Requirements coverage](docs/requirements-coverage.md).
- [Handoff and actual evidence](HANDOFF.md).

## Run the runtime

Prerequisites: .NET SDK 10.0.400, Node.js 24 with npm, Docker Compose v2, PostgreSQL 17.

```sh
docker compose -f infra/compose.dev.yaml up -d --build
dotnet run --project src/Blindmesh.Api -- migrate
dotnet run --project src/Blindmesh.Api          # http://127.0.0.1:8080
dotnet run --project src/Blindmesh.Worker
npm --prefix apps/web ci && npm --prefix apps/web run build && npm --prefix apps/web run start
```

The home page of the web application shows the real availability of the runtime. `GET /v1/health/live` reports the process, `GET /v1/health/ready` reports the database, the object storage and the mail sink.

## Verify

```sh
dotnet restore Blindmesh.sln --locked-mode
dotnet format Blindmesh.sln --verify-no-changes
dotnet build Blindmesh.sln -c Release --no-restore
dotnet test Blindmesh.sln -c Release --no-build
npm --prefix apps/web ci
npm --prefix apps/web run lint
npm --prefix apps/web run typecheck
npm --prefix apps/web run build
npm --prefix apps/web run api:check
npm --prefix apps/web run test:e2e -- --project=chromium --grep FOUND
npm --prefix apps/web run test:a11y -- --grep FOUND
python3 scripts/verify_bootstrap.py
node scripts/check-openspec-change-names.mjs
openspec validate --all --strict --no-interactive
```

The browser tests need the web application and the API running. The shared platform packages are not adopted yet because the public platform feed is unreachable; [the adoption note](docs/foundation/platform-adoption.md) records the probe, the blocked state and the retry.

## Planned MVP

GitHub sign-in and profiles; projects; capability declarations; a unified task marketplace; independent participations; structured submissions and findings; protected screenshots/logs; transactional credit escrow, settlement, and refunds; moderation, reputation, notifications, and a verified pilot deployment.

The implementation is C# 14 / .NET 10 / ASP.NET Core 10 / EF Core 10 with Npgsql, PostgreSQL 17, and Next.js 16 / React 19 / TypeScript 5 on Node.js 24. NuGet and npm manage backend and frontend dependencies. The C# backend supersedes the original PRD's Rust recommendation by user direction; the original PRD remains unchanged. See the architecture for ownership, contracts, UI behavior, and dependency policy.

## Boundaries

Credits are integer community accounting units with no cash value, withdrawal, exchange, or blockchain. Credentials for third-party resources are never collected. Authorized resource owners operate their own accounts or use official limited delegation. AI, native mobile apps, live video infrastructure, teams, paid credits, and external issue synchronization are deferred.

MIT license. Domain and trademark availability are unverified. [Bootstrap evidence](docs/bootstrap-evidence.md) records the tooling and publication status. Forge manages development metadata; the product runs without Forge or sibling checkouts.
