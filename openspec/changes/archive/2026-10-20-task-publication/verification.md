# Verification: TASK

Status: RUN — the task publication package is implemented, built and verified. Every command below was executed against the real API, the isolated PostgreSQL 17 of the test run and the compose stack. The evidence is the observed output, not a plan.

## Commands and observed result

1. `dotnet restore Blindmesh.sln --locked-mode`: PASS. Six projects restore from the committed `packages.lock.json` files. Every restore prints `warning NU1900` for the public platform feed, because that feed is unreachable. The warning is the visible evidence of the blocked platform adoption and was not hidden.
2. `dotnet format Blindmesh.sln --verify-no-changes`: PASS, exit code 0. No file needs formatting.
3. `dotnet build Blindmesh.sln -c Release --no-restore`: PASS. Five product projects and one test project, 0 errors, 5 warnings (the `NU1900` warnings of the unreachable feed).
4. `dotnet test Blindmesh.sln -c Release --no-build`: PASS. 85 tests, 0 failed, against an isolated PostgreSQL 17 container started by Testcontainers. 73 of them are the tests of the earlier packages and 12 are the tests of this package: `TASK1SuccessAPublicationReservesTheWholeBudgetAndOpensTheTask`, `TASK1RejectionATaskWithoutTheRequiredStepsIsRefusedAndLeavesNoReservation`, `TASK1RejectionAPublisherWithoutTheCreditsIsRefusedAndLeavesNoReservation`, `TASK1BoundaryARetriedPublicationReturnsTheSameTaskAndTheSameReservation`, `TASK1TheSameKeyWithAnotherTaskIsRefused`, `TASK1TheDraftOfATaskIsChangedWithTheVersionOfTheTask`, `TASK2SuccessAVisitorFiltersThePublicTasksByModeAndRequirement`, `TASK2RejectionAPrivateTaskIsNotFoundAndLeaksNothing`, `TASK2BoundaryACancellationClosesTheAdmissionAndRefundsTheUnusedSlots`, `TASK2TheEligibilityOfAPublishedTaskAnswersWithTheConditionsOfThatTask`, `TASK2TheEligibilityOfATaskThatTheActorMayNotSeeIsNotFound` and `TASKTheMigrationAddsTheTablesOfThePackageAndKeepsTheRowsOfTheEarlierPackages`.
5. `npm --prefix apps/web ci`: PASS. The lock file is committed and unchanged; the packages are installed from it.
6. `npm --prefix apps/web run lint`, `run typecheck`, `run build`: PASS. No warning, no error, Next.js 16.4.2. The build lists the new routes `/tasks`, `/tasks/new`, `/tasks/[id]` and `/projects/[id]/tasks`.
7. `npm --prefix apps/web run api:check`: PASS. The committed `packages/api-client/openapi.json` and `src/generated/schema.ts` match a fresh generation, byte for byte. The document owns the five task paths; the local OAuth fixture is not part of the published contract.
8. `docker compose -f infra/compose.dev.yaml up -d --build`: PASS. PostgreSQL 17.11, the object storage, the mail sink, the API, the worker, the web application and the Caddy proxy start with the code of this package. `dotnet run --project src/Blindmesh.Api -- migrate` applied `20261020091645_AddTaskPublication` to the schema of the previous package: the rows of the users, the projects, the capabilities and the ledger stayed in the database and the tables `tasks`, `task_requirements`, `task_steps`, `task_slots` and `task_history` were added with their 9 indexes and their foreign keys. The fresh schema of the test run applies the whole stream without a gap, so both the fresh schema and the upgrade are covered. `TASKTheMigrationAddsTheTablesOfThePackageAndKeepsTheRowsOfTheEarlierPackages` walks the same upgrade in a second database of the test container.
9. `npm --prefix apps/web run test:e2e -- --project=chromium --grep TASK`: PASS. 13 browser journeys of this package and 1 journey of the capability package whose title holds the word "task" (`CAP-UX the eligibility route shows the honest state of a task that is not published`), 14 of 14. The sign in needs the local OAuth fixture, so the API runs in the test mode and the web application points at it. Every group of journeys ran against its own fresh database, so no journey of an earlier package inherits the data of another one:

   ```sh
   # one group of journeys, for example TASK
   docker exec blindmesh-postgres-1 psql -U blindmesh -d postgres -c "CREATE DATABASE bm_task;"
   ASPNETCORE_URLS=http://127.0.0.1:58090 APP_ENV=test \
     Blindmesh__Identity__LocalFixtureEnabled=true \
     Blindmesh__Identity__PublicBaseUrl=http://127.0.0.1:53100 \
     Blindmesh__Identity__CookieSecurity=Development \
     Api__AllowedOrigins__0=http://127.0.0.1:53100 \
     Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=bm_task;Username=blindmesh;Password=blindmesh" \
     dotnet run --project src/Blindmesh.Api --no-launch-profile -- migrate
   ASPNETCORE_URLS=http://127.0.0.1:58090 APP_ENV=test \
     Blindmesh__Identity__LocalFixtureEnabled=true \
     Blindmesh__Identity__PublicBaseUrl=http://127.0.0.1:53100 \
     Blindmesh__Identity__CookieSecurity=Development \
     Api__AllowedOrigins__0=http://127.0.0.1:53100 \
     Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=bm_task;Username=blindmesh;Password=blindmesh" \
     dotnet run --project src/Blindmesh.Api --no-launch-profile &
   BLINDMESH_API_URL=http://127.0.0.1:58090 npm --prefix apps/web run start -- -p 53100 &
   BLINDMESH_WEB_URL=http://127.0.0.1:53100 npm --prefix apps/web run test:e2e -- --project=chromium --grep TASK
   ```

   The journeys of the earlier packages still pass against the same hosts with `--grep IDENT` (12 of 12), `--grep PROJ` (15 of 15), `--grep CAP` (12 of 12) and `--grep CRED` (8 of 8). The PROJ group holds 13 journeys of the project package and 2 journeys of this package, because their titles hold the word "project".
