# Verification

The verification of the package `moderation-arbitration`. Every command below was run on the machine of the worker, in the repository `/home/paul/code/blindmesh`, on the branch `main`. The outputs are the observed ones. A check that did not run is named as such and is never reported as a pass.

The backend half of the package was implemented before this half and was not changed by it. The interface was built on the contract of the backend as it stands.

A second run closed the two gaps the first run left open: the bonus of a decision and the reason of a case. Its record is the section 9. The sections below are the record of the first run and are left as they were written; where the two runs differ, the section 9 is the later truth.

## The environment of the run

| Item | Value |
|---|---|
| The API of the worker | `http://127.0.0.1:58098`, started with `APP_ENV=test` and the local OAuth fixture |
| The web application of the worker | `http://127.0.0.1:53108`, started with `BLINDMESH_API_URL=http://127.0.0.1:58098` |
| The database of the journeys | the container `blindmesh-postgres-1` on the port `55432`, the database `bm_mod` |
| The database of every group | a fresh database: the database was dropped, created and migrated before every group |
| The browsers | Chromium only |
| Node | 24 |
| npm | 11.16.0 |
| .NET SDK | 10.0.400 |

The seed of the database before a group:

```
docker exec blindmesh-postgres-1 psql -U blindmesh -d postgres -c "DROP DATABASE IF EXISTS \"bm_mod\";"
docker exec blindmesh-postgres-1 psql -U blindmesh -d postgres -c "CREATE DATABASE \"bm_mod\";"
dotnet run --project src/Blindmesh.Api --no-launch-profile -- migrate
```

The name of the database is written between quotes, because an unquoted name is folded to lower case and the API would read a database that is not the one of the run.

## 1. The .NET chain

### `dotnet restore Blindmesh.sln --locked-mode`

```
已还原 /home/paul/code/blindmesh/src/Blindmesh.Domain/Blindmesh.Domain.csproj (用时 90 毫秒)。
已还原 /home/paul/code/blindmesh/src/Blindmesh.Application/Blindmesh.Application.csproj (用时 15.52 秒)。
已还原 /home/paul/code/blindmesh/src/Blindmesh.Api/Blindmesh.Api.csproj (用时 16.7 秒)。
已还原 /home/paul/code/blindmesh/src/Blindmesh.Infrastructure/Blindmesh.Infrastructure.csproj (用时 17.56 秒)。
已还原 /home/paul/code/blindmesh/src/Blindmesh.Worker/Blindmesh.Worker.csproj (用时 18.48 秒)。
已还原 /home/paul/code/blindmesh/tests/Blindmesh.Tests/Blindmesh.Tests.csproj (用时 24.89 秒)。
```

The exit code is 0. The warning `NU1900` of the public platform feed `https://lileililiwen.github.io/forge/nuget/index.json` stands on every project, because the feed redirects to a 404:

```
/home/paul/code/blindmesh/src/Blindmesh.Application/Blindmesh.Application.csproj : warning NU1900: 获取包漏洞数据时出错: 无法加载源 https://lileililiwen.github.io/forge/nuget/index.json 的服务索引。 [/home/paul/code/blindmesh/Blindmesh.sln]
```

The warning is expected and was left visible. The feed was not removed from `NuGet.config`.

### `dotnet format Blindmesh.sln --verify-no-changes`

```
加载工作区时遇到警告。要记录警告，请将“详细级别”选项设置为“诊断”级别。
```

The exit code is 0. No file of the solution changed.

### `dotnet build Blindmesh.sln -c Release --no-restore`

```
已成功生成。
    5 个警告
    0 个错误

已用时间 00:00:07.68
```

The five warnings are the `NU1900` warnings of the feed above.

### `dotnet test Blindmesh.sln -c Release --no-build`

```
已通过! - 失败:     0，通过:   115，已跳过:     0，总计:   115，持续时间: 27 s - Blindmesh.Tests.dll (net10.0)
```

The exit code is 0. The nine tests of the moderation module are part of the 115:

