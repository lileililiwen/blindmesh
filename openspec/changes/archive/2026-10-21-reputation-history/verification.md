# Verification

The verification of the package `reputation-history`. Every command below was run on the machine of the worker, in the repository `/home/paul/code/blindmesh`, on the branch `main`, on the 22nd of October 2026. The outputs are the observed ones. A check that did not run is named as such and is never reported as a pass.

The backend half and the interface half of the package were written before this run and were reviewed, not rewritten. This run added the tests of the package, the journeys of the browser, the runs of axe, the traceability and this record, and it fixed the defects the run found.

## The environment of the run

| Item | Value |
|---|---|
| The API of the worker | `http://127.0.0.1:58096`, started with `APP_ENV=test` and the local OAuth fixture |
| The web application of the worker | `http://127.0.0.1:53106`, started with `BLINDMESH_API_URL=http://127.0.0.1:58096` |
| The worker of the product | `dotnet run --project src/Blindmesh.Worker --no-launch-profile`, the same database |
| The database of the journeys | the container `blindmesh-postgres-1` on the port `55432`, the database `bm_rep` |
| The object storage | the container `blindmesh-object-storage-1` on the port `59000` |
| The mail sink | the container `blindmesh-mail-1` on the port `51025` |
| The browsers | Chromium only |
| Node | 22.23.3 |
| npm | 10.9.9 |
| .NET SDK | 10.0.400 |
| Playwright | 1.64.1 |

The database of the run was created and migrated before the journeys:

```
docker exec blindmesh-postgres-1 psql -U blindmesh -d postgres -c 'CREATE DATABASE "bm_rep";'
dotnet run --project src/Blindmesh.Api --no-launch-profile -- migrate
```

The output of the migration:

```
info: Blindmesh.Infrastructure.Persistence.DatabaseMigrator[0]
      Applying migration '20261021154542_AddReputationHistory'.
info: Blindmesh.Infrastructure.Persistence.DatabaseMigrator[0]
      Database schema is up to date.
```

The name of the database is written between quotes, because an unquoted name is folded to lower case and the API would read another database.

The API of the run:

```
ASPNETCORE_URLS=http://127.0.0.1:58096 APP_ENV=test \
  Blindmesh__Identity__LocalFixtureEnabled=true \
  Blindmesh__Identity__PublicBaseUrl=http://127.0.0.1:53106 \
  Blindmesh__Identity__CookieSecurity=Development \
  Api__AllowedOrigins__0=http://127.0.0.1:53106 \
  Blindmesh__ObjectStorage__Endpoint=http://127.0.0.1:59000 \
  Blindmesh__ObjectStorage__PublicEndpoint=http://127.0.0.1:59000 \
  Blindmesh__ObjectStorage__AccessKey=blindmesh \
  Blindmesh__ObjectStorage__SecretKey=blindmesh-secret \
  Blindmesh__ObjectStorage__Bucket=blindmesh-reputation \
  Blindmesh__Mail__Host=127.0.0.1 \
  Blindmesh__Mail__Port=51025 \
  Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=bm_rep;Username=blindmesh;Password=blindmesh" \
  dotnet run --project src/Blindmesh.Api --no-launch-profile
```

The worker of the run:

```
Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=bm_rep;Username=blindmesh;Password=blindmesh" \
  dotnet run --project src/Blindmesh.Worker --no-launch-profile
```

The web application of the run:

```
BLINDMESH_API_URL=http://127.0.0.1:58096 npm --prefix apps/web run start -- -p 53106
```

The state of the API after the start:

```
$ curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:58096/v1/health/live
200
$ curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:58096/v1/health/ready
200
```

## 1. The .NET chain

### `dotnet restore Blindmesh.sln --locked-mode`

```
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Api/Blindmesh.Api.csproj (用时 12.73 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Application/Blindmesh.Application.csproj (用时 12.73 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Domain/Blindmesh.Domain.csproj (用时 12.73 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Infrastructure/Blindmesh.Infrastructure.csproj (用时 12.73 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Worker/Blindmesh.Worker.csproj (用时 12.73 秒)。
  已还原 /home/paul/code/blindmesh/tests/Blindmesh.Tests/Blindmesh.Tests.csproj (用时 12.73 秒)。
```

