# Verification: PART

Status: RUN — the participation execution package is implemented, built and verified. Every command below was executed against the real API, an isolated PostgreSQL 17 of the test run and the local web application. The evidence is the observed output, not a plan.

## Commands and observed result

1. `dotnet restore Blindmesh.sln --locked-mode`: PASS. Six projects restore from the committed `packages.lock.json` files. Every restore prints `warning NU1900` for the public platform feed, because that feed is unreachable. The warning is the visible evidence of the blocked platform adoption and was not hidden.
2. `dotnet format Blindmesh.sln --verify-no-changes`: PASS, exit code 0. No file needs formatting.
3. `dotnet build Blindmesh.sln -c Release --no-restore`: PASS. Five product projects and one test project, 0 errors, 5 warnings (the `NU1900` warnings of the unreachable feed).
4. `dotnet test Blindmesh.sln -c Release --no-build`: PASS. 98 tests, 0 failed, in 28 seconds, against an isolated PostgreSQL 17 container started by Testcontainers. The plain command runs the suite: the implementer found a defect of the test harness that made this command hang for ten minutes, and the integrator fixed the harness, so the suite no longer needs an environment variable. The defect and the fix are described below. 85 of the 98 tests are the tests of the earlier packages and 13 are the tests of this package: `PART1SuccessAnOwnerAcceptsAnEligibleApplicantAndTheApplicantStartsTheWork`, `PART1RejectionTwoAcceptancesOfTheLastSlotProduceExactlyOneLease`, `PART1BoundaryAMemberOfTheProjectCannotApplyToEarnFromIt`, `PART1TheWorkOfTheContributorListsTheSeatsAndHidesTheSeatsOfOthers`, `PART2SuccessTheVisibleDeadlineIsTheSmallerOfTheTaskDeadlineAndSevenDays`, `PART2SuccessTheTaskDeadlineBoundsTheVisibleDeadline`, `PART2RejectionASweepThatRacesAStartCommandProducesOneLegalTransition`, `PART2RejectionTheStartOfASeatAfterItsWindowIsRefused`, `PART2BoundaryTheCancellationOfATaskWithRunningWorkKeepsTheEscrowAndTheGrace`, `PART2BoundaryTheWithdrawalOfASeatGivesTheSlotBackToTheTask`, `PART2TheApplicationOfAContributorIsAcceptedOnlyOnce`, `PART2AnApplicationAfterTheAdmissionClosedIsRefused` and `PARTTheMigrationAddsTheTablesOfThePackageAndKeepsTheRowsOfTheEarlierPackages`.
5. `npm --prefix apps/web ci`: PASS. The lock file is committed and unchanged; the packages are installed from it.
6. `npm --prefix apps/web run lint`, `run typecheck`, `run build`: PASS. No warning, no error, Next.js 16.4.2. The build lists the new routes `/tasks/[id]/apply`, `/work` and `/work/[id]`.
7. `npm --prefix apps/web run api:check`: PASS. The committed `packages/api-client/openapi.json` and `src/generated/schema.ts` match a fresh generation, byte for byte. The document owns the seven new participation paths.
8. The migration of the package: `20261020145913_AddParticipationExecution` was applied to the isolated database of the browser journeys and to the fresh schema of the test run. It adds only the tables `participations`, `applications`, `slot_leases` and `participation_history` with their indexes and their foreign keys, and it keeps the rows of the earlier packages. `PARTTheMigrationAddsTheTablesOfThePackageAndKeepsTheRowsOfTheEarlierPackages` walks the same upgrade in a second database of the test container and proves that the exclusive lease of a slot is a partial unique index.
9. `npm --prefix apps/web run test:e2e -- --project=chromium --grep PART`: PASS. 8 browser journeys of this package, 8 of 8. The sign in needs the local OAuth fixture, so the API runs in the test mode and the web application points at it. Every group of journeys ran against its own fresh database, so no journey of an earlier package inherits the data of another one:

   ```sh
   # one group of journeys, for example PART
   docker exec blindmesh-postgres-1 psql -U blindmesh -d postgres -c "CREATE DATABASE bm_part;"
   ASPNETCORE_URLS=http://127.0.0.1:58092 APP_ENV=test \
     Blindmesh__Identity__LocalFixtureEnabled=true \
     Blindmesh__Identity__PublicBaseUrl=http://127.0.0.1:53102 \
     Blindmesh__Identity__CookieSecurity=Development \
     Api__AllowedOrigins__0=http://127.0.0.1:53102 \
     Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=bm_part;Username=blindmesh;Password=blindmesh" \
     dotnet run --project src/Blindmesh.Api --no-launch-profile -- migrate
   ASPNETCORE_URLS=http://127.0.0.1:58092 APP_ENV=test \
     Blindmesh__Identity__LocalFixtureEnabled=true \
     Blindmesh__Identity__PublicBaseUrl=http://127.0.0.1:53102 \
     Blindmesh__Identity__CookieSecurity=Development \
     Api__AllowedOrigins__0=http://127.0.0.1:53102 \
     Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=bm_part;Username=blindmesh;Password=blindmesh" \
     dotnet run --project src/Blindmesh.Api --no-launch-profile &
   BLINDMESH_API_URL=http://127.0.0.1:58092 npm --prefix apps/web run start -- -p 53102 &
   curl -s -o /dev/null http://127.0.0.1:53102/
   BLINDMESH_WEB_URL=http://127.0.0.1:53102 npm --prefix apps/web run test:e2e -- --project=chromium --grep PART
   ```

   The journeys of the earlier packages still pass against the same hosts with `--grep IDENT` (12 of 12), `--grep PROJ` (15 of 15), `--grep CAP` (12 of 12), `--grep CRED` (8 of 8) and `--grep TASK` (14 of 14). The PROJ group holds 13 journeys of the project package and 2 journeys of the task package, because their titles hold the word "project". The TASK group holds 13 journeys of the task package and 1 journey of the capability package, because its title holds the word "task". Every group runs against a database that was created for that group alone, and the name of the database is quoted, so the application and the setup script always mean the same database.
