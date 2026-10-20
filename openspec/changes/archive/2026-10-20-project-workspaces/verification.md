# Verification: PROJ

Status: RUN — the project package is implemented, built and verified. Every command below was executed against the real API, the isolated PostgreSQL 17 of the test run and the compose stack. The evidence is the observed output, not a plan.

## Commands and observed result

1. `dotnet restore Blindmesh.sln --locked-mode`: PASS. Six projects restore from the committed `packages.lock.json` files. Every restore prints `warning NU1900` for the public platform feed, because that feed is unreachable. The warning is the visible evidence of the blocked platform adoption and was not hidden.
2. `dotnet format Blindmesh.sln --verify-no-changes`: PASS, exit code 0. No file needs formatting.
3. `dotnet build Blindmesh.sln -c Release --no-restore`: PASS. Five product projects and one test project, 0 errors, 5 warnings (the `NU1900` warnings of the unreachable feed).
4. `dotnet test Blindmesh.sln -c Release --no-build`: PASS. 47 tests, 0 failed, against an isolated PostgreSQL 17 container started by Testcontainers. `dotnet test Blindmesh.sln -c Release --no-build --filter FullyQualifiedName~PROJ` runs the 11 tests of this package alone: 11 passed, 0 failed.
5. `npm --prefix apps/web ci`: PASS. The lock file is committed and unchanged; 391 packages were installed from it.
6. `npm --prefix apps/web run lint`, `run typecheck`, `run build`: PASS. No warning, no error, Next.js 16.4.2.
7. `npm --prefix apps/web run api:check`: PASS. The committed `packages/api-client/openapi.json` and `src/generated/schema.ts` match a fresh generation, byte for byte. The document owns the five project paths; the local OAuth fixture is not part of the published contract.
8. `docker compose -f infra/compose.dev.yaml up -d --build`: PASS. PostgreSQL 17.11, the object storage, the mail sink, the API, the worker, the web application and the Caddy proxy start with the code of this package. `dotnet run --project src/Blindmesh.Api -- migrate` applied `20261020020127_AddProjectWorkspaces` to the schema of the previous package, and the fresh schema of the test run applies the whole stream without a gap.
9. `npm --prefix apps/web run test:e2e -- --project=chromium --grep PROJ`: PASS. 13 browser journeys. The sign in journeys need the local OAuth fixture, so the API runs in the test mode and the web application points at it:

   ```sh
   ASPNETCORE_URLS=http://127.0.0.1:58090 APP_ENV=test \
     Blindmesh__Identity__LocalFixtureEnabled=true \
     Blindmesh__Identity__PublicBaseUrl=http://127.0.0.1:53100 \
     Blindmesh__Identity__CookieSecurity=Development \
     Api__AllowedOrigins__0=http://127.0.0.1:53100 \
     Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=blindmesh;Username=blindmesh;Password=blindmesh" \
     dotnet run --project src/Blindmesh.Api --no-launch-profile &

   BLINDMESH_API_URL=http://127.0.0.1:58090 npm --prefix apps/web run start -- -p 53100 &
   BLINDMESH_WEB_URL=http://127.0.0.1:53100 npm --prefix apps/web run test:e2e -- --project=chromium --grep PROJ
   ```

   The journeys of the identity package still pass against the same hosts: 12 journeys with `--grep IDENT`.
10. `npm --prefix apps/web run test:a11y -- --grep PROJ`: PASS. 6 axe runs, no violation in the light theme, in the dark theme, at 360 pixels, on the list, on the create form, on the project page, on the settings page and on the project that may not be seen.
11. `python3 scripts/verify_bootstrap.py`: PASS. 12 changes, 37 requirements, 111 scenarios, 90 unchecked tasks; the shared token SHA256 and the 12 contrast pairs are verified.
12. `node scripts/check-openspec-change-names.mjs`: PASS.
13. `openspec validate --all --strict --no-interactive`: 12 passed, 0 failed.

## Scenario evidence