The exit code is 0. The warning `NU1900` of the unreachable platform feed is expected on every restore and is left visible.

### `dotnet format Blindmesh.sln --verify-no-changes`

```
加载工作区时遇到警告。要记录警告，请将“详细级别”选项设置为“诊断”级别。
```

The exit code is 0. The run did not ask for a change of any file.

### `dotnet build Blindmesh.sln -c Release --no-restore`

```
已成功生成。
    5 个警告
    0 个错误
```

The exit code is 0. The five warnings are the `NU1900` of the unreachable platform feed, one per project of the solution.

### `dotnet test Blindmesh.sln -c Release --no-build`

```
已通过! - 失败:     0，通过:   126，已跳过:     0，总计:   126，持续时间: 51 s - Blindmesh.Tests.dll (net10.0)
```

The exit code is 0. The 126 tests hold the 120 tests of the earlier packages and the 6 tests of this package.

## 2. The tests of the package

```
dotnet test Blindmesh.sln -c Release --no-build --filter "FullyQualifiedName~Blindmesh.Tests.Reputation"
```

```
  已通过 Blindmesh.Tests.Reputation.REP2PrivacyTests.REP2RejectionThePublicRequestRevealsNoPrivateField [2 s]
  已通过 Blindmesh.Tests.Reputation.REP2PrivacyTests.REP2SuccessThePublicHistoryShowsOnlyThePermittedReferences [2 s]
  已通过 Blindmesh.Tests.Reputation.REP2PrivacyTests.REP2BoundaryASourceThatBecamePrivateDisappearsFromThePublicHistory [2 s]
  已通过 Blindmesh.Tests.Reputation.REP1ProvenanceTests.REP1BoundaryTheReversalCorrectsTheProjectionAndKeepsTheHistory [2 s]
  已通过 Blindmesh.Tests.Reputation.REP1ProvenanceTests.REP1SuccessTheAcceptedContributionIsCountedOnceAndTheProfileExplainsItsCategory [2 s]
  已通过 Blindmesh.Tests.Reputation.REP1ProvenanceTests.REP1RejectionTheSameEventDeliveredTwiceChangesNothing [4 s]

已通过! - 失败:     0，通过:     6，已跳过:     0，总计:     6，持续时间: 16 s - Blindmesh.Tests.dll (net10.0)
```

The exit code is 0. The tests drive the real API against the isolated PostgreSQL of the test run, they sign in through the local OAuth fixture and they read the database directly to assert the stored provenance. The three tests of REP-1 run the seat through the real endpoints, the consumer of the outbox of the worker applies the event and the test waits for the line of the reputation.

## 3. The chain of the web application

### `npm --prefix apps/web ci`

```
added 397 packages in 4s
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
✓ Compiled successfully in 756ms
Route (app)
├ ƒ /people/[username]
├ ƒ /people/[username]/history
├ ƒ /projects/[id]/settings
├ ○ /settings/capabilities
├ ○ /settings/profile
├ ○ /settings/reputation
```

The exit code is 0. The build holds the two routes of the package, `/people/[username]/history` and `/settings/reputation`.

### `npm --prefix apps/web run api:check`

```
The generated API client matches a fresh generation.
```

The exit code is 0.

The type check of the journeys of the browser is not part of the chain of the web application. It was run anyway:

```
cd apps/web && npx tsc --noEmit -p ../../tests/e2e/tsconfig.json
```

The two files of this package hold no error of type. The files of the earlier packages hold the same error of type as before this run (`runSeat(page, stamp)` receives a number where a string is expected); the error is not part of the chain and was not touched, because it belongs to the packages that came before.

## 4. The journeys of the browser

The database of the run was dropped, created and migrated again before the journeys, so the journeys never inherit the state of a run before them:

```
docker exec blindmesh-postgres-1 psql -U blindmesh -d postgres -c 'DROP DATABASE IF EXISTS "bm_rep";'
docker exec blindmesh-postgres-1 psql -U blindmesh -d postgres -c 'CREATE DATABASE "bm_rep";'
dotnet run --project src/Blindmesh.Api --no-launch-profile -- migrate
```

```
BLINDMESH_API_URL=http://127.0.0.1:58096 BLINDMESH_WEB_URL=http://127.0.0.1:53106 \
  npm --prefix apps/web run test:e2e -- --project=chromium --grep REP
```