10. `npm --prefix apps/web run test:a11y -- --grep PART`: PASS. 6 axe runs, no violation in the light theme, in the dark theme, at 360 pixels, on the list of the work, on the apply page and on the seat of a contributor. The axe runs of the earlier packages pass as well: 6 of 6 for IDENT, 6 of 6 for PROJ, 5 of 5 for CAP, 5 of 5 for CRED and 9 of 9 for TASK.
11. `npm --prefix apps/web run test:e2e -- --project=chromium --grep FOUND` and `npm --prefix apps/web run test:a11y -- --grep FOUND` against the compose stack through its proxy (`BLINDMESH_WEB_URL=http://127.0.0.1:58080`): PASS. 8 journeys of 8 and 4 axe runs of 4. The journeys need the object storage and the mail sink, so they run against the compose stack. The integrator rebuilt the stack with the code of this package before the run, so the images carry this package.
12. `python3 scripts/verify_bootstrap.py`: PASS. 12 changes, 37 requirements, 111 scenarios, 54 unchecked tasks; the shared token SHA256 and the 12 contrast pairs are verified.
13. `node scripts/check-openspec-change-names.mjs`: PASS.
14. `openspec validate --all --strict --no-interactive`: 12 passed, 0 failed.

## Scenario evidence

| Scenario | Test | Evidence |
|---|---|---|
| PART-1 success | `PART1SuccessAnOwnerAcceptsAnEligibleApplicantAndTheApplicantStartsTheWork` | The application answers 201 in the state `applied` and holds no instruction; the acceptance answers 200 with the state `accepted`, the slot `leased` and a start deadline; the start answers 200 with the state `running`, the released scope, one step and the execution deadline. The journal shows 10 credits in the pending bucket of the contributor, 20 in the reserved bucket of the publisher and a balanced journal per transaction. |
| PART-1 rejection | `PART1RejectionTwoAcceptancesOfTheLastSlotProduceExactlyOneLease` | Two acceptance commands of the last slot run at the same time; exactly one answers 200 and the other answers 409 with the code `state_conflict`. The database holds exactly one lease, one live lease, one leased slot and one reservation. |
| PART-1 boundary | `PART1BoundaryAMemberOfTheProjectCannotApplyToEarnFromIt` | The owner adds bob as a member of the project of the task and bob offers the matching capability. The application answers 403 with the code `forbidden`, and the database holds no participation, no application and no lease. |
| PART-2 success | `PART2SuccessTheVisibleDeadlineIsTheSmallerOfTheTaskDeadlineAndSevenDays`, `PART2SuccessTheTaskDeadlineBoundsTheVisibleDeadline` | The first test starts a seat of a task with a deadline of thirty days and reads a visible deadline of the start plus seven days; the task carries the state `in_progress` and the history holds the transition from `open` to `in_progress`. The second test starts a seat of a task with a deadline of two days and reads exactly the deadline of the task. |
| PART-2 rejection | `PART2RejectionASweepThatRacesAStartCommandProducesOneLegalTransition`, `PART2RejectionTheStartOfASeatAfterItsWindowIsRefused` | The sweep of the deadlines expires the seat whose start window passed; the later start answers 409 with the code `invalid_transition`; the seat holds the state `expired` exactly once, the slot is `available` again, the lease is released, the reserved bucket still holds the whole budget of the task and the pending bucket is empty. |
| PART-2 boundary | `PART2BoundaryTheCancellationOfATaskWithRunningWorkKeepsTheEscrowAndTheGrace`, `PART2BoundaryTheWithdrawalOfASeatGivesTheSlotBackToTheTask` | The cancellation of a task with running work answers 200; the task carries the state `cancelled`, the seat stays `running` with its visible deadline, the running slot stays `active` and keeps its budget, the free slot is refunded, 10 credits stay in the pending bucket and 5 in the reserved bucket. The withdrawal gives the slot back to the task, releases the lease and keeps the budget reserved for the next contributor. |
| PART-UX complete journey | `PART-UX a contributor applies for a slot, the owner accepts it and the contributor starts the work` | The journey writes its own workspace and its own task, the contributor applies, the owner accepts and the contributor starts the work; the scope and the execution deadline are visible after the commit and after a reload. |
| PART-UX recover from failure | `PART-UX the consent of the scope is required before the application`, `PART-UX the apply of a member of the workspace is refused`, `PART-UX an unavailable API explains the failure and offers a retry`, `PART-UX the work of a contributor without a seat explains the next action` | The consent keeps the page and names the field; the member of the workspace reads the restriction; the unavailable API shows the explanation and the retry answers with the committed state; the empty work shows the next action. |
| PART-UX responsive keyboard operation | `PART-UX the journey of a seat has no horizontal overflow at 360, 768 and 1440 pixels`, `PART-UX the journey of a seat works with the keyboard only`, `PART-UX the journey of a seat works in the dark theme`, and the six axe runs | The pages of the package do not scroll horizontally at the three widths; the consent and the button are reachable and usable with the keyboard alone; the journey works in the dark theme; the axe runs report no violation. |

