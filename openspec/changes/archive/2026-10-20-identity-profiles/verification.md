# Verification: IDENT

Status: RUN — the identity package is implemented, built and verified. Every command below was executed against the real API, the isolated PostgreSQL 17 of the test run and the compose stack. The evidence is the observed output, not a plan.

## Commands and observed result

1. `dotnet restore Blindmesh.sln --locked-mode`: PASS. Six projects restore from the committed `packages.lock.json` files. Every restore prints `warning NU1900` for the public platform feed, because that feed is unreachable. The warning is the visible evidence of the blocked platform adoption and was not hidden.
2. `dotnet format Blindmesh.sln --verify-no-changes`: PASS, exit code 0. No file needs formatting.
3. `dotnet build Blindmesh.sln -c Release --no-restore`: PASS. Five product projects and one test project, 0 errors, 5 warnings (the `NU1900` warnings of the unreachable feed).
4. `dotnet test Blindmesh.sln -c Release --no-build`: PASS. 36 tests, 0 failed, against an isolated PostgreSQL 17 container started by Testcontainers. `dotnet test Blindmesh.sln -c Release --no-build --filter FullyQualifiedName~IDENT` runs the 15 tests of this package alone: 15 passed, 0 failed.
5. `npm --prefix apps/web ci`: PASS. The lock file is committed and unchanged.
6. `npm --prefix apps/web run lint`, `run typecheck`, `run build`: PASS. No warning, no error, Next.js 16.4.2.
7. `npm --prefix apps/web run api:check`: PASS. The committed `packages/api-client/openapi.json` and `src/generated/schema.ts` match a fresh generation, byte for byte. The document owns the seven identity paths; the local OAuth fixture is not part of the published contract.
8. `docker compose -f infra/compose.dev.yaml up -d --build`: PASS. PostgreSQL 17.11, the object storage, the mail sink, the API, the worker, the web application and the Caddy proxy start. `dotnet run --project src/Blindmesh.Api -- migrate` applied `20261020001654_AddIdentityProfiles` to the schema of the previous package, and the fresh schema of the test run applies the whole stream without a gap.
9. `npm --prefix apps/web run test:e2e -- --project=chromium --grep IDENT`: PASS. 12 browser journeys. The sign in journeys need the local OAuth fixture, so the API runs in the test mode and the web application points at it:

   ```sh
   ASPNETCORE_URLS=http://127.0.0.1:58090 APP_ENV=test \
     Blindmesh__Identity__LocalFixtureEnabled=true \
     Blindmesh__Identity__PublicBaseUrl=http://127.0.0.1:53100 \
     Blindmesh__Identity__CookieSecurity=Development \
     Api__AllowedOrigins__0=http://127.0.0.1:53100 \
     Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=blindmesh;Username=blindmesh;Password=blindmesh" \
     dotnet run --project src/Blindmesh.Api --no-launch-profile &

   BLINDMESH_API_URL=http://127.0.0.1:58090 npm --prefix apps/web run start -- -p 53100 &
   BLINDMESH_WEB_URL=http://127.0.0.1:53100 npm --prefix apps/web run test:e2e -- --project=chromium --grep IDENT
   ```

   The journeys of the foundation run against the compose stack and pass there as well: 8 journeys with `--grep FOUND`.
10. `npm --prefix apps/web run test:a11y -- --grep IDENT`: PASS. 6 axe runs, no violation in the light theme, in the dark theme, at 360 pixels, on the onboarding, on the public profile and on the profile that does not exist. The 4 axe runs of the foundation still pass.
11. `python3 scripts/verify_bootstrap.py`: PASS. 12 changes, 37 requirements, 111 scenarios, 99 unchecked tasks; the shared token SHA256 and the 12 contrast pairs are verified.
12. `node scripts/check-openspec-change-names.mjs`: PASS.
13. `openspec validate --all --strict --no-interactive`: 12 passed, 0 failed.

## Scenario evidence

| Scenario | What the run proved |
|---|---|
| IDENT-1 success | The callback answers 302 to the allowlisted path and sets the session cookie. The session row exists and holds only the 64 character hash of the token. The contributor and the provider identity exist. No table of the package holds a provider access token. |
| IDENT-1 rejection | A replayed state, an expired state and a callback without the pre-auth cookie are refused and create no session. The refused sign in answers `/sign-in` with a coarse slug and no private field. |
| IDENT-1 boundary | A banned contributor loses the access on the next request: the session read answers 401 and the body holds no email and no provider field. |
| IDENT-2 success | The profile is saved, the version advances by one, the row holds the change and the public page carries the display name, the bio, the time zone, the tags and the join date, and no email, no provider identity, no role, no account id and no session. |
| IDENT-2 rejection | A stale `expected_version` answers 409 and leaves the stored profile untouched. A foreign account answers 403. An invalid field answers 422 with the field errors and leaves the version untouched. |
| IDENT-2 boundary | A profile that does not exist and a private profile answer with the same code and the same message, so the answer never reveals that an account exists. |
| IDENT-UX complete journey | The browser signs in through the local fixture, lands on the onboarding, saves the profile, sees the committed version and finds the public page. The navigation shows the state of the session. |
| IDENT-UX recover from failure | An invalid field is marked, the message is associated with the field and the first field with an error takes the focus. The draft of the contributor is kept. An unavailable dependency and a missing profile show an actionable explanation. |
| IDENT-UX responsive keyboard operation | The journey runs at 360, 768 and 1440 pixels without a horizontal scroll, the navigation collapses behind the menu button on a small screen, the journey works with the keyboard only, the dark theme renders the same journey and the axe runs report no violation. |

## Fixtures and limits

- The local OAuth fixture is the only sign in provider of this run. It is enabled with `APP_ENV=test` and `Blindmesh:Identity:LocalFixtureEnabled`, and a host that asks for it in another environment refuses to start. The seeded accounts are alice (owner), bob (eligible contributor), cara (ineligible), dana (moderator) and erin (administrator).
- The browser journey needs a plain HTTP host, so the session uses the explicitly non-__Host development cookie and the web address of the browser is listed in `Api:AllowedOrigins`. The production cookie mode stays the default.
- The proxy of the web application drops the `Location` of a redirect, so the sign in chain of the browser could not follow the provider. The proxy now passes the location through, and the journeys prove the whole chain.
- The grant eligibility of the seeded accounts belongs to the credit package. The identity package stores the provider email and does not decide the grant.
- The release smoke on Firefox and WebKit did not run. Only the Chromium browser is installed in this environment.
- The `forge gate .` command did not run: the Forge binary is not on PATH in this environment. The native checks above are the evidence.
