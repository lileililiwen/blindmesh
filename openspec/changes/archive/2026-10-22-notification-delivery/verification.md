# Verification

The verification of the package `notification-delivery`. Every command below was run on the machine of the worker, in the repository `/home/paul/code/blindmesh`, on the branch `main`, on the 22nd of October 2026. The outputs are the observed ones. A check that did not run is named as such and is never reported as a pass.

The run added the notification module, the worker that sends the notifications, the four endpoints of the API, the two pages of the web application, the tests of the package, the journeys of the browser, the runs of axe, the traceability and this record.

## The environment of the run

| Item | Value |
|---|---|
| The API of the worker | `http://127.0.0.1:58094`, started with `APP_ENV=test` and the local OAuth fixture |
| The web application of the worker | `http://127.0.0.1:53104`, started with `BLINDMESH_API_URL=http://127.0.0.1:58094` |
| The worker of the product | `dotnet run --project src/Blindmesh.Worker --no-launch-profile`, the same database |
| The database of the journeys | the container `blindmesh-postgres-1` on the port `55432`, the database `bm_notif` |
| The object storage | the container `blindmesh-object-storage-1` on the port `59000` |
| The mail sink | the container `blindmesh-mail-1` on the port `51025`, its HTTP interface on the port `58025` |
| The browsers | Chromium only |
| Node | 22.23.3 |
| npm | 10.9.9 |
| .NET SDK | 10.0.400 |
| Playwright | 1.64.1 |

The database of the run was created and migrated before the journeys:

```
docker exec blindmesh-postgres-1 psql -U blindmesh -d postgres -c 'CREATE DATABASE "bm_notif";'
```

```
CREATE DATABASE
```

```
Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=bm_notif;Username=blindmesh;Password=blindmesh" \
  dotnet run --project src/Blindmesh.Api --no-launch-profile -- migrate
```

```
info: Microsoft.EntityFrameworkCore.Migrations[20402]
      Applying migration '20261021225331_AddNotificationDelivery'.
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
  Blindmesh__ObjectStorage__Bucket=blindmesh-notif \
  Blindmesh__Mail__Host=127.0.0.1 \
  Blindmesh__Mail__Port=51025 \
  Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=bm_notif;Username=blindmesh;Password=blindmesh" \
  dotnet run --project src/Blindmesh.Api --no-launch-profile
```

The worker of the run:

```
Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=bm_notif;Username=blindmesh;Password=blindmesh" \
  Blindmesh__Mail__Host=127.0.0.1 Blindmesh__Mail__Port=51025 \
  dotnet run --project src/Blindmesh.Worker --no-launch-profile
```

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

The web application was warmed with a request before the first journey, because the first request of the browser would otherwise compile the route and the journey would fail:

```
$ curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:53104/notifications
200
$ curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:53104/settings/notifications
200
```

## 1. The .NET chain

### `dotnet restore Blindmesh.sln --locked-mode`

```
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Domain/Blindmesh.Domain.csproj (用时 93 毫秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Application/Blindmesh.Application.csproj (用时 9.9 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Worker/Blindmesh.Worker.csproj (用时 9.9 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Infrastructure/Blindmesh.Infrastructure.csproj (用时 9.93 秒)。
  已还原 /home/paul/code/blindmesh/tests/Blindmesh.Tests/Blindmesh.Tests.csproj (用时 9.92 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Api/Blindmesh.Api.csproj (用时 9.93 秒)。
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

已用时间 00:00:03.07
```

The exit code is 0. The five warnings are the `NU1900` of the unreachable platform feed, one per project of the solution.

### `dotnet test Blindmesh.sln -c Release --no-build`

```
已通过! - 失败:     0，通过:   132，已跳过:     0，总计:   132，持续时间: 1 m 43 s - Blindmesh.Tests.dll (net10.0)
```

The exit code is 0. The 132 tests hold the 126 tests of the earlier packages and the 6 tests of this package.

## 2. The tests of the package

```
dotnet test tests/Blindmesh.Tests/Blindmesh.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~Blindmesh.Tests.Notifications"
```

```
已通过! - 失败:     0，通过:     6，已跳过:     0，总计:     6，持续时间: 1 m 28 s - Blindmesh.Tests.dll (net10.0)
```