## The decisions of this package

1. **The deadlines.** A contributor has 48 hours to apply after the publication of the task and an accepted contributor has 24 hours to start. An application that the owner does not answer within 48 hours expires, and a seat that is not started within its 24 hours expires. The visible execution deadline is the smaller of the deadline of the task and the start of the work plus seven days, as the architecture demands.
2. **The sweep of the deadlines.** The worker owns a sweep that finds the applications that wait longer than 48 hours for a decision and the seats that wait longer than 24 hours for the start. It reads the rows without a lock, locks the task first and then the seat, and it uses the injected clock, so the sweep and a command never produce two legal transitions. The sweep runs in the transaction of the caller, so an expiry and its escrow move together.
3. **The escrow of a seat.** The acceptance writes no ledger row: the budget of the task stays reserved while the seat waits for its start. The start moves the **base reward of the slot** from the reserved bucket of the publisher into the pending bucket of the contributor through `BeginPendingAsync`, inside the same scoped `DbContext` and the same transaction as the state of the seat. The bonus of the slot stays reserved, because the shared contract of the settlement releases the bonus separately ("Submission moves base from the assigned reservation into contributor pending; bonus stays reserved"). Only the credits module writes a ledger row.
4. **The escrow of a seat that never starts.** The withdrawal and the expiry of an accepted seat release the lease and give the slot back to the task while the task still accepts work and its deadline did not pass; the budget of the slot stays reserved for the next contributor. Otherwise the budget of the slot is refunded to the publisher with `RefundUnstartedAsync`, because nothing had moved yet.
5. **The escrow of a running seat.** The cancellation of a task refunds only the slots that are still available, so the running seat keeps its budget and its submission grace, as the architecture demands. The settlement of the pending credit of a seat and the release of its bonus belong to the package that owns the submissions and the disputes: the credit module has no primitive that returns a pending credit to a publisher, so the expiry of a running participation and the refund of its pending budget are not implemented here. This is the boundary of this package and it is deliberate.
6. **The member of the project.** A member or the owner of the project of the task never earns from that project. The application answers 403, even when the public capabilities match, and the acceptance checks the rule again and refuses the applicant that joined the project in the meantime.
7. **The aggregate status of the task.** A task with a running seat carries the state `in_progress`, a task without one is `open` again and a cancelled task keeps its state. The recompute happens under the lock of the task. The status does not change the version of the task, because the content the contributor saw did not change; the version stays the optimistic concurrency version of the content and of the publication.
8. **The slot state.** The state `leased` was added to the slots of a task: an accepted seat holds its slot before the work starts, and the cancellation must not refund it. The other six states keep their meaning.
9. **The instructions of a task.** The scope and the steps are released with the seat, so an applied seat never carries them and the eligibility of a contributor is never read from the instructions of a task.
10. **The visibility of a seat.** A seat is visible to its contributor, to the owner of the task and to the owner of the project. Every other actor answers 404, and the answer never names the project, the owner or the contributor. The applications of a task are readable by the owner of the task and by the owner of the project only.