| Test | File |
|---|---|
| `MOD1SuccessTheIndependentModeratorDecidesTheCaseAndThePartiesSeeTheDecisionAndTheLedgerResult` | `tests/Blindmesh.Tests/Moderation/MOD1ArbitrationTests.cs` |
| `MOD1RejectionTheConflictingModeratorAndTheOrdinaryUserAreRefusedAndTheEscrowStaysUnchanged` | `tests/Blindmesh.Tests/Moderation/MOD1ArbitrationTests.cs` |
| `MOD1BoundaryTheAppealReachesAnotherModeratorAndThePriorRowsStayImmutable` | `tests/Blindmesh.Tests/Moderation/MOD1ArbitrationTests.cs` |
| `MOD1BoundaryOnlyOneCaseIsOpenPerSubmissionAndTheSettlementOfTheSeatWaitsForTheDecision` | `tests/Blindmesh.Tests/Moderation/MOD1ArbitrationTests.cs` |
| `MOD1SuccessTheDecisionAgainstTheContributorMovesTheBaseBackToThePublisher` | `tests/Blindmesh.Tests/Moderation/MOD1ArbitrationTests.cs` |
| `MOD1BoundaryTheCaseMayOnlyBeClosedAfterTheDecisionAndTheWindowOfTheAppeal` | `tests/Blindmesh.Tests/Moderation/MOD1ArbitrationTests.cs` |
| `MOD2SuccessTheReporterReadsAReceiptAndOnlyTheStaffReadsTheBody` | `tests/Blindmesh.Tests/Moderation/MOD2SafetyTests.cs` |
| `MOD2RejectionTheBannedAccountIsRefusedBeforeAnyEffect` | `tests/Blindmesh.Tests/Moderation/MOD2SafetyTests.cs` |
| `MOD2BoundaryTheReportOfPrivateContentStaysWithTheStaffAndRevealsNothingToAnUnrelatedAccount` | `tests/Blindmesh.Tests/Moderation/MOD2SafetyTests.cs` |

## 2. The web application

### `npm --prefix apps/web ci`

```
npm warn deprecated eslint@9.39.5: This version is no longer supported. Please see https://eslint.org/version-support for other options.

added 391 packages, and audited 393 packages in 3m

154 packages are looking for funding
  run `npm fund` for details

5 high severity vulnerabilities

Some issues need review, and may require choosing
a different dependency.

Run `npm audit` for details.
npm warn allow-scripts 1 package has install scripts not yet covered by allowScripts:
npm warn allow-scripts   unrs-resolver@1.12.2 (postinstall: node postinstall.js)
npm warn allow-scripts
npm warn allow-scripts Run `npm approve-scripts --allow-scripts-pending` to review, or `npm approve-scripts <pkg>` to allow.
```

The exit code is 0. The five advisories of a high severity come from the chain of the development dependency `eslint-config-next`, which pulls `braces`, `fast-glob` and `micromatch`. There is no patched release of `braces`, so the chain was not broken by a forced upgrade. The shipped dependencies of the production audit clean.

### `npm --prefix apps/web run lint`

```
> blindmesh-web@0.1.0 lint
> eslint .
```

The exit code is 0. No warning and no error.

### `npm --prefix apps/web run typecheck`

```
> blindmesh-web@0.1.0 typecheck
> tsc --noEmit
```

The exit code is 0.

### `npm --prefix apps/web run build`

```
▲ Next.js 16.4.2 (Turbopack)
✓ Compiled successfully in 949ms
  Finished TypeScript in 1641ms ...
✓ Generating static pages using 11 workers (18/18) in 267ms
```

The exit code is 0. The build wrote the four new routes of the package:

```
┌ ○ /
├ ○ /_not-found
├ ○ /admin/disputes
├ ○ /admin/reports
├ ƒ /api/[...path]
├ ○ /capabilities
├ ƒ /disputes/[id]
├ ƒ /findings/[id]
├ ○ /onboarding
├ ƒ /people/[username]
├ ○ /projects
...
├ ○ /reports/new
...
```

The four routes are `/reports/new`, `/disputes/[id]`, `/admin/reports` and `/admin/disputes`.

### `npm --prefix apps/web run api:check`

```
The generated API client matches a fresh generation.
```

The exit code is 0. The committed `packages/api-client/openapi.json` and `packages/api-client/src/generated/schema.ts` are byte identical to a fresh generation. The document was stale before this change, because the moderation endpoints were added after the last generation; `npm --prefix apps/web run api:generate` wrote the new document and it is part of this change.

## 3. The journeys of the browser

### `npm --prefix apps/web run test:e2e -- --project=chromium --grep MOD`

The database was fresh. The output:

```
Running 11 tests using 1 worker

  ✓   1 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:267:5 › MOD-UX the case runs from the evidence of the seat to the decision of the moderator (7.7s)
  ✓   2 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:318:5 › MOD-UX the party of the case appeals the decision once and another moderator receives the case (33.6s)
  ✓   3 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:371:5 › MOD-UX the moderator that already acted on the case is refused (35.2s)
  ✓   4 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:398:5 › MOD-UX the staff asks the parties of a case for more evidence (34.5s)
  ✓   5 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:432:5 › MOD-UX the reporter reads the receipt of the report and the staff read the report (33.5s)
  ✓   6 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:480:5 › MOD-UX a report with a short reason is refused and the draft stays (378ms)
  ✓   7 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:498:5 › MOD-UX an unavailable API explains the failure and offers a retry (307ms)
  ✓   8 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:509:5 › MOD-UX the pages of the moderation have no horizontal overflow at 360, 768 and 1440 pixels (33.7s)
  ✓   9 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:559:5 › MOD-UX the journey of the moderation works with the keyboard only (451ms)
  ✓  10 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:583:5 › MOD-UX the journey of the moderation works in the dark theme (410ms)
  ✓  11 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:603:5 › MOD-UX a banned account may not write a report (758ms)

  11 passed (3.0m)
```

The exit code is 0. The filter `MOD` also matches the test `TASK2SuccessAVisitorFiltersThePublicTasksByModeAndRequirement` of the package of the tasks, which is expected and which passes in the group of the tasks.

### `npm --prefix apps/web run test:a11y -- --grep MOD`

The database was fresh. The output:

```
Running 7 tests using 1 worker

  ✓  1 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:237:5 › MOD-UX the report form has no accessibility violation (2.5s)
  ✓  2 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:250:5 › MOD-UX the report form has no accessibility violation at 360 pixels (737ms)
  ✓  3 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:265:5 › MOD-UX the report form has no accessibility violation in the dark theme (766ms)
  ✓  4 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:281:5 › MOD-UX the queue of the reports has no accessibility violation (750ms)
  ✓  5 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:294:5 › MOD-UX the queue of the cases has no accessibility violation (730ms)
  ✓  6 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:307:5 › MOD-UX the case of an arbitration has no accessibility violation (6.4s)
  ✓  7 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:334:5 › MOD-UX the case of an arbitration has no accessibility violation at 360 pixels (33.0s)

  7 passed (45.5s)
```

The exit code is 0. The runs of axe carry the tags `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`, `wcag22aa` and `best-practice`, and no violation was found in any of the seven pages.

## 4. The regression of the earlier packages

Every group ran on its own fresh database: the database was dropped, created and migrated before the journeys of the group and again before the runs of axe of the group. The commands of every group were:

```
BLINDMESH_WEB_URL=http://127.0.0.1:53108 npm --prefix apps/web run test:e2e -- --project=chromium --grep <GROUP>
BLINDMESH_WEB_URL=http://127.0.0.1:53108 npm --prefix apps/web run test:a11y -- --grep <GROUP>
```

| Group | The journeys of the browser | The runs of axe |
|---|---|---|
| `IDENT` | 12 passed (7.1s) | 6 passed (4.8s) |
| `PROJ` | 15 passed (11.2s) | 7 passed (7.7s) |
| `CAP` | 12 passed (8.1s) | 5 passed (5.4s) |
| `CRED` | 8 passed (6.2s) | 5 passed (5.8s) |
| `TASK` | 14 passed (11.3s) | 9 passed (8.8s) |
| `PART` | 10 passed (1.9m) | 6 passed (7.1s) |
| `EVID` | 10 passed (2.9m) | 4 passed (5.2s) |

Every group ran with the exit code 0. The group `TASK` includes the test `TASK2SuccessAVisitorFiltersThePublicTasksByModeAndRequirement`, which the filter `MOD` also matches.

## 5. The planning checks

### `python3 scripts/verify_bootstrap.py`

```
PASS planning artifacts: 12 changes, 37 requirements, 111 scenarios, 36 unchecked tasks; token pin and 12 contrast pairs verified
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
✓ change/moderation-arbitration
✓ change/notification-delivery
✓ spec/participation-execution
✓ change/pilot-operations
✓ spec/project-workspaces
✓ change/reputation-history
✓ spec/runtime-foundation
✓ spec/task-publication
Totals: 12 passed, 0 failed (12 items)
```

The exit code is 0.

## 6. The checks that did not run

| Check | Why |
|---|---|
| The journeys and the runs of axe of the Firefox and of the WebKit | The runtimes are not installed on this machine. Only the Chromium browser is installed. |
| `forge gate .` | The Forge binary is not on the path of the machine. |
| The smoke of the release of the product | The release pipeline of the product does not exist yet. |

## 7. The defects found in the backend

The backend was not changed by this half of the package. The three findings below are the honest reading of the contract of the API, and the interface of the package works around them without inventing any data.