| Scenario | What the run proved | Test |
|---|---|---|
| PROJ-1 success | The owner creates a draft, activates it and records a version. The answer holds the committed state, the status history holds one row (`draft` to `active`) and the version rows hold the label and the commit reference. The database holds one status history row, one version row and one project row. | `Proj1LifecycleTests.PROJ1SuccessTheOwnerActivatesTheDraftAndTheVersionHistoryIsRecorded` |
| PROJ-1 rejection | A viewer may not change the content of the project: the answer is 403 with the code `forbidden` and the stored name, version and state stay as they were. An illegal transition of a draft answers 409 with the code `invalid_transition`, the state stays `draft`, the version stays 1 and the status history stays empty. | `Proj1LifecycleTests.PROJ1RejectionAViewerMayNotEditAndAnIllegalTransitionIsRefused` |
| PROJ-1 boundary | The archive of a project with activity keeps the versions and the history, the answer reports `accepts_tasks` false, an archived project refuses a new version and a new content with 409, and the stored project and the version rows stay as they were. | `Proj1LifecycleTests.PROJ1BoundaryTheArchiveKeepsTheActivityAndRefusesNewVersions` |
| PROJ-1 boundary | A stale `expected_version` answers 409 with the code `version_conflict` and names the current version; the stored summary and the version stay as they were. | `Proj1LifecycleTests.PROJ1BoundaryAStaleExpectedVersionIsRefusedWithTheCurrentVersion` |
| PROJ-2 success | The owner adds bob as a viewer; the member row holds the user, the role and the time. The member reads the private project and may not change it: the answer is 403 and the stored project stays as it was. | `Proj2VisibilityTests.PROJ2SuccessAMemberReadsThePrivateProjectAndMayNotChangeIt` |
| PROJ-2 rejection | An unrelated contributor gets 404 for the private project. The body holds no title, no summary, no description and no repository address, and the project is not part of the list of the unrelated contributor. | `Proj2VisibilityTests.PROJ2RejectionAnUnrelatedContributorGetsNoProjectAndNoDetail` |
| PROJ-2 boundary | The removed member loses the access by the next read: the member row is gone, the read answers 404 and the write answers 404 as well. | `Proj2VisibilityTests.PROJ2BoundaryTheRemovedMemberLosesTheAccessByTheNextRead` |
| PROJ-2 boundary | Only the owner changes the visibility. A public project is readable without a session and carries no membership of another contributor; after the change back to private the next read of the visitor answers 404. | `Proj2VisibilityTests.PROJ2BoundaryOnlyTheOwnerChangesTheVisibilityAndAPublicProjectIsReadable` |
| PROJ-2 boundary | Only the owner manages the members: an editor that adds a member answers 403 and the member rows stay as they were. | `Proj2VisibilityTests.PROJ2BoundaryOnlyTheOwnerManagesTheMembers` |
| PROJ-2 boundary | The list holds every project the caller may see exactly once, in the descending order of the creation time and the id, also when three projects share the same creation time. The cursor walks the whole list without a gap. | `Proj2VisibilityTests.PROJ2BoundaryTheListHoldsTheVisibleProjectsInAStableOrder`, `Proj2VisibilityTests.PROJ2BoundaryTheListKeepsEveryProjectWhenTheyShareTheCreationTime` |
| PROJ-UX complete journey | The browser signs in through the local fixture, creates a project, finds it in the list, publishes it, records a version and sees the version and the history on the project page. The settings page saves the content and announces the success after the server committed it. | `journey.spec.ts`: the owner creates a project and finds it in the list, the owner publishes the project and records a version, the content of a project is saved and announced |
| PROJ-UX recover from failure | An invalid repository address is marked, the message is associated with the field and the field takes the focus; the draft is kept. A project that may not be seen shows the same safe page as a project that does not exist and the page holds no title. A member without the right sees the restriction instead of the forms. The removal of a member and the archive confirm their consequences. | `journey.spec.ts`: an invalid field is marked and takes the focus, a project that may not be seen shows the safe page, a member without the right sees the restriction, the removal of a member needs a confirmation, the create form refuses a visitor without a session, the failure state explains the failure and offers a retry |
| PROJ-UX responsive keyboard operation | The journey runs at 360, 768 and 1440 pixels without a horizontal scroll, the journey works with the keyboard only, the dark theme renders the same journey and the six axe runs report no violation. | `journey.spec.ts`: the journey has no horizontal overflow at 360, 768 and 1440 pixels, the journey works with the keyboard only, the journey works in the dark theme; `accessibility.a11y.ts` (6 runs) |

## Fixtures and limits

- The local OAuth fixture is the only sign in provider of this run. It is enabled with `APP_ENV=test` and `Blindmesh:Identity:LocalFixtureEnabled`, and a host that asks for it in another environment refuses to start. The seeded accounts are alice (owner), bob (eligible contributor), cara (ineligible), dana (moderator) and erin (administrator).
- The browser journey needs a plain HTTP host, so the session uses the explicitly non-__Host development cookie and the web address of the browser is listed in `Api:AllowedOrigins`. The production cookie mode stays the default.
- The browser journey of the project list expects an empty list for a visitor. Every project of the browser journeys stays private, so the list of a visitor holds no project.
- The project member is added by the name of an existing contributor. The tests sign the contributor in first, so the contributor exists before the member is added.
- The web proxy of the API turned a `204 No Content` of the API into a `500`. The proxy now passes the answer of a response without a body through unchanged; the removal of a member and the sign out of the identity package answer with their real status.
- The matching scope of a `matched` project is owned by a later package. A matched project is readable by the owner and the members until the matching exists.
- The release smoke on Firefox and WebKit did not run. Only the Chromium browser is installed in this environment.
- The `forge gate .` command did not run: the Forge binary is not on PATH in this environment. The native checks above are the evidence.

## Independent re-run by the integrator

The integrator repeated the whole command set against the working tree of this package after the implementer finished. The results were the same: 47 xUnit tests passed, the .NET chain was clean, the web chain (lint, typecheck, build, `api:check`) passed, `IDENT|PROJ` passed 25 of 25 browser journeys, the PROJ and IDENT axe runs passed 12 of 12, the FOUND journeys passed 8 of 8 and the FOUND axe runs 4 of 4.

Two failures of a first full-suite run were observed and explained. Neither is a defect of this package:

- `FOUND-UX the home page shows the real availability of the runtime` failed against the local test-mode API because that API has no object storage and no mail sink; the home page reported the real state of those dependencies. The same journey passes 8 of 8 against the compose stack, which owns the storage and the mail sink. The foundation journeys belong to the compose stack, the identity and project journeys belong to the test-mode API.
- `IDENT-UX the sign in journey opens the onboarding of the contributor` failed on the very first browser request of a freshly started web application, because the sign in page was still in its "Checking your session." state while the first request compiled the route. The same journey passes alone and in the warm run of 25 of 25. The first request of a cold server is slow; it is a property of a cold start, not of the journey.
