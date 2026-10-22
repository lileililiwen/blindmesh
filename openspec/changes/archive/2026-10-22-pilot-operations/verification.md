# Verification

The verification of the package `pilot-operations`. Every command below was run on the machine of the worker, in the repository `/home/paul/code/blindmesh`, on the branch `main`, on the 22nd of October 2026. The outputs are the observed ones. A check that did not run is named as such and is never reported as a pass.

The run added the operations module, the migration with its three tables, the eight endpoints of the API, the two pages of the web application, the four runbooks and the production compose file, the tests of the package, the journeys of the browser, the runs of axe, the traceability and this record.

## The environment of the run

| Item | Value |
|---|---|
| The API of the worker | `http://127.0.0.1:58094`, started with `APP_ENV=test` and the local OAuth fixture |
| The web application of the worker | `http://127.0.0.1:53104`, started with `BLINDMESH_API_URL=http://127.0.0.1:58094` |
| The database of the journeys | the container `blindmesh-postgres-1` on the port `55432`, the database `bm_ops` |
| The object storage | the container `blindmesh-object-storage-1` on the port `59000` |
| The mail sink | the container `blindmesh-mail-1` on the port `51025` |
| The browsers | Chromium only |
| Node | 22.23.3 |
| npm | 10.9.9 |
| .NET SDK | 10.0.400 |
| Playwright | 1.64.1 |

The database of the run was created and migrated before the journeys:

```
docker exec blindmesh-postgres-1 psql -U blindmesh -d postgres -c 'CREATE DATABASE "bm_ops";'
```

```
CREATE DATABASE
```

```
Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=bm_ops;Username=blindmesh;Password=blindmesh" \
  dotnet run --project src/Blindmesh.Api --no-launch-profile -- migrate
```

```
info: Microsoft.EntityFrameworkCore.Migrations[20402]
      Applying migration '20261021225331_AddNotificationDelivery'.
info: Microsoft.EntityFrameworkCore.Migrations[20402]
      Applying migration '20261022003747_AddPilotOperations'.
info: Blindmesh.Infrastructure.Persistence.DatabaseMigrator[0]
      Database schema is up to date.
```

The name of the database is written between quotes, because an unquoted name is folded to lower case and the API would read another database.

The API of the run:

```
ASPNETCORE_URLS=http://127.0.0.1:58094 APP_ENV=test \
  Blindmesh__Identity__LocalFixtureEnabled=true \
  Blindmesh__Identity__PublicBaseUrl=http://127.0.0.1:53104 \
  Blindmesh__Identity__CookieSecurity=Development \
  Api__AllowedOrigins__0=http://127.0.0.1:53104 \
  Blindmesh__ObjectStorage__Endpoint=http://127.0.0.1:59000 \
  Blindmesh__ObjectStorage__PublicEndpoint=http://127.0.0.1:59000 \
  Blindmesh__ObjectStorage__AccessKey=blindmesh \
  Blindmesh__ObjectStorage__SecretKey=blindmesh-secret \
  Blindmesh__ObjectStorage__Bucket=blindmesh-ops \
  Blindmesh__Mail__Host=127.0.0.1 \
  Blindmesh__Mail__Port=51025 \
  Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=bm_ops;Username=blindmesh;Password=blindmesh" \
  dotnet run --project src/Blindmesh.Api --no-launch-profile
```

The web application of the run:

```
BLINDMESH_API_URL=http://127.0.0.1:58094 npm --prefix apps/web run start -- -p 53104
```

The state of the API and of the web application after the start:

```
$ curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:58094/v1/health/live
200
$ curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:58094/v1/health/ready
200
$ curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:53104/status
200
$ curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:53104/admin/operations
200
```

The web application was warmed with a request before the first journey, because the first request of the browser would otherwise compile the route and the journey would fail.

## The client of PostgreSQL of the host

The client that is installed on the host of the worker is version 15 and it refuses to dump a server of version 17:

```
$ pg_dump --version
pg_dump (PostgreSQL) 15.19 (Ubuntu 15.19-1.pgdg24.04+2)
$ pg_dump --host=127.0.0.1 --port=55432 --username=blindmesh --dbname=blindmesh --format=custom --file=/tmp/probe.dump
pg_dump: 错误: 因为服务器版本不匹配而终止
pg_dump: detail: 服务器版本: 17.11; pg_dump 版本: 15.19 (Ubuntu 15.19-1.pgdg24.04+2)
```

The run therefore used the client of version 17 of the image `postgres:17.11-alpine`, which is the same major version as the server. The client of the host is a small shim in `~/.local/bin` that runs the client of the container and shares the directory of the dump with it; the shim is outside the repository and changes nothing of the product.