| Finding | Where | What the interface does |
|---|---|---|
| The answer of a case carries neither the reason of the case nor the note of a request of more evidence, although the service builds the evidence and the trail of the case. | `src/Blindmesh.Application/Moderation/ModerationService.cs:325` builds `Evidence` and `Audit`; `src/Blindmesh.Api/Endpoints/Moderation/ModerationEndpoints.cs:329` answers with `result.Dispute` alone; `src/Blindmesh.Application/Moderation/ModerationContracts.cs:117` has no field for the reason of the case. | The page of a case shows the state, the stake, the award, the decision with its reason, the move of the credits and the appeal, and it says that the note of the request of more evidence stays in the trail. |
| The document of the API declares the receipt of a report for `GET /v1/reports/{id}`, but the answer of a staff member carries the whole report with the reason. | `src/Blindmesh.Api/Endpoints/Moderation/ModerationEndpoints.cs:56` declares `ReportReceiptView`; the answer at line 227 of the same file is the report of the staff. | The reader of the receipt of a report uses the declared contract. The queue of the reports uses the list of the staff, which declares the whole report. |
| The API leaves out every field of a null value, while the document marks these fields as required. | `src/Blindmesh.Api/Program.cs:74` sets `JsonIgnoreCondition.WhenWritingNull`; the generated types promise `null` for these fields. | The interface reads an absent field as absent and never as a value. |

## 8. The honest limits of the evidence

- The reason of a case and the note of a request of more evidence are in the trail of the case. The answer of the API does not carry them, so the page of a case says so instead of showing a field that is empty. The journeys do not claim to see them.
- The command that opens the case of a seat and the command that bans an account have no page of their own in this package. The journeys send them through the API with the session and the CSRF token of the browser, and the client library of the module carries both.
- The journeys of the browser ran with the local OAuth fixture of the test environment. They prove the interface and the contract of the API, not the behaviour of a real provider.
- The runs of axe prove the rules of WCAG 2.2 AA that axe knows. They do not prove the meaning of a page for a reader of a screen reader, and no reading test with a person was made.
- The journeys of the browser ran with the seeds of the local fixture. The performance of the pages under a load was not measured.
- The design of the package names a bonus in the decision of a case. The contract of the API has no such field: the decision carries the boolean `award_base` and the reason. The interface follows the contract and offers no bonus.

## 9. The second run: the bonus of a decision and the reason of a case

The first half of the package left two gaps open. The design named a bonus in the decision of a case and the contract of the API had no such field, and the answer of a case carried neither the reason the case was opened nor the note of a request of more evidence, although the service built both. This half closed the two gaps.

The sections above are the record of the first run and are left exactly as they were written. Where the two runs differ, this section is the later truth.

### 9.1 What changed

| Gap | The change |
|---|---|
| The bonus of a decision | The body of `POST /v1/admin/disputes/{id}/resolve` carries an optional `bonus` in whole credits, and a body without the field still works. A bonus needs the base of the seat: a bonus without the base answers 422 and writes nothing. A bonus may never be more than the reserved escrow of the task: the credit module refuses the command and the case answers 409 with the escrow exactly as it was. The bonus leaves the reserved bucket of the publisher and lands in the pending bucket of the contributor with `BeginPendingAsync`, and the arbitration pays the base and the bonus out of the pending bucket with `ArbitrateAsync`. Both calls run in the same transaction as the rest of the command. |
| The record of the award | The row of the case keeps the amount the award moved, so the appeal compensates the base and the bonus together with `CompensateArbitrationAsync`. The answer of a case carries the amount as `awarded_amount` and as `ledger_amount`. |
| The reason of a case | The row of the case keeps the note of a request of more evidence, and the answer of a case carries the reason it was opened, the note of the request and the amount the award moved. The page of a case shows the reason and the note. |

The files of this half:

| File | What it holds |
|---|---|
| `src/Blindmesh.Domain/Moderation/Dispute.cs` | the column `AwardedAmount` and the column `EvidenceNote` of the row of a case |
| `src/Blindmesh.Infrastructure/Moderation/ModerationModelConfiguration.cs` | the names of the two columns |
| `src/Blindmesh.Infrastructure/Persistence/Migrations/20261021105402_AddDisputeBonusAndReason.cs` | the migration of the two columns |
| `src/Blindmesh.Application/Moderation/ModerationContracts.cs` | the field `Bonus` of the command of a decision, the fields `Reason`, `EvidenceNote` and `AwardedAmount` of the answer of a case |
| `src/Blindmesh.Application/Moderation/ModerationService.cs` | the rule of the bonus, the two calls of the credit module and the amount the award moved |
| `src/Blindmesh.Api/Endpoints/Moderation/ModerationEndpoints.cs` | the field `bonus` of the body of the decision and the message of a refused bonus |
| `apps/web/src/lib/moderation.ts` | the reading of the bonus of a decision |
| `apps/web/src/components/dispute-queue.tsx` | the field of the bonus and the rule of the base |
| `apps/web/src/components/dispute-detail.tsx` | the reason of the case, the note of the request and the amount the award moved |
| `packages/api-client/openapi.json`, `packages/api-client/src/generated/schema.ts` | the generated client of the new fields |
| `tests/Blindmesh.Tests/Moderation/MOD1BonusTests.cs` | the five tests of the backend of this half |
| `tests/e2e/moderation-arbitration/journey.spec.ts` | the two journeys of the browser of this half |
| `tests/e2e/moderation-arbitration/accessibility.a11y.ts` | the run of axe of the form of the decision |
| `docs/architecture.md` | the bonus of a decision, the reason of a case and the escrow of a dispute |

