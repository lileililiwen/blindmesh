# Blindmesh

Find what you couldn't see. Build alone, test together.

Blindmesh is a mutual testing platform for independent developers. Contributors earn internal Credits for useful testing and spend them on exploration, directed verification, and authorized resource collaboration.

**Status: all thirteen packages run.** The API, the worker, the PostgreSQL schema, the generated API client, the web application, the GitHub sign in, the public contributor profile, the project workspaces with their visibility and membership, the capability directory with its eligibility evaluation, the wallet with the conserved credit balances and the limited grant, the task marketplace with the publication that reserves the whole budget of a task before it opens, the seat of a contributor with its application, its lease, its start and its deadlines, the evidence of a finished seat with its private files, its findings and its settlement, the moderation of the reports and of the credit disputes with its reasoned decisions, the reputation that counts the accepted contributions and the confirmed findings of a contributor, the notifications of the workflow events with their in-app items, their emails and the preferences of the recipient, the pilot operations with their runbooks, their restore rehearsal, their authorized deletion and their measured gate, and the scoped matching with the three visibility levels, the list of the candidates of a task and the fair exploration of the accounts are implemented, built and tested. Every package of the delivery order is archived. See [HANDOFF](HANDOFF.md) for the actual evidence and the remaining boundaries.

## Start here

- [Source PRD](requirements.md) — Chinese PRD v1.0, 2026-10-19; preserved unchanged.
- [Delivery order](ROADMAP.md) and [package map](openspec/package-map.md).
- [Architecture and shared contracts](docs/architecture.md).
- [Requirements coverage](docs/requirements-coverage.md).
- [Handoff and actual evidence](HANDOFF.md).
- [Capture plan and real screenshots](docs/assets/capture-plan.md).

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

## Sign in locally

The sign in of a local environment uses the seeded accounts of the local GitHub fixture, which only runs with `APP_ENV=test` and never in production. A plain HTTP host needs the development cookie mode and its own address in the allowed origins:

```sh
ASPNETCORE_URLS=http://127.0.0.1:58090 APP_ENV=test \
  Blindmesh__Identity__LocalFixtureEnabled=true \
  Blindmesh__Identity__PublicBaseUrl=http://127.0.0.1:53100 \
  Blindmesh__Identity__CookieSecurity=Development \
  Api__AllowedOrigins__0=http://127.0.0.1:53100 \
  Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=5432;Database=blindmesh;Username=blindmesh;Password=blindmesh" \
  dotnet run --project src/Blindmesh.Api --no-launch-profile &

BLINDMESH_API_URL=http://127.0.0.1:58090 npm --prefix apps/web run start -- -p 53100
```

Open `http://127.0.0.1:53100/sign-in?actor=alice` and sign in as alice, bob, cara, dana or erin.

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
npm --prefix apps/web run test:e2e -- --project=chromium --grep MOD
npm --prefix apps/web run test:a11y -- --grep MOD
npm --prefix apps/web run test:e2e -- --project=chromium --grep REP
npm --prefix apps/web run test:a11y -- --grep REP
npm --prefix apps/web run test:e2e -- --project=chromium --grep NOTIF
npm --prefix apps/web run test:a11y -- --grep NOTIF
npm --prefix apps/web run test:e2e -- --project=chromium --grep OPS
npm --prefix apps/web run test:a11y -- --grep OPS
npm --prefix apps/web run test:e2e -- --project=chromium --grep MATCH
npm --prefix apps/web run test:a11y -- --grep MATCH
python3 scripts/verify_bootstrap.py
node scripts/check-openspec-change-names.mjs
openspec validate --all --strict --no-interactive
```

The browser tests need the web application and the API running. The shared platform packages are not adopted yet because the public platform feed is unreachable; [the adoption note](docs/foundation/platform-adoption.md) records the probe, the blocked state and the retry.

## Planned MVP

GitHub sign-in and profiles; projects; capability declarations; a unified task marketplace; independent participations; structured submissions and findings; protected screenshots/logs; transactional credit escrow, settlement, and refunds; moderation, reputation, notifications, and a verified pilot deployment. The matching of the marketplace is scoped: a project and a task carry the visibility `public`, `matched` or `private`, the owner of a task reads the accounts that fit it, and the order of that list is a fair exploration rule and never a score.

The implementation is C# 14 / .NET 10 / ASP.NET Core 10 / EF Core 10 with Npgsql, PostgreSQL 17, and Next.js 16 / React 19 / TypeScript 5 on Node.js 24. NuGet and npm manage backend and frontend dependencies. The C# backend supersedes the original PRD's Rust recommendation by user direction; the original PRD remains unchanged. See the architecture for ownership, contracts, UI behavior, and dependency policy.

## Boundaries

Credits are integer community accounting units with no cash value, withdrawal, exchange, or blockchain. Credentials for third-party resources are never collected. Authorized resource owners operate their own accounts or use official limited delegation. AI, native mobile apps, live video infrastructure, teams, paid credits, and external issue synchronization are deferred.

MIT license. Domain and trademark availability are unverified. [Bootstrap evidence](docs/bootstrap-evidence.md) records the tooling and publication status. Forge manages development metadata; the product runs without Forge or sibling checkouts.
