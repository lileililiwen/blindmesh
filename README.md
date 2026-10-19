# Blindmesh

Find what you couldn't see. Build alone, test together.

Blindmesh is a proposed mutual testing platform for independent developers. Contributors earn internal Credits for useful testing and spend them on exploration, directed verification, and authorized resource collaboration.

**Status: documentation and specifications only.** No runnable application, production service, migrations, or product tests exist. Bootstrap verification cannot establish product readiness.

## Start here

- [Source PRD](requirements.md) — Chinese PRD v1.0, 2026-10-19; preserved unchanged.
- [Delivery order](ROADMAP.md) and [package map](openspec/package-map.md).
- [Architecture and shared contracts](docs/architecture.md).
- [Requirements coverage](docs/requirements-coverage.md).
- [Handoff and actual evidence](HANDOFF.md).

## Planned MVP

GitHub sign-in and profiles; projects; capability declarations; a unified task marketplace; independent participations; structured submissions and findings; protected screenshots/logs; transactional credit escrow, settlement, and refunds; moderation, reputation, notifications, and a verified pilot deployment.

The planned implementation is C# 14 / .NET 10 / ASP.NET Core 10 / EF Core 10 with Npgsql, PostgreSQL 17, and Next.js 16 / React 19 / TypeScript 5 on Node.js 24. NuGet and npm manage backend and frontend dependencies. The C# backend supersedes the original PRD's Rust recommendation by user direction; the original PRD remains unchanged. See the architecture for ownership, contracts, UI behavior, and dependency policy.

## Documentation verification

Prerequisites: Python 3.12+, OpenSpec CLI 1.6.0; Node.js 24 runs the OpenSpec CLI. Ensure these tools are on PATH.

```sh
python3 scripts/verify_bootstrap.py
node scripts/check-openspec-change-names.mjs
openspec list
openspec validate --all --strict --no-interactive
```

These commands validate planning artifacts. Application quickstart, build, and browser commands are acceptance deliverables of `runtime-foundation`; they do not work yet.

## Boundaries

Credits are integer community accounting units with no cash value, withdrawal, exchange, or blockchain. Credentials for third-party resources are never collected. Authorized resource owners operate their own accounts or use official limited delegation. AI, native mobile apps, live video infrastructure, teams, paid credits, and external issue synchronization are deferred.

MIT license. Domain and trademark availability are unverified. [Bootstrap evidence](docs/bootstrap-evidence.md) records actual tooling and publication status. Forge manages development metadata; the product must run without Forge or sibling checkouts.