### 9.2 The environment of the second run

| Item | Value |
|---|---|
| The API of the worker | `http://127.0.0.1:58100`, started with `APP_ENV=test` and the local OAuth fixture |
| The web application of the worker | `http://127.0.0.1:53110`, started with `BLINDMESH_API_URL=http://127.0.0.1:58100` |
| The databases of the journeys | the container `blindmesh-postgres-1` on the port `55432`; a fresh database per group, dropped with `WITH (FORCE)`, created and migrated before every group |
| The migration | `20261021105402_AddDisputeBonusAndReason` |
| The browsers | Chromium only |

The migration touches the table `disputes` only. It adds the column `awarded_amount` (bigint, not null, default 0) and the column `evidence_note` (character varying(2000), null):

```
migrationBuilder.AddColumn<long>(
    name: "awarded_amount",
    table: "disputes",
    type: "bigint",
    nullable: false,
    defaultValue: 0L);

migrationBuilder.AddColumn<string>(
    name: "evidence_note",
    table: "disputes",
    type: "character varying(2000)",
    maxLength: 2000,
    nullable: true);
```

### 9.3 The .NET chain

The commands and their observed output:

### `dotnet restore Blindmesh.sln --locked-mode`

```
  正在确定要还原的项目…
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Domain/Blindmesh.Domain.csproj (用时 109 毫秒)。
/home/paul/code/blindmesh/src/Blindmesh.Worker/Blindmesh.Worker.csproj : warning NU1900: 获取包漏洞数据时出错: 无法加载源 https://lileililiwen.github.io/forge/nuget/index.json 的服务索引。 [/home/paul/code/blindmesh/Blindmesh.sln]
/home/paul/code/blindmesh/src/Blindmesh.Application/Blindmesh.Application.csproj : warning NU1900: 获取包漏洞数据时出错: 无法加载源 https://lileililiwen.github.io/forge/nuget/index.json 的服务索引。 [/home/paul/code/blindmesh/Blindmesh.sln]
/home/paul/code/blindmesh/src/Blindmesh.Infrastructure/Blindmesh.Infrastructure.csproj : warning NU1900: 获取包漏洞数据时出错: 无法加载源 https://lileililiwen.github.io/forge/nuget/index.json 的服务索引。 [/home/paul/code/blindmesh/Blindmesh.sln]
/home/paul/code/blindmesh/tests/Blindmesh.Tests/Blindmesh.Tests.csproj : warning NU1900: 获取包漏洞数据时出错: 无法加载源 https://lileililiwen.github.io/forge/nuget/index.json 的服务索引。 [/home/paul/code/blindmesh/Blindmesh.sln]
/home/paul/code/blindmesh/src/Blindmesh.Api/Blindmesh.Api.csproj : warning NU1900: 获取包漏洞数据时出错: 无法加载源 https://lileililiwen.github.io/forge/nuget/index.json 的服务索引。 [/home/paul/code/blindmesh/Blindmesh.sln]
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Application/Blindmesh.Application.csproj (用时 15.23 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Infrastructure/Blindmesh.Infrastructure.csproj (用时 15.4 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Api/Blindmesh.Api.csproj (用时 15.4 秒)。
  已还原 /home/paul/code/blindmesh/src/Blindmesh.Worker/Blindmesh.Worker.csproj (用时 16.03 秒)。
  已还原 /home/paul/code/blindmesh/tests/Blindmesh.Tests/Blindmesh.Tests.csproj (用时 16.43 秒)。
```
### `dotnet format Blindmesh.sln --verify-no-changes`

```
加载工作区时遇到警告。要记录警告，请将“详细级别”选项设置为“诊断”级别。
```

### `dotnet build Blindmesh.sln -c Release --no-restore`

