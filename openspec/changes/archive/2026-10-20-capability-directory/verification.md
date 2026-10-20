# Verification: CAP

Status: RUN — the capability package is implemented, built and verified. Every command below was executed against the real API, the isolated PostgreSQL 17 of the test run and the compose stack. The evidence is the observed output, not a plan.

## Commands and observed result

1. `dotnet restore Blindmesh.sln --locked-mode`: PASS. Six projects restore from the committed `packages.lock.json` files. Every restore prints `warning NU1900` for the public platform feed, because that feed is unreachable. The warning is the visible evidence of the blocked platform adoption and was not hidden.
2. `dotnet format Blindmesh.sln --verify-no-changes`: PASS, exit code 0. No file needs formatting.
3. `dotnet build Blindmesh.sln -c Release --no-restore`: PASS. Five product projects and one test project, 0 errors, 5 warnings (the `NU1900` warnings of the unreachable feed).
4. `dotnet test Blindmesh.sln -c Release --no-build`: PASS. 59 tests, 0 failed, against an isolated PostgreSQL 17 container started by Testcontainers. `dotnet test Blindmesh.sln -c Release --no-build --filter FullyQualifiedName~CAP` runs the 12 tests of this package alone: 12 passed, 0 failed.
5. `npm --prefix apps/web ci`: PASS. The lock file is committed and unchanged; the packages are installed from it.
6. `npm --prefix apps/web run lint`, `run typecheck`, `run build`: PASS. No warning, no error, Next.js 16.4.2. The build lists the three new routes `/capabilities`, `/settings/capabilities` and `/tasks/[id]/eligibility`.
7. `npm --prefix apps/web run api:check`: PASS. The committed `packages/api-client/openapi.json` and `src/generated/schema.ts` match a fresh generation, byte for byte. The document owns the seven capability paths; the local OAuth fixture is not part of the published contract.
8. `docker compose -f infra/compose.dev.yaml up -d --build`: PASS. PostgreSQL 17.11, the object storage, the mail sink, the API, the worker, the web application and the Caddy proxy start with the code of this package. `dotnet run --project src/Blindmesh.Api -- migrate` applied `20261020034947_AddCapabilityDirectory` to the schema of the previous package: the three users and the 120 projects of the previous snapshot stayed in the database, and the three new tables `capabilities`, `capability_tags` and `capability_status_history` were added with their indexes and constraints. The fresh schema of the test run applies the whole stream without a gap, so both the fresh schema and the upgrade are covered.
9. `npm --prefix apps/web run test:e2e -- --project=chromium --grep CAP`: PASS. 12 browser journeys. The sign in journeys need the local OAuth fixture, so the API runs in the test mode and the web application points at it. The ports 58090 and 53100 were taken by another process on this machine, so the run of this package used the free ports 58091 and 53101 with the same configuration:

   ```sh
   ASPNETCORE_URLS=http://127.0.0.1:58091 APP_ENV=test \
     Blindmesh__Identity__LocalFixtureEnabled=true \
     Blindmesh__Identity__PublicBaseUrl=http://127.0.0.1:53101 \
     Blindmesh__Identity__CookieSecurity=Development \
     Api__AllowedOrigins__0=http://127.0.0.1:53101 \
     Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=blindmesh;Username=blindmesh;Password=blindmesh" \
     dotnet run --project src/Blindmesh.Api --no-launch-profile &

   BLINDMESH_API_URL=http://127.0.0.1:58091 npm --prefix apps/web run start -- -p 53101 &
   BLINDMESH_WEB_URL=http://127.0.0.1:53101 npm --prefix apps/web run test:e2e -- --project=chromium --grep CAP
   ```

   The journeys of the identity package and of the project package still pass against the same hosts with `--grep IDENT` and `--grep PROJ`.
10. `npm --prefix apps/web run test:a11y -- --grep CAP`: PASS. 5 axe runs, no violation in the light theme, in the dark theme, at 360 pixels, on the directory, on the settings page and on the eligibility route.
11. `python3 scripts/verify_bootstrap.py`: PASS. 12 changes, 37 requirements, 111 scenarios, 81 unchecked tasks; the shared token SHA256 and the 12 contrast pairs are verified.
12. `node scripts/check-openspec-change-names.mjs`: PASS.
13. `openspec validate --all --strict --no-interactive`: 12 passed, 0 failed.

