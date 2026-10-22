# Verification

The verification of the package `scoped-matching`. Every command below was run on the machine of the worker, in the repository `/home/paul/code/blindmesh`, on the branch `main`, on the 22nd of October 2026. The outputs are the observed ones. A check that did not run is named as such and is never reported as a pass.

The backend half and the interface half of the package were written by the worker of this session, the tests, the journeys, the runs of axe, the traceability and this record belong to the same run.

## The environment of the run

| Item | Value |
|---|---|
| The API of the worker | `http://127.0.0.1:58094`, started with `APP_ENV=test` and the local OAuth fixture |
| The web application of the worker | `http://127.0.0.1:53104`, started with `BLINDMESH_API_URL=http://127.0.0.1:58094` |
| The worker of the product | `dotnet run --project src/Blindmesh.Worker --no-launch-profile`, the same database |
| The database of the journeys | the container `blindmesh-postgres-1` on the port `55432`, the database `bm_match` |
| The object storage | the container `blindmesh-object-storage-1` on the port `59000` |
| The mail sink | the container `blindmesh-mail-1` on the port `51025` |
| The browsers | Chromium only (`chromium-1248`, `chromium_headless_shell-1248`); Firefox and WebKit are not installed |
| Node | 22.23.3 |
| npm | 10.9.9 |
| .NET SDK | 10.0.400 |
| Playwright | 1.64.1 |

The database of the run was created and migrated before the journeys:

```
docker exec blindmesh-postgres-1 psql -U blindmesh -d postgres -c 'CREATE DATABASE "bm_match";'
dotnet run --project src/Blindmesh.Api --no-launch-profile -- migrate
```

The output of the migration:

```
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
  Blindmesh__ObjectStorage__Bucket=blindmesh-match \
  Blindmesh__Mail__Host=127.0.0.1 \
  Blindmesh__Mail__Port=51025 \
  Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=bm_match;Username=blindmesh;Password=blindmesh" \
  dotnet run --project src/Blindmesh.Api --no-launch-profile
```

The worker of the run:

```
Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=bm_match;Username=blindmesh;Password=blindmesh" \
  dotnet run --project src/Blindmesh.Worker --no-launch-profile
```

The package writes no row of the ledger and no event of the outbox, so the journeys of the package do not depend on the worker; the worker of the session ran until the runtime limit of the background task ended it, and the sweep of the parent runs its own worker.

The web application of the run:

```
BLINDMESH_API_URL=http://127.0.0.1:58094 npm --prefix apps/web run start -- -p 53104
```

The state of the API after the start:

```
$ curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:58094/v1/health/live
200
$ curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:58094/v1/health/ready
200
```

The database of the journeys was dropped and created again before the run, so the journeys of the package started on an empty marketplace.

## 1. The .NET chain

### `dotnet restore Blindmesh.sln --locked-mode`

```
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Api/Blindmesh.Api.csproj (用时 13.85 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Application/Blindmesh.Application.csproj (用时 13.83 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Domain/Blindmesh.Domain.csproj (用时 82 毫秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Infrastructure/Blindmesh.Infrastructure.csproj (用时 13.85 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Worker/Blindmesh.Worker.csproj (用时 13.83 秒)。
  已还原 /home/paul/code/blindmesh/tests/Blindmesh.Tests/Blindmesh.Tests.csproj (用时 13.84 秒)。
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

已用时间 00:00:09.06
```

The exit code is 0. The five warnings are the `NU1900` of the unreachable platform feed, one per project of the solution.

### `dotnet test Blindmesh.sln -c Release --no-build`

```
已通过! - 失败:     0，通过:   148，已跳过:     0，总计:   148，持续时间: 1 m 34 s - Blindmesh.Tests.dll (net10.0)
```

The exit code is 0. The 148 tests hold the 139 tests of the earlier packages and the 9 tests of this package.

## 2. The tests of the package

```
dotnet test Blindmesh.sln -c Release --no-build --filter "FullyQualifiedName~Blindmesh.Tests.Matching" --logger "console;verbosity=normal"
```

```
  已通过 Blindmesh.Tests.Matching.MATCH3FairnessTests.MATCH3BoundaryTheAnswerCarriesNoScoreAndTheOrderIsStable [1 s]
  已通过 Blindmesh.Tests.Matching.MATCH3FairnessTests.MATCH3RejectionTheAccountThatDoesNotFitIsNeverListed [238 ms]
  已通过 Blindmesh.Tests.Matching.MATCH3FairnessTests.MATCH3SuccessTheAccountWithoutAnAcceptedContributionComesFirstAndTheReasonIsNamed [218 ms]
  已通过 Blindmesh.Tests.Matching.MATCH2CandidateTests.MATCH2SuccessTheOwnerReadsTheCandidatesWithTheConditionThatEachDeclarationSatisfied [213 ms]
  已通过 Blindmesh.Tests.Matching.MATCH2CandidateTests.MATCH2RejectionTheAccountOutsideTheProjectLearnsNothing [199 ms]
  已通过 Blindmesh.Tests.Matching.MATCH2CandidateTests.MATCH2BoundaryTheWithdrawnOrPrivateDeclarationLeavesTheListAndNothingPrivateIsShown [298 ms]
  已通过 Blindmesh.Tests.Matching.MATCH1ReachTests.MATCH1RejectionTheCandidateThatDoesNotFitNeverSeesTheTask [289 ms]
  已通过 Blindmesh.Tests.Matching.MATCH1ReachTests.MATCH1BoundaryTheReachEndsWithTheScopeAndKeepsTheTakenSeat [420 ms]
  已通过 Blindmesh.Tests.Matching.MATCH1ReachTests.MATCH1SuccessTheCandidateThatFitsTheConditionsSeesTheTaskOfTheMatchedProject [239 ms]

已通过! - 失败:     0，通过:     9，已跳过:     0，总计:     9，持续时间: 4 s - Blindmesh.Tests.dll (net10.0)
```