```
/home/paul/code/blindmesh/tests/Blindmesh.Tests/Blindmesh.Tests.csproj : warning NU1900: 获取包漏洞数据时出错: 无法加载源 https://lileililiwen.github.io/forge/nuget/index.json 的服务索引。
/home/paul/code/blindmesh/src/Blindmesh.Api/Blindmesh.Api.csproj : warning NU1900: 获取包漏洞数据时出错: 无法加载源 https://lileililiwen.github.io/forge/nuget/index.json 的服务索引。
    5 个警告
    0 个错误

已用时间 00:01:05.12
```
### `dotnet test Blindmesh.sln -c Release --no-build`

```
/home/paul/code/blindmesh/tests/Blindmesh.Tests/bin/Release/net10.0/Blindmesh.Tests.dll (.NETCoreApp,Version=v10.0)的测试运行
总共 1 个测试文件与指定模式相匹配。

已通过! - 失败:     0，通过:   120，已跳过:     0，总计:   120，持续时间: 30 s - Blindmesh.Tests.dll (net10.0)
```

The exit code of every command is 0. The five warnings of the build are the `NU1900` warnings of the public platform feed `https://lileililiwen.github.io/forge/nuget/index.json`, which redirects to a 404. The warning is expected and was left visible; the feed was not removed from `NuGet.config`.

The 120 tests of the suite are the 115 of the first run and the five tests of this half:

| Test | File |
|---|---|
| `MOD1SuccessTheModeratorAwardsTheBaseAndTheBonusAndTheJournalStaysBalanced` | `tests/Blindmesh.Tests/Moderation/MOD1BonusTests.cs` |
| `MOD1RejectionTheBonusBeyondTheFundedCapIsRefusedAndTheEscrowStaysUnchanged` | `tests/Blindmesh.Tests/Moderation/MOD1BonusTests.cs` |
| `MOD1RejectionTheBonusWithoutTheBaseIsRefusedAndNothingIsWritten` | `tests/Blindmesh.Tests/Moderation/MOD1BonusTests.cs` |
| `MOD1BoundaryTheAppealThatReversesADecisionWithABonusMovesTheBaseAndTheBonusBack` | `tests/Blindmesh.Tests/Moderation/MOD1BonusTests.cs` |
| `MOD2BoundaryTheReasonOfTheCaseIsReadableByThePartiesAndTheStaffAndNotByAnUnrelatedAccount` | `tests/Blindmesh.Tests/Moderation/MOD1BonusTests.cs` |

### 9.4 The web application

```
> blindmesh-web@0.1.0 lint
> eslint .
```

```
> blindmesh-web@0.1.0 typecheck
> tsc --noEmit
```

```
> blindmesh-web@0.1.0 build
> next build

▲ Next.js 16.4.2 (Turbopack)
✓ Running next.config.ts took 115ms

  Creating an optimized production build ...
✓ Compiled successfully in 3.1s
  Running TypeScript ...
  Finished TypeScript in 1672ms ...
  Collecting page data using 11 workers ...
  Generating static pages using 11 workers (18/18) in 270ms
  Finalizing page optimization ...
```

```
> blindmesh-web@0.1.0 api:check
> node ../../packages/api-client/generate.mjs --check
...
The generated API client matches a fresh generation.
```

The exit code of every command is 0. The lint and the type check wrote no warning and no error. The client of the API is generated: `npm --prefix apps/web run api:generate` wrote `packages/api-client/openapi.json` and `packages/api-client/src/generated/schema.ts` with the fields `bonus` of the body of a decision and the fields `reason`, `evidence_note` and `awarded_amount` of the answer of a case. No model of the client was written by hand.

### 9.5 The journeys of the browser of the package

The database was fresh (`bm_MOD3` for the journeys, `bm_AMODA` for the runs of axe). The commands were:

```
BLINDMESH_API_URL=http://127.0.0.1:58100 BLINDMESH_WEB_URL=http://127.0.0.1:53110 npm --prefix apps/web run test:e2e -- --project=chromium --grep MOD
BLINDMESH_API_URL=http://127.0.0.1:58100 BLINDMESH_WEB_URL=http://127.0.0.1:53110 npm --prefix apps/web run test:a11y -- --grep MOD
```