## Scenario evidence

| Scenario | What the run proved | Test |
|---|---|---|
| CAP-1 success | Bob declares a public device with the tags `arm64` and `sequoia` and the identifiers `os=macos` and `os_version=15.1`. The answer holds the kind, the label, the visibility, the state `declared`, the verification `self_declared`, `usable_for_matching` true and the version 1. The database holds one capability row, two tag rows and no status history row, and the declaration is in the directory of a visitor without a session. | `Cap1DeclarationTests.CAP1SuccessAContributorDeclaresADeviceCapabilityAndTheDirectoryHoldsIt` |
| CAP-1 rejection | A credential in a field of the body answers 400 and the body never holds the value. A credential in the metadata answers 422 with the field error `metadata`, the message names only the allowed keys and the value never appears in the answer. No capability row and no tag row is written. | `Cap1DeclarationTests.CAP1RejectionACredentialInADeclarationIsRefusedWithoutAStoreAndWithoutAnEcho` |
| CAP-1 boundary | The withdrawal of a declaration answers 200 with the state `withdrawn`, the version 2 and one status history row `declared` to `withdrawn`. The capability row, its tag row and its history row stay in the database, the declaration leaves the match and it is not in the directory any more, while the owner still reads it in the own list. | `Cap1DeclarationTests.CAP1BoundaryAWithdrawnDeclarationKeepsItsRowAndItsHistoryAndLeavesTheMatch` |
| CAP-1 rejection | A private declaration of another contributor answers 404 for the read and for the change, and the stored label and version stay as they were. A public declaration of another contributor is visible but answers 403 for the change. | `Cap1DeclarationTests.CAP1RejectionAContributorMayNotReadOrChangeAPrivateDeclarationOfAnotherContributor` |
| CAP-1 boundary | A stale `expected_version` answers 409 with the code `version_conflict`, and the stored label and version stay as they were. | `Cap1DeclarationTests.CAP1BoundaryAStaleVersionIsRefusedAndTheDeclarationKeepsItsValue` |
| CAP-1 rejection | A visitor without a session answers 401 for the create command and no capability row is written. | `Cap1DeclarationTests.CAP1RejectionAContributorWithoutASessionMayNotDeclareACapability` |
| CAP-2 success | Bob declares a public region `Berlin`, a public device `MacBook Pro 14` and a public role `tester`. The evaluation of the conditions `region=Berlin`, `device=macbook pro 14` and `role=tester` answers eligible with every condition satisfied and a reason that names the declaration. The same state and the same conditions answer the same body. | `Cap2EligibilityTests.CAP2SuccessAnEligibleContributorMeetsEveryHardCondition` |
| CAP-2 rejection | Cara declares a public device and a private region. The evaluation of `region=Paris` and `device=Surface Laptop` answers not eligible: the region condition is not satisfied and its reason names the region and the value without naming the private declaration; the device condition is satisfied. The answer counts one declaration that can be used for a match. | `Cap2EligibilityTests.CAP2RejectionAContributorWithoutTheRequiredRegionGetsAnActionableReason` |
| CAP-2 boundary | The evaluation after the preview reads the committed state: after the declaration became unavailable the same condition answers not eligible with the reason that no public and available device declaration matches. The evaluation never trusts the answer of the preview. | `Cap2EligibilityTests.CAP2BoundaryTheEvaluationUsesTheCommittedStateAndNotACachedPreview` |
| CAP-2 rejection | A condition of a kind that is not region, device or role answers 422 with the code `unprocessable_entity`. | `Cap2EligibilityTests.CAP2RejectionAConditionWithAnUnknownKindIsRefused` |
| CAP-2 boundary | The evaluation of a task that is not published answers 404 with the code `not_found`; the body holds no condition and no task id, so the module never invents a task. | `Cap2EligibilityTests.CAP2BoundaryTheEvaluationOfATaskIsNotFoundBecauseNoTaskIsPublished` |
| CAP-2 rejection | A visitor without a session answers 401 for the evaluation of a task. | `Cap2EligibilityTests.CAP2RejectionAContributorWithoutASessionMayNotEvaluateAnEligibility` |
| CAP-UX complete journey | The browser signs in through the local fixture, declares a capability, finds it in the directory with the note that it can be matched against a task, withdraws it with a confirmation and reads the history of the withdrawal; the withdrawn declaration is not in the directory any more. | `journey.spec.ts`: the contributor declares a capability and finds it in the directory, the contributor withdraws a declaration and it keeps its history |
| CAP-UX recover from failure | A declaration without a label is refused with the message of the field, the field is marked `aria-invalid` and takes the focus, and the draft of the contributor survives the refusal. A private declaration of another contributor is in no answer of the directory. The own list of a contributor without a declaration explains the next action. The settings route offers the sign in to a visitor without a session. The failure state of the API explains the failure and offers a retry. | `journey.spec.ts`: an invalid declaration is marked and takes the focus, a private declaration of another contributor stays invisible, the own list of a contributor without a declaration explains the next action, the settings route offers the sign in to a visitor without a session, the failure state explains the failure and offers a retry |
| CAP-UX responsive keyboard operation | The three routes run at 360, 768 and 1440 pixels without a horizontal scroll, the declaration works with the keyboard only, the dark theme renders the same journey and the five axe runs report no violation. | `journey.spec.ts`: the journey has no horizontal overflow at 360, 768 and 1440 pixels, the journey works with the keyboard only, the journey works in the dark theme; `accessibility.a11y.ts` (5 runs) |
| CAP-UX complete journey | The eligibility route shows the honest state of a task that is not published and the same evaluation for a condition that the contributor names: the satisfied condition answers that the contributor meets every condition, the unmet condition answers that the contributor does not meet every condition and names the missing declaration. | `journey.spec.ts`: the eligibility route shows the honest state of a task that is not published, the eligibility check names the satisfied and the unmet condition |