The exit code is 0. The tests drive the real API against the isolated PostgreSQL of the test run, they sign in through the local OAuth fixture, they write through the real endpoints and they read the database directly to assert the seat that was taken and the score of the reputation.

## 3. The chain of the web application

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

The build was run before the sweep of the browser, on the same source of the web application. The excerpt of the routes of the package:

```
├ ƒ /tasks/[id]
├ ƒ /tasks/[id]/apply
├ ƒ /tasks/[id]/candidates
├ ƒ /tasks/[id]/eligibility
```

The exit code is 0. The build holds the route of the package, `/tasks/[id]/candidates`.

### `npm --prefix apps/web run api:check`

```
Wrote 234998 bytes of OpenAPI to /home/paul/code/blindmesh/packages/api-client/.generated/openapi.json.
The generated API client matches a fresh generation.
```

The exit code is 0. The generated files of the client hold the route of the package, `GET /v1/tasks/{id}/candidates`, and the schema of the answer, `CandidateListView`.

`npm --prefix apps/web ci` was run by the parent in the same session and the exit code is 0. The lock file is committed and unchanged.

## 4. The browser sweep

The browser sweep of the package ran against the API and the web application of the run, with the Chromium browser. The exact commands:

```
BLINDMESH_API_URL=http://127.0.0.1:58098 BLINDMESH_WEB_URL=http://127.0.0.1:53110 \
  npm --prefix apps/web run test:e2e -- --project=chromium --grep MATCH
```

```
BLINDMESH_API_URL=http://127.0.0.1:58098 BLINDMESH_WEB_URL=http://127.0.0.1:53110 \
  npm --prefix apps/web run test:a11y -- --grep MATCH
```

The scope rule touches the list of the tasks of every package, so the parent ran the sweep of every group of every package on its own fresh database of 2026-10-22, with one build of the web application and one web server. The group of the matching package passed 14 journeys and 4 runs of axe, and the whole sweep passed 154 journeys and 81 runs of axe, with no failure and no violation:

```
FOUND e2e_exit=0 journeys_passed=8  journeys_failed=0 axe_passed=4 axe_failed=0
IDENT e2e_exit=0 journeys_passed=12 journeys_failed=0 axe_passed=6 axe_failed=0
PROJ  e2e_exit=0 journeys_passed=20 journeys_failed=0 axe_passed=8 axe_failed=0
CAP   e2e_exit=0 journeys_passed=12 journeys_failed=0 axe_passed=5 axe_failed=0
CRED  e2e_exit=0 journeys_passed=8  journeys_failed=0 axe_passed=5 axe_failed=0
TASK  e2e_exit=0 journeys_passed=18 journeys_failed=0 axe_passed=10 axe_failed=0
PART  e2e_exit=0 journeys_passed=11 journeys_failed=0 axe_passed=6 axe_failed=0
EVID  e2e_exit=0 journeys_passed=10 journeys_failed=0 axe_passed=4 axe_failed=0
MOD   e2e_exit=0 journeys_passed=13 journeys_failed=0 axe_passed=8 axe_failed=0
REP   e2e_exit=0 journeys_passed=10 journeys_failed=0 axe_passed=9 axe_failed=0
NOTIF e2e_exit=0 journeys_passed=7  journeys_failed=0 axe_passed=4 axe_failed=0
OPS   e2e_exit=0 journeys_passed=11 journeys_failed=0 axe_passed=8 axe_failed=0
MATCH e2e_exit=0 journeys_passed=14 journeys_failed=0 axe_passed=4 axe_failed=0
```

The group of the task publication ran in the same sweep, with the same rule:

```
BLINDMESH_API_URL=http://127.0.0.1:58098 BLINDMESH_WEB_URL=http://127.0.0.1:53110 \
  npm --prefix apps/web run test:e2e -- --project=chromium --grep TASK
```

```
BLINDMESH_API_URL=http://127.0.0.1:58098 BLINDMESH_WEB_URL=http://127.0.0.1:53110 \
  npm --prefix apps/web run test:a11y -- --grep TASK
```

The sweep found one defect of the composition of the worker, and the defect is fixed: the worker did not register the module of the matching, so the claim cycle of the outbox could not resolve the rule of the scope and failed every cycle, and the projection of the reputation never ran. The worker registers the module now, and the groups of the reputation and of the notifications pass.

## The limits of the run

- The browser journeys ran with the Chromium browser only, because the Firefox and the WebKit runtimes are not installed on this machine. The two other browsers are therefore not proven by this run.
- The soft weights of the PRD §5.2 stay unimplemented, because the PRD calls them hypotheses that must be calibrated with real data and must not be used as a quality guarantee. The order of the list is a rule of exploration with a readable reason, and it is not a quality claim.
- The push of a matched task is not implemented. The account that the list names is not told that it was listed; a push belongs to the notification package.
- The kinds of a hard condition stay the three of the capability module: region, device and role. The package owns no new vocabulary of a condition.
- The package owns no row and no table. The candidate list is read from the declarations of the capability module, from the conditions of the task module and from the accepted contributions of the reputation module, so it holds no copy of the state of another module.
- The rule of the scope is applied by one method of the matching module. The list of the tasks, the detail of a task, the eligibility, the seat of a task, the detail of a project and the list of the projects ask that method, so a task that the list of the marketplace does not show is never readable through another route.