```
Running 13 tests using 1 worker

  ✓   1 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:267:5 › MOD-UX the case runs from the evidence of the seat to the decision of the moderator (7.8s)
  ✓   2 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:318:5 › MOD-UX the party of the case appeals the decision once and another moderator receives the case (33.6s)
  ✓   3 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:371:5 › MOD-UX the moderator that already acted on the case is refused (32.9s)
  ✓   4 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:398:5 › MOD-UX the staff asks the parties of a case for more evidence (39.0s)
  ✓   5 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:436:5 › MOD-UX the moderator awards a bonus with the base and the parties read the reason of the case (34.1s)
  ✓   6 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:504:5 › MOD-UX a bonus is only awarded together with the base of the seat (32.8s)
  ✓   7 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:532:5 › MOD-UX the reporter reads the receipt of the report and the staff read the report (33.1s)
  ✓   8 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:580:5 › MOD-UX a report with a short reason is refused and the draft stays (388ms)
  ✓   9 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:598:5 › MOD-UX an unavailable API explains the failure and offers a retry (295ms)
  ✓  10 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:609:5 › MOD-UX the pages of the moderation have no horizontal overflow at 360, 768 and 1440 pixels (33.6s)
  ✓  11 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:659:5 › MOD-UX the journey of the moderation works with the keyboard only (450ms)
  ✓  12 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:683:5 › MOD-UX the journey of the moderation works in the dark theme (394ms)
  ✓  13 [chromium] › ../../tests/e2e/moderation-arbitration/journey.spec.ts:703:5 › MOD-UX a banned account may not write a report (658ms)

  13 passed (4.2m)
```

```
Running 8 tests using 1 worker

  ✓  1 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:237:5 › MOD-UX the report form has no accessibility violation (2.5s)
  ✓  2 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:250:5 › MOD-UX the report form has no accessibility violation at 360 pixels (879ms)
  ✓  3 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:265:5 › MOD-UX the report form has no accessibility violation in the dark theme (806ms)
  ✓  4 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:281:5 › MOD-UX the queue of the reports has no accessibility violation (755ms)
  ✓  5 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:294:5 › MOD-UX the queue of the cases has no accessibility violation (807ms)
  ✓  6 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:307:5 › MOD-UX the case of an arbitration has no accessibility violation (4.9s)
  ✓  7 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:334:5 › MOD-UX the form of the decision of a case has no accessibility violation (36.4s)
  ✓  8 [a11y] › ../../tests/e2e/moderation-arbitration/accessibility.a11y.ts:369:5 › MOD-UX the case of an arbitration has no accessibility violation at 360 pixels (34.1s)

  8 passed (1.3m)
```

The exit code of both commands is 0. The run of axe of the seventh line is the new one: it opens a case in the queue of the cases and reads the form of the decision with the field of the bonus. The runs of axe carry the tags `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`, `wcag22aa` and `best-practice`, and no violation was found in any of the eight pages.

### 9.6 The regression of the earlier packages

Every group ran on its own fresh database: the database was dropped with `WITH (FORCE)`, created and migrated before the journeys of the group and again before the runs of axe of the group. The commands of every group were:

```
BLINDMESH_API_URL=http://127.0.0.1:58100 BLINDMESH_WEB_URL=http://127.0.0.1:53110 npm --prefix apps/web run test:e2e -- --project=chromium --grep <GROUP>
BLINDMESH_API_URL=http://127.0.0.1:58100 BLINDMESH_WEB_URL=http://127.0.0.1:53110 npm --prefix apps/web run test:a11y -- --grep <GROUP>
```

| Group | The journeys of the browser | The runs of axe |
|---|---|---|
| `IDENT` | 12 passed (6.1s) | 6 passed (5.7s) |
| `PROJ` | 15 passed (14.5s) | 7 passed (7.9s) |
| `CAP` | 12 passed (8.4s) | 5 passed (8.5s) |
| `CRED` | 8 passed (5.5s) | 5 passed (6.1s) |
| `TASK` | 14 passed (13.2s) | 9 passed (8.9s) |
| `PART` | 11 passed (2.5m) | 6 passed (7.1s) |
| `EVID` | 10 passed (2.7m) | 4 passed (5.4s) |

Every group ran with the exit code 0 and no failure. The group `TASK` includes the test `TASK2SuccessAVisitorFiltersThePublicTasksByModeAndRequirement`, which the filter `MOD` also matches.

### 9.7 The planning checks

### `python3 scripts/verify_bootstrap.py`

```
PASS planning artifacts: 12 changes, 37 requirements, 111 scenarios, 36 unchecked tasks; token pin and 12 contrast pairs verified
Product/runtime/browser/deployment verification: NOT PERFORMED
```

### `node scripts/check-openspec-change-names.mjs`

```
OpenSpec change names: PASS
```

### `openspec validate --all --strict --no-interactive`