10. `npm --prefix apps/web run test:a11y -- --grep TASK`: PASS. 7 axe runs, no violation in the light theme, in the dark theme, at 360 pixels, on the list, on the form, on the detail and on the eligibility page. The axe runs of the earlier packages pass as well: 6 of 6 for IDENT, 6 of 6 for PROJ, 5 of 5 for CAP and 5 of 5 for CRED.
11. `npm --prefix apps/web run test:e2e -- --project=chromium --grep FOUND` and `npm --prefix apps/web run test:a11y -- --grep FOUND` against the rebuilt compose stack through its proxy (`BLINDMESH_WEB_URL=http://127.0.0.1:58080`): PASS. 8 journeys of 8 and 4 axe runs of 4. The journeys need the object storage and the mail sink, so they run against the compose stack and not against the local test API.
12. `python3 scripts/verify_bootstrap.py`: PASS. 12 changes, 37 requirements, 111 scenarios, the shared token SHA256 and the 12 contrast pairs are verified.
13. `node scripts/check-openspec-change-names.mjs`: PASS.
14. `openspec validate --all --strict --no-interactive`: 12 passed, 0 failed.

## Scenario evidence

| Scenario | What the run proved | Test |
|---|---|---|
| TASK-1 success | A publication reserves the whole budget `(base + bonus) * slots` of the task, writes the slots, opens the task and keeps the journal of the credits balanced. | `TASK1SuccessAPublicationReservesTheWholeBudgetAndOpensTheTask` |
| TASK-1 rejection | A task without the required steps of its mode is refused with 422, stays a draft and leaves no entry in the journal. | `TASK1RejectionATaskWithoutTheRequiredStepsIsRefusedAndLeavesNoReservation` |
| TASK-1 rejection | A publisher without the credits is refused with 422 `insufficient_funds`, the task stays a draft and leaves no reservation and no slot. | `TASK1RejectionAPublisherWithoutTheCreditsIsRefusedAndLeavesNoReservation` |
| TASK-1 boundary | A publication that is retried with the same key after a lost answer returns the same task and the same reservation, and the journal holds one reserve transaction only. | `TASK1BoundaryARetriedPublicationReturnsTheSameTaskAndTheSameReservation` |
| TASK-1 boundary | The same key with another task is refused with 409 `idempotency_key_reuse`. | `TASK1TheSameKeyWithAnotherTaskIsRefused` |
| TASK-1 boundary | A draft is changed with the version of the task, so a stale write is refused. | `TASK1TheDraftOfATaskIsChangedWithTheVersionOfTheTask` |
| TASK-2 success | A visitor sees the public feed, filters it by the mode and by the hard requirement, pages it with a stable cursor and no candidate summary holds the scope of the task. | `TASK2SuccessAVisitorFiltersThePublicTasksByModeAndRequirement` |
| TASK-2 rejection | A private task answers 404 and leaks neither the project, nor the contributor, nor the task. | `TASK2RejectionAPrivateTaskIsNotFoundAndLeaksNothing` |
| TASK-2 boundary | A cancellation closes the admission, refunds the unused slots, keeps the budget of the running slot and leaves the journal balanced. | `TASK2BoundaryACancellationClosesTheAdmissionAndRefundsTheUnusedSlots` |
| TASK-2 boundary | The eligibility of a published task answers with the conditions of that task, and the task that the actor may not see answers 404. | `TASK2TheEligibilityOfAPublishedTaskAnswersWithTheConditionsOfThatTask`, `TASK2TheEligibilityOfATaskThatTheActorMayNotSeeIsNotFound` |
| TASK-2 boundary | The migration of the package adds its five tables and keeps the rows of the users, the projects, the capabilities and the ledger of the earlier packages. | `TASKTheMigrationAddsTheTablesOfThePackageAndKeepsTheRowsOfTheEarlierPackages` |
| TASK-UX complete journey | The browser signs in through the local fixture, writes a task, publishes it, sees the open state and the reserved budget, lists the marketplace, narrows it with the filters and reads the conditions of the task on the eligibility route. | `journey.spec.ts`: the publisher writes a task and publishes it, the marketplace lists the task and the filters narrow the list, the eligibility of a real task answers with the conditions of that task, the tasks of a project are listed on their own route |
| TASK-UX recover from failure | The empty state of a project without a task, the refusal of the form for a visitor without a session, the marked invalid field that takes the focus, the safe page of a task that may not be seen, the confirmation of a cancellation and the failure state with its retry are all shown. | `journey.spec.ts`: the project without a task explains the next action, the create form refuses a visitor without a session, an invalid field is marked and takes the focus, a task that may not be seen shows the safe page, the cancellation of a task needs a confirmation, the failure state explains the failure and offers a retry |
| TASK-UX responsive keyboard operation | The four routes run at 360, 768 and 1440 pixels without a horizontal scroll, the whole journey works with the keyboard only, the dark theme renders the same journey and the seven axe runs report no violation. | `journey.spec.ts`: the journey has no horizontal overflow at 360, 768 and 1440 pixels, the journey works with the keyboard only, the journey works in the dark theme; `accessibility.a11y.ts` (7 runs) |