## The task that does not exist yet

The task publication package is not implemented, so the marketplace holds no published task with hard conditions. The decision of this package is:

- The evaluation itself belongs to the capabilities module. It is deterministic and it is exposed through `POST /v1/capabilities/eligibility`, so it is provable and usable without a task.
- The conditions of a task reach the module through the seam `ITaskConditionReader`. The only implementation of this package, `NoPublishedTaskConditionReader`, reports that no condition is available. The task publication package registers its own reader, and `GET /v1/capabilities/tasks/{task_id}/eligibility` then answers the evaluation of that task.
- Until then the endpoint answers 404, the route `/tasks/{id}/eligibility` shows the honest state of a task that does not exist, and no part of the task package is implemented.

The decision is recorded in `docs/architecture.md` under "The eligibility evaluation and the task that does not exist yet".

## Fixtures and limits

- The local OAuth fixture is the only sign in provider of this run. It is enabled with `APP_ENV=test` and `Blindmesh:Identity:LocalFixtureEnabled`, and a host that asks for it in another environment refuses to start. The seeded accounts are alice (owner), bob (eligible contributor), cara (ineligible), dana (moderator) and erin (administrator).
- The browser journey needs a plain HTTP host, so the session uses the explicitly non-__Host development cookie and the web address of the browser is listed in `Api:AllowedOrigins`. The production cookie mode stays the default.
- The ports 58090 and 53100 of the documented local run were taken by another process on this machine. The run of this package used the ports 58091 and 53101 with the same configuration. The process on 58090 was not touched.
- The browser journeys of this package declare public capabilities, so the directory of a visitor is not empty after a run. The journeys address the own declarations by their label instead of by their position.
- The release smoke on Firefox and WebKit did not run. Only the Chromium browser is installed in this environment.
- The `forge gate .` command did not run: the Forge binary is not on PATH in this environment. The native checks above are the evidence.

## Independent re-run by the integrator

The integrator repeated the whole command set against the working tree of this package after the implementer finished. The results were the same: 59 xUnit tests passed, the .NET chain was clean, the web chain (lint, typecheck, build, `api:check`) passed, the browser journeys of the identity, the project and the capability package passed 37 of 37, the axe runs of the three packages passed 17 of 17, the FOUND journeys passed 8 of 8 and the FOUND axe runs 4 of 4 against the compose stack.

The integrator used the documented ports 58090 and 53100 with a freshly built web application, after warming the first request. The cold start of the web application makes the first sign in journey wait in the "Checking your session." state, exactly as it did in the project package; the journey passes in the warm run.