The product takes the path of the client as configuration, so a host with a matching client needs no shim at all:

| Variable | What it is |
|---|---|
| `Blindmesh__Operations__PgDumpPath` | The client that dumps the database. |
| `Blindmesh__Operations__PgRestorePath` | The client that restores a dump. |
| `Blindmesh__Operations__PsqlPath` | The client that creates the isolated database. |

## 1. The .NET chain

### `dotnet restore Blindmesh.sln --locked-mode`

```
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Application/Blindmesh.Application.csproj (用时 9.93 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Worker/Blindmesh.Worker.csproj (用时 9.93 秒)。
  已还原 /home/paul/code/blindmesh/tests/Blindmesh.Tests/Blindmesh.Tests.csproj (用时 9.95 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Infrastructure/Blindmesh.Infrastructure.csproj (用时 9.96 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Api/Blindmesh.Api.csproj (用时 9.96 秒)。
```

The exit code is 0. The warning `NU1900` of the unreachable platform feed is expected on every restore and is left visible.

### `dotnet format Blindmesh.sln --verify-no-changes`

```
加载工作区时遇到警告。要记录警告，请将“详细级别”选项设置为“诊断”级别。
```

The exit code is 0. The run did not ask for a change of any file.

### `dotnet build Blindmesh.sln -c Release --no-restore`

```
    5 个警告
    0 个错误

已用时间 00:00:03.43
```

The exit code is 0. The five warnings are the `NU1900` of the unreachable platform feed, one per project of the solution.

### `dotnet test Blindmesh.sln -c Release --no-build`

```
已通过! - 失败:     0，通过:   139，已跳过:     0，总计:   139，持续时间: 53 s - Blindmesh.Tests.dll (net10.0)
```

The exit code is 0. The 139 tests hold the 132 tests of the earlier packages and the 7 tests of this package.

## 2. The tests of the package

```
dotnet test tests/Blindmesh.Tests/Blindmesh.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~Blindmesh.Tests.Operations"
```

```
已通过! - 失败:     0，通过:     7，已跳过:     0，总计:     7，持续时间: 14 s - Blindmesh.Tests.dll (net10.0)
```

The exit code is 0. The seven tests hold the six scenarios of the contract of OPS-1 and OPS-2 and the public status of the product:

| Scenario | Test |
|---|---|
| OPS-1 success | `OPS1SuccessTheOperatorRestoresTheBackupIntoAnIsolatedDatabaseAndEveryCheckPasses` |
| OPS-1 rejection | `OPS1RejectionAFailedLedgerCheckBlocksThePromotionAndTheRecoveryPathStaysAvailable` |
| OPS-1 boundary | `OPS1BoundaryTheDeletionRequestRemovesTheIdentityAndKeepsTheProvenanceOfTheSettledRecords` |
| OPS-2 success | `OPS2SuccessTheGateReportsTheMeasuredCountsAndTheRecordedEvidenceOfThePilot` |
| OPS-2 rejection | `OPS2RejectionTheGateIsRefusedAndTheReleaseStaysBlockedWithTheFailedCommand` |
| OPS-2 boundary | `OPS2BoundaryTheLatencyAndTheAvailabilityStayTargetsUntilTheirWindowIsDocumented` |
| The public status | `OPS2ThePublicStatusHoldsTheStateOfTheRuntimeAndNoPrivateData` |

The tests drive the real API against the isolated PostgreSQL of the test run, they sign in through the local OAuth fixture and they read the database directly to assert the stored state.

The test of the success of OPS-1 is a real rehearsal. It dumps the database of the test container with the client of PostgreSQL, creates the isolated database with the quoted name, restores the dump into it, runs the four checks against the restored database with the same `BlindmeshDbContext` and drops the isolated database again. The test asserts that the four verdicts pass and that no database whose name starts with `bm_rehearsal_` is left behind.

The test of the rejection of OPS-1 writes a transaction of the journal that does not sum to zero, which the credits module never writes. The rehearsal finds it, the ledger check fails, the rehearsal has the status `failed` and the gate blocks the promotion and names the command of the check.

## 3. The rehearsal against the database of the journeys

```
curl -s -b <cookie> -X POST http://127.0.0.1:58094/v1/admin/operations/restores \
  -H 'Content-Type: application/json' -H 'Origin: http://127.0.0.1:53104' \
  -H 'X-CSRF-Token: <token>' -d '{"browser_journeys":11,"axe_runs":8}'
```