The exit code is 0. The six tests are the six scenarios of the contract of NOTIF-1 and NOTIF-2:

| Scenario | Test |
|---|---|
| NOTIF-1 success | `Notif1Success_OneInAppItemAppearsAndAnEligibleEmailIsSentWithAStableMessageId` |
| NOTIF-1 rejection | `Notif1Rejection_AFailedAttemptKeepsItsRetryStateAndStatesTheUncertainty` |
| NOTIF-1 boundary | `Notif1Boundary_TheDeliveryIsDeadLetteredWithARedactedOperatorAlertWhileOtherDeliveriesContinue` |
| NOTIF-2 success | `Notif2Success_TheUnreadStateAndThePreferencesPersistWithTheFeedbackOfTheServer` |
| NOTIF-2 rejection | `Notif2Rejection_AnotherAccountGetsNoSummaryAndNoActorIdentity` |
| NOTIF-2 boundary | `Notif2Boundary_AQueuedEmailOfARevokedMembershipHoldsNoPrivateContent` |

The tests drive the real API against the isolated PostgreSQL of the test run, they sign in through the local OAuth fixture and they read the database directly to assert the stored state. The consumer of the outbox and the sender of the notifications of the product run inside the host of the test, so a notification of a test is written and sent by the code that runs in production.

The test of the success of NOTIF-1 sends its email to the real mail sink of the environment and reads the message back over the HTTP interface of the sink (`http://127.0.0.1:58025/api/v1/messages` and `/api/v1/message/{id}`). The transport is therefore the real one and not a stand in.

The tests of the rejection and of the boundary use a local SMTP sink of the test project. The sink of the rejection accepts the connection and never answers, so the attempt of the mail channel runs into its own timeout and the outcome of the attempt stays uncertain. The sink of the boundary refuses one recipient with the code `550` and accepts the others, so the delivery of the contributor dies while the delivery of the owner of the task goes out. The tests move the moment of the next attempt into the past, so they do not wait for the wall clock; the schedule itself is asserted by the boundary test, which reads the moments that the service computes.

## 3. The chain of the web application

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

The exit code is 0. The build holds the two routes of the package, `/notifications` and `/settings/notifications`.

### `npm --prefix apps/web run api:check`

```
The generated API client matches a fresh generation.
```

The exit code is 0. The generated files `packages/api-client/openapi.json` and `packages/api-client/src/generated/schema.ts` were written by `npm --prefix apps/web run api:generate` and were never edited by hand. The hand-written client holds the four methods of the package.

## 4. The journeys of the browser

```
BLINDMESH_API_URL=http://127.0.0.1:58094 BLINDMESH_WEB_URL=http://127.0.0.1:53104 \
  npm --prefix apps/web run test:e2e -- --project=chromium --grep NOTIF
```

```
  ✓  1 [chromium] › ../../tests/e2e/notification-delivery/journey.spec.ts:233:5 › NOTIF-UX the notification of an accepted work appears and is marked as read (34.6s)
  ✓  2 [chromium] › ../../tests/e2e/notification-delivery/journey.spec.ts:265:5 › NOTIF-UX the preferences of the notifications are saved and announced by the server (492ms)
  ✓  3 [chromium] › ../../tests/e2e/notification-delivery/journey.spec.ts:292:5 › NOTIF-UX the list of a contributor without a notification explains the next action (396ms)
  ✓  4 [chromium] › ../../tests/e2e/notification-delivery/journey.spec.ts:304:5 › NOTIF-UX an unavailable API explains the failure and offers a retry (456ms)
  ✓  5 [chromium] › ../../tests/e2e/notification-delivery/journey.spec.ts:327:5 › NOTIF-UX the pages of the notifications have no horizontal overflow at 360, 768 and 1440 pixels (33.7s)
  ✓  6 [chromium] › ../../tests/e2e/notification-delivery/journey.spec.ts:358:5 › NOTIF-UX the journey of the notifications works with the keyboard only (348ms)
  ✓  7 [chromium] › ../../tests/e2e/notification-delivery/journey.spec.ts:378:5 › NOTIF-UX the journey of the notifications works in the dark theme (312ms)

  7 passed (1.2m)
```

The exit code is 0.

## 5. The runs of axe