```
✓ spec/capability-directory
✓ spec/credit-ledger
✓ spec/evidence-findings
✓ spec/identity-profiles
✓ change/moderation-arbitration
✓ change/notification-delivery
✓ spec/participation-execution
✓ change/pilot-operations
✓ spec/project-workspaces
✓ change/reputation-history
✓ spec/runtime-foundation
✓ spec/task-publication
Totals: 12 passed, 0 failed (12 items)
```

The exit code of every command is 0. The second line of the first command is the honest answer of the script: the product, the runtime, the browser and the deployment are not verified by it. The 36 unchecked tasks are the tasks of the change; the boxes of `tasks.md` were not ticked by this half, because the archive of the package is the work of the owner of the change.

### 9.8 The ledger of the bonus and of the appeal that reverses it

The journey `MOD-UX the moderator awards a bonus with the base and the parties read the reason of the case` ran in the database `bm_MOD3`. The task of the case holds a base of 2, a bonus of the slot of 2 and one slot, so the budget of the task is 4. The entries of the case, in the order they were written:

| The transaction | The bucket | The amount |
|---|---|---|
| `reserve` | the available of the publisher | -4 |
| `reserve` | the reserved of the publisher, `task:{id}` | +4 |
| `begin_pending` (the base of the seat, the participation package) | the reserved of the publisher | -2 |
| `begin_pending` (the base of the seat, the participation package) | the pending of the contributor | +2 |
| `begin_pending` (the bonus of the decision) | the reserved of the publisher | -1 |
| `begin_pending` (the bonus of the decision) | the pending of the contributor | +1 |
| `arbitration` (the base and the bonus) | the pending of the contributor | -3 |
| `arbitration` (the base and the bonus) | the available of the contributor | +3 |
| `compensation` (the appeal) | the available of the contributor | -3 |
| `compensation` (the appeal) | the available of the publisher | +3 |

The signed sum of every transaction of the case is 0, and the balance of no account of a contributor or of a publisher is below zero. The row of the case keeps `awarded_amount = 3` and the kind of the last transaction `compensation`, and the answer of the case carries `awarded_amount = 3` and `ledger_amount = 3`.

The balances of the case:

| The moment | The available of the publisher | The reserved of the task | The pending of the contributor | The available of the contributor |
|---|---|---|---|---|
| before the decision | 28 | 4 | 2 | 66 |
| after the award of the base and of the bonus | 28 | 1 | 0 | 69 |
| after the appeal that reverses the decision | 31 | 1 | 0 | 66 |

The publisher holds 32 before the case and 32 after the appeal, and the reserved bucket of the task keeps the rest of the funded escrow.

The test of the backend proves the same movement on a bigger case: a base of 10, a bonus of 5, a reserved escrow of 20 and a grant of 50.

| The moment | The available of the contributor | The pending of the contributor | The available of the publisher | The reserved of the publisher |
|---|---|---|---|---|
| before the decision | 0 | 10 | 30 | 20 |
| after the award of the base and of the bonus | 15 | 0 | 30 | 15 |
| after the appeal that reverses the decision | 0 | 0 | 45 | 15 |

The publisher holds 60 after the appeal, the same total as a case without a bonus, and the signed sum of every transaction of the journal is 0.

### 9.9 The checks that did not run

| Check | Why |
|---|---|
| The journeys and the runs of axe of the Firefox and of the WebKit | The runtimes are not installed on this machine. Only the Chromium browser is installed. |
| `forge gate .` | The Forge binary is not on the path of the machine. |
| The smoke of the release of the product | The release pipeline of the product does not exist yet. |
| `npm --prefix apps/web ci` | The packages of the run were already installed and the lock file did not change. The lint, the type check, the build and the check of the client ran against them. |

### 9.10 The defects found in this half

| Finding | Where | What was done |
|---|---|---|
| The test of the appeal of the first half compared the lines of the trail by their position, and the clock of the test stands still, so the two decisions of a case carry the same moment and the order of the lines falls back to their random identifier. The comparison held only in half of the runs. | `tests/Blindmesh.Tests/Moderation/MOD1ArbitrationTests.cs`, the test `MOD1BoundaryTheAppealReachesAnotherModeratorAndThePriorRowsStayImmutable` | The line of the first decision is now found by its identifier and compared with itself. The test of this half compares the rows of the ledger by their identifier as well. |
| The answer of a case left out the reason of the case and the note of a request of more evidence, although the service built both. | The finding of the first half, in the section 7 above | Closed by this half: the answer of a case carries `reason` and `evidence_note`, and the page of a case shows both. |
| The design named a bonus in the decision of a case and the contract of the API had no such field. | The finding of the first half, in the section 8 above | Closed by this half: the body of the decision carries the bonus, and the page of the queue of the cases offers it. |