```
{
  "id": "e7ed52ce-924b-40cf-b481-6fa39b659065",
  "namespace": "bm_rehearsal_305ee859",
  "started_at": "2026-10-22T01:42:07.8540644+00:00",
  "completed_at": "2026-10-22T01:42:10.6441631+00:00",
  "status": "passed",
  "schema_check": "the restored database holds the expected schema 20261022003747_AddPilotOperations",
  "data_check": "the row counts of 48 tables match the live database",
  "object_check": "the restored database refers to 0 objects and the checksum of their keys matches the live database",
  "ledger_check": "the 0 transactions of the journal sum to zero and no account with an owner holds a negative balance",
  "notes": "",
  "browser_journeys": 11,
  "axe_runs": 8
}
```

The rehearsal passed. The isolated database was dropped again, so the container holds no database of a rehearsal.

## 4. The chain of the web application

### `npm --prefix apps/web ci`

```
added 397 packages in 5s
```

The exit code is 0.

### `npm --prefix apps/web run lint`

```
> blindmesh-web@0.1.0 lint
> eslint .
```

The exit code is 0.

### `npm --prefix apps/web run typecheck`

```
> blindmesh-web@0.1.0 typecheck
> tsc --noEmit
```

The exit code is 0.

### `npm --prefix apps/web run build`

```
○  (Static)   prerendered as static content
ƒ  (Dynamic)  server-rendered on demand
```

The exit code is 0. The build holds the two routes of the package, `/admin/operations` and `/status`.

### `npm --prefix apps/web run api:check`

```
The generated API client matches a fresh generation.
```

The exit code is 0. The generated files `packages/api-client/openapi.json` and `packages/api-client/src/generated/schema.ts` were written by `npm --prefix apps/web run api:generate` and were never edited by hand. The hand-written client holds the eight methods of the package.

## 5. The journeys of the browser

The database was dropped and created again before this run, so the journeys ran against a fresh database that only holds the migration of the release.

```
BLINDMESH_API_URL=http://127.0.0.1:58094 BLINDMESH_WEB_URL=http://127.0.0.1:53104 \
  npm --prefix apps/web run test:e2e -- --project=chromium --grep OPS
```

```
  ✓   1 [chromium] › ../../tests/e2e/pilot-operations/journey.spec.ts:31:5 › OPS-UX the administrator reads the measured gate with the command of every check (2.2s)
  ✓   2 [chromium] › ../../tests/e2e/pilot-operations/journey.spec.ts:55:5 › OPS-UX the rehearsal runs and shows the four verdicts (4.3s)
  ✓   3 [chromium] › ../../tests/e2e/pilot-operations/journey.spec.ts:76:5 › OPS-UX the deletion request asks for the confirmation and keeps the pseudonym (791ms)
  ✓   4 [chromium] › ../../tests/e2e/pilot-operations/journey.spec.ts:103:5 › OPS-UX an ordinary account is refused by the operations of the pilot (372ms)
  ✓   5 [chromium] › ../../tests/e2e/pilot-operations/journey.spec.ts:111:5 › OPS-UX a visitor without a session is told that the area belongs to the administrators (176ms)
  ✓   6 [chromium] › ../../tests/e2e/pilot-operations/journey.spec.ts:118:5 › OPS-UX the public status page shows the real state of the product (198ms)
  ✓   7 [chromium] › ../../tests/e2e/pilot-operations/journey.spec.ts:137:5 › OPS-UX the empty state of the operations explains the next action (418ms)
  ✓   8 [chromium] › ../../tests/e2e/pilot-operations/journey.spec.ts:172:5 › OPS-UX the failure state explains the failure and offers a retry (415ms)
  ✓   9 [chromium] › ../../tests/e2e/pilot-operations/journey.spec.ts:195:5 › OPS-UX the journey has no horizontal overflow at 360, 768 and 1440 pixels (723ms)
  ✓  10 [chromium] › ../../tests/e2e/pilot-operations/journey.spec.ts:215:5 › OPS-UX the journey works with the keyboard only (432ms)
  ✓  11 [chromium] › ../../tests/e2e/pilot-operations/journey.spec.ts:237:5 › OPS-UX the journey works in the dark theme (382ms)

  11 passed (11.1s)
```

The exit code is 0.

## 6. The runs of axe

```
BLINDMESH_API_URL=http://127.0.0.1:58094 BLINDMESH_WEB_URL=http://127.0.0.1:53104 \
  npm --prefix apps/web run test:a11y -- --grep OPS
```