## The defects the integrator found and fixed

1. The navigation of the shell overflowed by 45 pixels at 768 pixels, because the package added a link to the navigation. The navigation now wraps, so no page of the product scrolls horizontally at the three widths.
2. The status of a task with running work was not shown in the interface. The badge of the state now reads "in progress".
3. The aggregate status of a task was recomputed before the seat was written, so the query of the running seats still saw the old state. The commands now write the seat first, recompute the status and write it in the same transaction.

## The defect of the harness of the FOUND package, and the fix of the integrator

This package found a defect that belongs to an earlier package. The implementer did not change the file, because the earlier package is archived and owns it. The integrator fixed the harness, so the suite runs with the plain command again.

`Found3PortabilityTests.TheLockedRestoreSucceedsWithTheRepositoryNuGetConfiguration` runs `dotnet restore Blindmesh.sln --locked-mode` through `ProcessRunner`, which redirects the output of the child and then awaits `Process.WaitForExitAsync`. That wait needs the end of the output as well as the end of the process. The workers of MSBuild keep the inherited handle of the output open after the restore is done, when the reuse of the nodes is on, so the end of the output never comes and the wait runs into its timeout of ten minutes.

The observed evidence of the implementer:

| Check | Result |
|---|---|
| `dotnet restore Blindmesh.sln --locked-mode --configfile NuGet.config` on the command line, while the test was waiting | finishes in 1.8 seconds with the exit code 0 |
| The same test alone | passes in 1 second |
| The whole suite | 97 passed, 1 failed after 10 minutes and 26 seconds |
| The whole suite with `MSBUILDDISABLENODEREUSE=1` | 98 passed, 0 failed in 27 seconds |

The fix of the integrator is in `tests/Blindmesh.Tests/Runtime/ProcessRunner.cs` and holds for every child, not only for the children of MSBuild. The runner waits for the exit of the process through the event of the process, reads the output with `ReadToEndAsync` and bounds the wait for the end of the output with a grace period of thirty seconds. A child that leaves a grandchild behind therefore cannot hang the suite for ten minutes; the answer holds the output that was read. The plain command `dotnet test Blindmesh.sln -c Release --no-build` now reports 98 passed, 0 failed in 28 seconds, with no environment variable and with the reuse of the MSBuild nodes on.

## Fixtures and limits

- The browser journeys use the local OAuth fixture with the seeded actors: alice, bob, cara, dana and erin. The journeys own their workspace, their task, their capability and their application, so they never inherit the state of another journey. The limited grant of an owner pays for the tasks of the journeys, so the journeys of the package spread their owners over alice, dana and erin.
- The deadlines of the xUnit tests are proved with the timestamps of the committed rows and with the injected clock of the product. The clock of the product is never moved.
- The concurrency of the acceptance is proved with two commands at the same time against the last slot of a task; the row locks of the shared contract serialize them.
- The upgrade of the schema is proved in a second database of the test container: the migration of the package runs on the schema of the previous package and keeps its rows.
- The compose stack carries the images of this package: the integrator rebuilt it before the FOUND run.
- The suite of the tests runs with the plain command. The defect of the harness of the FOUND package is fixed.

## Independent re-run by the integrator

The integrator repeated the whole command set against the working tree of this package after the implementer finished, fixed the harness defect and repeated every journey group. The results were: the .NET chain was clean (restore, format, build, and 98 xUnit tests in 28 seconds), the web chain was clean (`ci`, `lint`, `typecheck`, `build` and `api:check` byte-identical), the browser journeys of the identity, the project, the capability, the credit, the task and the participation package passed 69 of 69, the axe runs of the six packages passed 37 of 37, and the FOUND journeys passed 8 of 8 with 4 of 4 axe runs against the compose stack rebuilt with the code of this package.