```
  ✓   1 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:532:5 › MOD-UX the reporter reads the receipt of the report and the staff read the report (7.7s)
  ✓   2 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:580:5 › MOD-UX a report with a short reason is refused and the draft stays (436ms)
  ✓   3 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:703:5 › MOD-UX a banned account may not write a report (753ms)
  ✓   4 [chromium] › ../../tests/e2e/reputation-history/journey.spec.ts:264:5 › REP-UX the accepted contribution of a seat appears in the history of the contributor (34.5s)
  ✓   5 [chromium] › ../../tests/e2e/reputation-history/journey.spec.ts:289:5 › REP-UX a source that becomes private disappears from the public history (37.0s)
  ✓   6 [chromium] › ../../tests/e2e/reputation-history/journey.spec.ts:321:5 › REP-UX the history of a contributor without a contribution explains the next action (771ms)
  ✓   7 [chromium] › ../../tests/e2e/reputation-history/journey.spec.ts:342:5 › REP-UX an unavailable API explains the failure and offers a retry (684ms)
  ✓   8 [chromium] › ../../tests/e2e/reputation-history/journey.spec.ts:364:5 › REP-UX the pages of the reputation have no horizontal overflow at 360, 768 and 1440 pixels (35.6s)
  ✓   9 [chromium] › ../../tests/e2e/reputation-history/journey.spec.ts:397:5 › REP-UX the journey of the reputation works with the keyboard only (726ms)
  ✓  10 [chromium] › ../../tests/e2e/reputation-history/journey.spec.ts:417:5 › REP-UX the journey of the reputation works in the dark theme (601ms)

  10 passed (2.0m)
```

The exit code is 0. The seven journeys of this package are the journeys 4 to 10. The filter `REP` also matches the three journeys of the moderation package, because their titles hold the word `report`; they passed as well and they are named here so the count is not read as ten journeys of this package.

The state of the database after the journeys, read directly:

```
$ docker exec blindmesh-postgres-1 psql -U blindmesh -d bm_rep -c \
  "SELECT u.username, r.category, r.delta, r.note, p.name AS project FROM reputation_events r JOIN users u ON u.id = r.user_id LEFT JOIN projects p ON p.id = r.source_project_id ORDER BY r.occurred_at;"

 username |       category       | delta |         note          |         project
----------+----------------------+-------+-----------------------+--------------------------
 bob      | AcceptedContribution |     1 | Accepted contribution | Reputation 1792620479143
 bob      | AcceptedContribution |     1 | Accepted contribution | Reputation 1792620513691
 bob      | AcceptedContribution |     1 | Accepted contribution | Reputation 1792620552165
(3 rows)

$ docker exec blindmesh-postgres-1 psql -U blindmesh -d bm_rep -c \
  "SELECT username, current_score, accepted_contributions, reversed_contributions FROM reputation_scores s JOIN users u ON u.id = s.user_id;"

 username | current_score | accepted_contributions | reversed_contributions
----------+---------------+------------------------+-------------------------
 bob      |             3 |                      3 |                      0
(1 row)
```

The three lines are the three journeys that run a seat: the complete journey, the journey of the privacy and the journey of the layouts. The project `Reputation 1792620513691` is the project the journey of the privacy closed, and the line of it is gone from the public history of the contributor while the counts of the contributor stay correct.

The latency of the worker between the writing of an event and its application, read from the outbox of the run:

```
            written            |            applied            |     latency
-------------------------------+-------------------------------+-----------------
 2026-10-21 22:08:32.146355+00 | 2026-10-21 22:08:32.351915+00 | 00:00:00.20556
 2026-10-21 22:09:08.636515+00 | 2026-10-21 22:09:08.662982+00 | 00:00:00.026467
 2026-10-21 22:09:46.625714+00 | 2026-10-21 22:09:46.670235+00 | 00:00:00.044521
```

## 5. The runs of axe

```
BLINDMESH_API_URL=http://127.0.0.1:58096 BLINDMESH_WEB_URL=http://127.0.0.1:53106 \
  npm --prefix apps/web run test:a11y -- --grep REP
```