```
  ✓   1 [a11y] › ../../tests/e2e/pilot-operations/accessibility.a11y.ts:27:5 › OPS-UX the operations of the pilot have no accessibility violation (1.1s)
  ✓   2 [a11y] › ../../tests/e2e/pilot-operations/accessibility.a11y.ts:39:5 › OPS-UX the operations of the pilot have no accessibility violation in the dark theme (1.0s)
  ✓   3 [a11y] › ../../tests/e2e/pilot-operations/accessibility.a11y.ts:53:5 › OPS-UX the operations of the pilot have no accessibility violation at 360 pixels (940ms)
  ✓   4 [a11y] › ../../tests/e2e/pilot-operations/accessibility.a11y.ts:67:5 › OPS-UX the deletion form of the operations has no accessibility violation (1.0s)
  ✓   5 [a11y] › ../../tests/e2e/pilot-operations/accessibility.a11y.ts:79:5 › OPS-UX the refused operations have no accessibility violation (760ms)
  ✓   6 [a11y] › ../../tests/e2e/pilot-operations/accessibility.a11y.ts:91:5 › OPS-UX the public status page has no accessibility violation (583ms)
  ✓   7 [a11y] › ../../tests/e2e/pilot-operations/accessibility.a11y.ts:103:5 › OPS-UX the public status page has no accessibility violation in the dark theme (586ms)
  ✓   8 [a11y] › ../../tests/e2e/pilot-operations/accessibility.a11y.ts:117:5 › OPS-UX the public status page has no accessibility violation at 360 pixels (553ms)

  8 passed (7.1s)
```

The exit code is 0. The runs used the tags `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`, `wcag22aa` and `best-practice`.

## 7. The planning verification

```
python3 scripts/verify_bootstrap.py
```

```
PASS planning artifacts: 12 changes, 37 requirements, 111 scenarios, 9 unchecked tasks; token pin and 12 contrast pairs verified
Product/runtime/browser/deployment verification: NOT PERFORMED
```

```
node scripts/check-openspec-change-names.mjs
```

```
OpenSpec change names: PASS
```

`openspec validate --all --strict --no-interactive` did not run: the command `openspec` is not on the path of the machine of the worker. The two planning scripts above ran and passed.

## 8. The checks that did not run

| Check | Why |
|---|---|
| The journeys in Firefox and in WebKit | Only Chromium is installed on the machine of the worker. The release smoke of the product still has to run them. |
| `openspec validate --all --strict --no-interactive` | The command `openspec` is not on the path of the machine of the worker. |
| The upgrade of a database that holds the rows of the earlier packages | The migration `AddPilotOperations` only creates the three tables of the package and changes no earlier table. The fresh database of the run was migrated from zero, and the tests of the package migrate the isolated database of the test run from zero as well. |
| The deployment of the production compose file | The compose file is configuration and nothing of it was deployed. The product never deploys itself. |
| The release smoke | Not part of this change. |

## 9. The decisions of the run that a reader of the contract should know

| Decision | What was done and why |
|---|---|
| The numbers of the browser journeys and of the runs of axe | The operator enters the two numbers of the current pilot run in the form of the restore rehearsal, and they are kept with the rehearsal. The gate reports the numbers of the latest rehearsal as they are. The design named the numbers as operator input that the gate reads from the database; the rehearsal row is the record of the pilot run, so no fourth table was added to the package. |
| The endpoints of the package | The design named five endpoints. The run added two more, because the application service of the design holds `RecordBackupAsync` and a deletion request has to be readable for the operator: `POST /v1/admin/operations/backups` and `GET /v1/admin/operations/deletions`. The public status `GET /v1/status` was named by the design as well. |
| The comparison of the row counts | The rehearsal reads the row counts of the live database before the dump and after the restore. A table whose count changed between the two reads was written while the rehearsal ran, so it is not compared and it is named in the notes of the rehearsal. Every other table has to match exactly, or the data check fails. |
| The object inventory | The inventory is the number of the objects the database refers to and the checksum of their sorted keys. The restored database and the live database are read with the same reader, so both sides of the comparison are measured the same way. |
| The closing of an account | The account of a deleted subject keeps the pseudonym, its profile becomes private, its provider identity is removed and its sessions are revoked. The rows of the ledger and the rows of the audit keep the identifier of the account, and the account keeps only the pseudonym. The table of the users was not changed by this package. |
| The path of the client of PostgreSQL | The client of the host must have the same major version as the server. The paths of the client are configuration, so the operator may point the product at the client of the host. The image of the API of the repository does not install the client yet; the runbook of the deployment names the requirement. |
| The gate of the pilot is never ready in this run | The items of the p95 of the latency and of the availability have no observation window yet, so they stay a target and they block the promotion. That is the honest state of the pilot and the gate says so. |