## The decisions of this package

- The Tasks module owns `tasks`, `task_requirements`, `task_steps`, `task_slots` and `task_history`. A task is a draft until it is published, and the state of a task, the state of a slot and the state of a participation are separate state machines.
- The publication is an escrow. The publish command reserves the whole budget of the task in the bucket of the publisher through `ICreditService.ReserveTaskAsync` in the transaction of the caller. The Tasks module never writes a ledger row: the Credits module stays the only writer of the journal. The cancellation refunds the unused slots through `ICreditService.RefundUnstartedAsync`.
- The visibility of a task is the visibility of the project that holds it, with the mode of the task on top. A private task is visible to the owner and to the members of the project, a matched task waits for the matching of the marketplace and shows a summary only, and a public task is open to everybody. The scope of a task and the resource of an access task never leave the boundary of the task that may not be seen.
- The hard conditions of a task reach the capability module through the seam `ITaskConditionReader`. The task publication package registers `PublishedTaskConditionReader`, so `GET /v1/capabilities/tasks/{task_id}/eligibility` now answers the evaluation of the conditions of a real task instead of the honest 404 of the earlier package.
- The money commands use a scoped `Idempotency-Key` record written in the same transaction, the same rule as the credit package. The same key with the same payload replays the original answer; the same key with another payload answers a conflict.
- A published task needs at least one hard requirement, so the marketplace can match a contributor against it. The rule lives in `TaskRules.ValidatePublication` and the form of the interface applies it.

## The defects the integrator found and fixed

- The journey of the task list of a project read the project and the task of the first journey of the file, so it only held when the whole file ran. The journey writes its own project and its own task now, and it holds when it runs alone. The filter `--grep PROJ` picks this journey up, because its title holds the word "project".
- The journey of the credit package counted the claim button of the grant before the wallet had loaded, so on a cold database it took the branch of an already claimed grant and failed. The journey waits for the panel of the grant now. The journey of the history of the wallet had the same race with the empty state, and it waits for one of the two states now. The journey of the task package waits for the same panel.
- The implementer run of the whole suite in one command failed the journey of the empty project list, because the shared development database already held a public project of an earlier run. The verification of every package runs against its own fresh database, so the journeys of a package never inherit the data of another one.

## Fixtures and limits

- The local OAuth fixture is the only sign in provider of this run. It is enabled with `APP_ENV=test` and `Blindmesh:Identity:LocalFixtureEnabled`, and a host that asks for it in another environment refuses to start. The seeded accounts are alice (owner), bob (eligible contributor), cara (ineligible), dana (moderator) and erin (administrator).
- The browser journey needs a plain HTTP host, so the session uses the explicitly non-`__Host` development cookie and the web address of the browser is listed in `Api:AllowedOrigins`. The production cookie mode stays the default.
- The compose stack accepts the origin of its proxy only, so the journeys that run against the stack use the address of the proxy (`http://127.0.0.1:58080`) and not the address of the web container.
- The release smoke on Firefox and WebKit did not run. Only the Chromium browser is installed in this environment.
- The `forge gate .` command did not run: the Forge binary is not on PATH in this environment. The native checks above are the evidence.

## Independent re-run by the integrator

The integrator repeated the whole command set against the working tree of this package after the implementer finished, found the three defects listed above, fixed them and repeated the journeys. The results were: 85 xUnit tests passed, the .NET chain was clean, the web chain (lint, typecheck, build, `api:check`) passed, the browser journeys of the identity, the project, the capability, the credit and the task package passed 69 of 69 runs, the axe runs of the five packages passed 33 of 33, and the FOUND journeys passed 8 of 8 with 4 of 4 axe runs against the rebuilt compose stack.