```
BLINDMESH_API_URL=http://127.0.0.1:58094 BLINDMESH_WEB_URL=http://127.0.0.1:53104 \
  npm --prefix apps/web run test:a11y -- --grep NOTIF
```

```
  ✓  1 [a11y] › ../../tests/e2e/notification-delivery/accessibility.a11y.ts:231:5 › NOTIF-UX the list of the notifications has no accessibility violation (34.1s)
  ✓  2 [a11y] › ../../tests/e2e/notification-delivery/accessibility.a11y.ts:248:5 › NOTIF-UX the list of the notifications has no accessibility violation at 360 pixels (35.9s)
  ✓  3 [a11y] › ../../tests/e2e/notification-delivery/accessibility.a11y.ts:271:5 › NOTIF-UX the preferences of the notifications have no accessibility violation (843ms)
  ✓  4 [a11y] › ../../tests/e2e/notification-delivery/accessibility.a11y.ts:285:5 › NOTIF-UX the preferences of the notifications have no accessibility violation in the dark theme (835ms)

  4 passed (1.2m)
```

The exit code is 0. The runs used the tags `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`, `wcag22aa` and `best-practice`. The list of the notifications was read with a real notification of an accepted work, so the run covered the loaded state and not only the empty state.

## 6. The planning verification

```
python3 scripts/verify_bootstrap.py
```

```
PASS planning artifacts: 12 changes, 37 requirements, 111 scenarios, 18 unchecked tasks; token pin and 12 contrast pairs verified
Product/runtime/browser/deployment verification: NOT PERFORMED
```

```
node scripts/check-openspec-change-names.mjs
```

```
OpenSpec change names: PASS
```

`openspec validate --all --strict --no-interactive` did not run: the command `openspec` is not on the path of the machine of the worker. The two planning scripts above ran and passed.

## 7. The checks that did not run

| Check | Why |
|---|---|
| The journeys in Firefox and in WebKit | Only Chromium is installed on the machine of the worker. The release smoke of the product still has to run them. |
| `openspec validate --all --strict --no-interactive` | The command `openspec` is not on the path of the machine of the worker. |
| The migration from the schema of the previous package | The migration `AddNotificationDelivery` only creates the two tables of the package and changes no earlier table. The fresh database of the run was migrated from zero, and the tests of the package migrate the isolated database of the test run from zero as well. The upgrade of a database that holds the rows of the earlier packages was not run. |
| The release smoke and the deployment | Not part of this change. |

## 8. The decisions of the run that a reader of the contract should know

| Decision | What was done and why |
|---|---|
| The identity of a delivery | The identity of a delivery is the event type of the notification, the identifier of the outbox message, the recipient and the channel. The outbox message is the identity of the concrete event, so the same kind of event on two aggregates never shares one delivery, and a redelivered message never writes a second row. |
| The schedule of the retries | The five delays of the product are the waits before the five attempts: a minute, five minutes, thirty minutes, two hours and eight hours. The delivery that failed its fifth attempt is dead lettered. All five delays of the schedule are used and the fifth failed attempt ends the life of the delivery, as the design of the package names it. |
| The in-app row and the email row | The in-app row is the notification of the recipient. The email row is the copy that goes to the mailbox. The list of the API holds the in-app rows, so one event never shows twice. |
| The categories with a producer | `ApplicationReceived`, `ApplicationAccepted`, `SubmissionReceived` and `CorrectionRequested` got a producer in the module that owns them (the tasks module and the findings module). `Acceptance`, `DisputeUpdate` and `ModerationOutcome` are carried by the events that already existed. `Deadline` and `Refund` have no producer: the enum and the preferences carry them, but no module writes such an event, so no notification is faked for them. |
| The alert of the operator | The alert of a dead letter is an in-app notification for every account that administers the product. The category of the alert is `ModerationOutcome`, because the enum of the package holds exactly the nine categories of the product; the summary of the alert names the queue and holds no detail. |
| The change of the claim cycle of the outbox | One outbox event may have more than one consumer, so the handlers of the claim cycle are grouped by their event type. Without this change the claim cycle would stop, because the reputation module and the notification module both consume `findings.participation.accepted` and `findings.finding.confirmed`. |
| The address of the sender | The mail channel sends from `no-reply@blindmesh.local`. The `MailOptions` of the runtime options carry the host and the port only, and no other file of the runtime foundation was changed. |