```
  ✓  1 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:237:5 › MOD-UX the report form has no accessibility violation (3.1s)
  ✓  2 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:250:5 › MOD-UX the report form has no accessibility violation at 360 pixels (1.1s)
  ✓  3 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:265:5 › MOD-UX the report form has no accessibility violation in the dark theme (1.2s)
  ✓  4 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:281:5 › MOD-UX the queue of the reports has no accessibility violation (1.3s)
  ✓  5 [a11y] › ../../tests/e2e/reputation-history/accessibility.a11y.ts:239:5 › REP-UX the public history of a contributor has no accessibility violation (7.6s)
  ✓  6 [a11y] › ../../tests/e2e/reputation-history/accessibility.a11y.ts:258:5 › REP-UX the public history of a contributor has no accessibility violation at 360 pixels (36.0s)
  ✓  7 [a11y] › ../../tests/e2e/reputation-history/accessibility.a11y.ts:281:5 › REP-UX the own history of the contributor has no accessibility violation (35.7s)
  ✓  8 [a11y] › ../../tests/e2e/reputation-history/accessibility.a11y.ts:300:5 › REP-UX the empty history of a contributor has no accessibility violation (931ms)
  ✓  9 [a11y] › ../../tests/e2e/reputation-history/accessibility.a11y.ts:313:5 › REP-UX the history of a contributor has no accessibility violation in the dark theme (35.6s)

  9 passed (2.1m)
```

The exit code is 0. The five runs of axe of this package are the runs 5 to 9. The four runs of the moderation package matched the same filter and passed as well. The database of the run was dropped, created and migrated again before these runs, as it was before the journeys.

## 6. The planning checks

### `python3 scripts/verify_bootstrap.py`

```
PASS planning artifacts: 12 changes, 37 requirements, 111 scenarios, 27 unchecked tasks; token pin and 12 contrast pairs verified
Product/runtime/browser/deployment verification: NOT PERFORMED
```

The exit code is 0. The second line is the honest answer of the script: the product, the runtime, the browser and the deployment are not verified by this script.

### `node scripts/check-openspec-change-names.mjs`

```
OpenSpec change names: PASS
```

The exit code is 0.

### `openspec validate --all --strict --no-interactive`

```
✓ spec/capability-directory
✓ spec/credit-ledger
✓ spec/evidence-findings
✓ spec/identity-profiles
✓ spec/moderation-arbitration
✓ change/notification-delivery
✓ spec/participation-execution
✓ change/pilot-operations
✓ spec/project-workspaces
✓ change/reputation-history
✓ spec/runtime-foundation
✓ spec/task-publication
Totals: 12 passed, 0 failed (12 items)
```

The exit code is 0. The binary `openspec` is not on the path of the machine, so the pinned version of the workflow, `npx @fission-ai/openspec@1.6.0`, ran the command.

## 7. The checks that did not run

| Check | Why |
|---|---|
| The journeys and the runs of axe of the Firefox and of the WebKit | The runtimes are not installed on this machine. Only the Chromium browser is installed. |
| The journeys and the runs of axe of the earlier packages | The chain of this run asks for the journeys of this package. The regression of the earlier packages is covered by the 126 tests of the .NET chain, not by their journeys. |
| `forge gate .` | The Forge binary is not on the path of the machine. |
| The smoke of the release of the product | The release pipeline of the product does not exist yet. |

## 8. The defects the run found and what was done

| Finding | Where | What was done |
|---|---|---|
| The endpoint of the object storage of the brief, `Blindmesh__ObjectStorage__Endpoint=59000`, is not an address the client of the object storage accepts. The log of the API holds `Value for ServiceURL is not a valid URL: 59000` and the readiness of the API answered 503 with the mail sink unreachable. | The environment of the run of the brief | The run used the values of the run of the archived package `evidence-findings`: the endpoint and the public endpoint `http://127.0.0.1:59000`, the key `blindmesh`, the secret `blindmesh-secret`, the bucket `blindmesh-reputation`, and the host `127.0.0.1` with the port `51025` for the mail sink. The API answered 200 on `/v1/health/live` and on `/v1/health/ready`. The values of the brief were not changed in the repository, because the configuration of the stack is shared with the earlier packages. |
| The acceptance of a submission needs a body. The first run of the tests answered 400 on `POST /v1/submissions/{id}/accept` without a body. | `src/Blindmesh.Api/Endpoints/Findings/FindingsEndpoints.cs` | The scenario of the tests sends `{"bonus":0}`. The endpoint was not changed. |
| A seat of a private project is impossible. The first run answered 404 on the application of a seat in a private project, because a member of a project never earns credits from it and a visitor never sees a task of a private project. | `src/Blindmesh.Application/Tasks/ParticipationService.cs` | The scenarios of the package reach a private source by closing the project of the owner after the contribution. The rejection scenario reads the public history of that closed source and the export that is refused to a visitor; the boundary scenario reads the history again after the closing. No rule of the product was weakened. |
| The account of the platform that issues the credits holds a negative balance of its own. The first run of the test of the reversal failed on the balance of the account `PlatformIssuance`. | `src/Blindmesh.Domain/Credits/LedgerAccount.cs` | The invariant of the package holds for the accounts of the contributors: the test asserts that the signed sum of every transaction of the journal is zero and that no account with an owner holds a negative balance. The account of the platform keeps its own nature. |
| The filter `REP` of the journeys matches the journeys of the moderation package, because their titles hold the word `report`. | The commands of the brief | The counts of the journeys and of the runs of axe are read as seven and five for this package, and the journeys of the other package are named in the record so the number of the run is not misread. |
| The tests of the package drive the consumer of the outbox of the worker of the product, so the test project needs the worker. | `tests/Blindmesh.Tests/Blindmesh.Tests.csproj` | The test project references `src/Blindmesh.Worker`, the project of this product that owns the handlers of the events and the cycle that claims the outbox. No NuGet package and no sibling package was added, and the lock file of the test project holds the new project reference. |
| The wait of the journeys of the browser stopped at the first line of a contribution it found on the page, not at the line of its own seat. The run of axe of a second seat therefore failed on the line of its own project, although the worker had written it a moment before. | `tests/e2e/reputation-history/journey.spec.ts`, `tests/e2e/reputation-history/accessibility.a11y.ts` | The wait reads the page again until the line of its own source is there. The journeys and the runs of axe were run again on a fresh database and passed. |

Two findings of the run concern the worker of the product and belong to the packages that came before. They are named here and were not changed.

| Finding | Where | What was done |
|---|---|---|
| The cycle of the deadlines of the reviews of the worker fails with `Unable to resolve service for type 'Blindmesh.Application.Moderation.ModerationService' while attempting to activate 'Blindmesh.Application.Findings.FindingsService'`. The worker never registered the module of the moderation. | `src/Blindmesh.Worker/Program.cs`, `src/Blindmesh.Worker/ReviewDeadlineService.cs` | The finding was left to the owner of the moderation package; the cycle of the outbox is not touched by it, and the latency between the writing of an event and its application stayed between 0.03 and 0.21 seconds. |
| The events of the moderation have no consumer, so they stay pending in the outbox, and the cycle that claims the outbox takes them again at once, because it counts the messages it claimed and not the messages it applied. | `src/Blindmesh.Worker/OutboxClaimService.cs` | The finding was left to the owner of the foundation package. The latency of the events of the reputation is not affected by it, and the two pending messages of the moderation of the run are visible in the outbox of the run. |

## 9. The honest limits of the evidence

- The package owns no command of its own. The contribution of a seat and its reversal are written by the findings and the moderation packages inside the transaction of their own command. The tests of the backend register the real handlers and the real claim cycle of the worker, so the effect of an event is produced by the code that runs in production and not by a stand in of the test. The browser journeys ran with the worker of the product next to the API, in the same way.
- The API exposes no route for one single line of the reputation and no route of export. A visitor that asks for a private source event reads the public history of the contributor, and the answer holds neither the line of the private source nor a field of it. The export of the own history is the route `GET /v1/reputation/history`, and it answers 401 to a visitor.
- The tests of the backend use a PostgreSQL container of their own, so the state of the journeys never reaches them. The journeys of the browser ran against the database `bm_rep` of the run, which was created and migrated before the journeys and was not empty before them.
- The journeys waited for the worker of the product to apply the event of the outbox. The wait reads the page again until the line of its own source is there, with a limit of thirty reads, one second apart. The worker applied the events of the run between 0.03 and 0.21 seconds after they were written.
- The evidence of the browser is the evidence of Chromium. The journeys of Firefox and of WebKit did not run, because their runtimes are not installed on this machine.
