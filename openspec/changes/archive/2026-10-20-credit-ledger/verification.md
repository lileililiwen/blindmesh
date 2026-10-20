# Verification: CRED

Status: RUN — the credit package is implemented, built and verified. Every command below was executed against the real API, the isolated PostgreSQL 17 of the test run and the compose stack. The evidence is the observed output, not a plan.

## Commands and observed result

1. `dotnet restore Blindmesh.sln --locked-mode`: PASS. Six projects restore from the committed `packages.lock.json` files. Every restore prints `warning NU1900` for the public platform feed, because that feed is unreachable. The warning is the visible evidence of the blocked platform adoption and was not hidden.
2. `dotnet format Blindmesh.sln --verify-no-changes`: PASS, exit code 0. No file needs formatting.
3. `dotnet build Blindmesh.sln -c Release --no-restore`: PASS. Five product projects and one test project, 0 errors, 5 warnings (the `NU1900` warnings of the unreachable feed).
4. `dotnet test Blindmesh.sln -c Release --no-build`: PASS. 73 tests, 0 failed, against an isolated PostgreSQL 17 container started by Testcontainers. `dotnet test Blindmesh.sln -c Release --no-build --filter FullyQualifiedName~CRED` runs the 14 tests of this package alone: 14 passed, 0 failed.
5. `npm --prefix apps/web ci`: PASS. The lock file is committed and unchanged; the packages are installed from it.
6. `npm --prefix apps/web run lint`, `run typecheck`, `run build`: PASS. No warning, no error, Next.js 16.4.2. The build lists the new routes `/wallet` and `/wallet/history`.
7. `npm --prefix apps/web run api:check`: PASS. The committed `packages/api-client/openapi.json` and `src/generated/schema.ts` match a fresh generation, byte for byte. The document owns the three wallet paths; the local OAuth fixture is not part of the published contract.
8. `docker compose -f infra/compose.dev.yaml up -d --build`: PASS. PostgreSQL 17.11, the object storage, the mail sink, the API, the worker, the web application and the Caddy proxy start with the code of this package. `dotnet run --project src/Blindmesh.Api -- migrate` applied `20261020055413_AddCreditLedger` to the schema of the previous package: the rows of the users, the projects and the capabilities stayed in the database and the tables `ledger_accounts`, `ledger_transactions`, `ledger_entries`, `credit_lots`, `idempotency_records` and `grant_claims` were added with their indexes and constraints. The fresh schema of the test run applies the whole stream without a gap, so both the fresh schema and the upgrade are covered. `Cred1MigrationTests.Cred1TheMigrationAddsTheTablesOfThePackageAndKeepsTheRowsOfTheEarlierPackages` walks the same upgrade in a second database of the test container.
9. `npm --prefix apps/web run test:e2e -- --project=chromium --grep CRED`: PASS. 8 browser journeys. The sign in needs the local OAuth fixture, so the API runs in the test mode and the web application points at it. The ports 58090 and 53100 were taken by another process on this machine during the implementer run, so that run used the free ports 58091 and 53101 with the same configuration:

   ```sh
   ASPNETCORE_URLS=http://127.0.0.1:58091 APP_ENV=test \
     Blindmesh__Identity__LocalFixtureEnabled=true \
     Blindmesh__Identity__PublicBaseUrl=http://127.0.0.1:53101 \
     Blindmesh__Identity__CookieSecurity=Development \
     Api__AllowedOrigins__0=http://127.0.0.1:53101 \
     Blindmesh__Database__ConnectionString="Host=127.0.0.1;Port=55432;Database=blindmesh;Username=blindmesh;Password=blindmesh" \
     dotnet run --project src/Blindmesh.Api --no-launch-profile &

   BLINDMESH_API_URL=http://127.0.0.1:58091 npm --prefix apps/web run start -- -p 53101 &
   BLINDMESH_WEB_URL=http://127.0.0.1:53101 npm --prefix apps/web run test:e2e -- --project=chromium --grep CRED
   ```

   The journeys of the identity package, of the project package and of the capability package still pass against the same hosts with `--grep IDENT`, `--grep PROJ` and `--grep CAP`.
10. `npm --prefix apps/web run test:a11y -- --grep CRED`: PASS. 5 axe runs, no violation in the light theme, in the dark theme, at 360 pixels, on the wallet and on the wallet history.
11. `python3 scripts/verify_bootstrap.py`: PASS. 12 changes, 37 requirements, 111 scenarios, the shared token SHA256 and the 12 contrast pairs are verified.
12. `node scripts/check-openspec-change-names.mjs`: PASS.
13. `openspec validate --all --strict --no-interactive`: 12 passed, 0 failed.

## Scenario evidence

| Scenario | What the run proved | Test |
|---|---|---|
| CRED-1 success | A reservation and its settlement write two balanced transactions. The sum of the entries of every transaction is zero, the bucket of the contributor holds the reserved amount and the balance of the journal rebuilds the available, the reserved and the pending amount. | `Cred1ConservationTests.Cred1SuccessAReservationAndASettlementKeepTheJournalBalanced` |
| CRED-1 rejection | Two commands that would overspend the same bucket run at the same time. The one that fits commits, the other answers a conflict and the journal holds no partial entry. | `Cred1ConservationTests.Cred1RejectionTheConcurrentOverspendCommitsOnceAndTheOtherAnswersAConflict` |
| CRED-1 rejection | The wallet of another contributor answers 404 and holds no amount of the contributor. | `Cred1ConservationTests.Cred1RejectionTheWalletOfAnotherContributorIsNotFoundAndHoldsNoAmount` |
| CRED-1 boundary | A command that timed out after the commit is retried with the same key and the same payload. The original answer comes back and the journal holds no second entry. | `Cred1ConservationTests.Cred1BoundaryTheRetriedKeyReturnsTheOriginalAnswerWithoutASecondEntry` |
| CRED-1 boundary | A money command without an idempotency key is refused and no entry is written. | `Cred1ConservationTests.Cred1BoundaryTheGrantCommandWithoutAKeyIsRefusedWithoutAnEntry` |
| CRED-1 boundary | The migration of the package adds its tables and keeps the rows of the users, the projects and the capabilities of the earlier packages. | `Cred1MigrationTests.Cred1TheMigrationAddsTheTablesOfThePackageAndKeepsTheRowsOfTheEarlierPackages` |
| CRED-2 success | An eligible account claims the grant. Exactly 60 credits appear, the lot carries the expiry date of 30 days and the transaction of the grant is in the wallet. | `Cred2GrantTests.Cred2SuccessAnEligibleAccountClaimsExactlySixtyCreditsWithTheExpiry` |
| CRED-2 rejection | The second claim of the same provider subject issues nothing and the answer says that the grant was already claimed. | `Cred2GrantTests.Cred2RejectionTheSecondClaimOfTheSameSubjectIssuesNothing` |
| CRED-2 rejection | An account without a verified email and an account that is too young are refused with the reason of every missing condition. | `Cred2GrantTests.Cred2RejectionTheAccountWithoutAVerifiedEmailOrTooYoungIsRefusedWithTheReason` |
| CRED-2 rejection | A claim without the accepted rules is refused and nothing is issued. | `Cred2GrantTests.Cred2RejectionTheGrantWithoutTheAcceptedRulesIsRefused` |
| CRED-2 rejection | The daily cap of the platform holds: the issuance stops at the cap and the answer names the condition. | `Cred2GrantTests.Cred2RejectionTheDailyCapOfThePlatformHolds` |
| CRED-2 rejection | The daily limit of an address holds: the claims of one address stop at the limit. | `Cred2GrantTests.Cred2RejectionTheDailyLimitOfAnAddressHolds` |
| CRED-2 boundary | A reserved grant credit that expires before the payment becomes an earned credit that never expires, so the balance of the contributor stays whole. | `Cred2GrantTests.Cred2BoundaryAnExpiredReservedGrantIsPaidAsAnEarnedCreditThatNeverExpires` |
| CRED-2 boundary | A reserved grant credit that expires before the refund goes to the expiry sink, and no balance ever goes negative. | `Cred2GrantTests.Cred2BoundaryAnExpiredRefundGoesToTheExpirySinkAndNoBalanceGoesNegative` |
| CRED-UX complete journey | The browser signs in through the local fixture, claims the grant with the acceptance of the rules, sees the 60 credits, the expiry of the lot and the transaction of the grant in the wallet history. A second run of the journey finds the grant already claimed and the page says so. | `journey.spec.ts`: the contributor claims the grant and sees the credits with their expiry, the history of a wallet without a transaction explains the next action |
| CRED-UX recover from failure | The account that is too young and without a verified email sees the reason of every missing condition and no claim button. The wallet of a visitor without a session offers the sign in. A failure of the API shows the failure and offers a retry. | `journey.spec.ts`: the account that is too young is told why the grant is not available, the wallet of a visitor without a session offers the sign in, the failure state explains the failure and offers a retry |
| CRED-UX responsive keyboard operation | The two routes run at 360, 768 and 1440 pixels without a horizontal scroll, the claim works with the keyboard only, the dark theme renders the same journey and the five axe runs report no violation. | `journey.spec.ts`: the journey has no horizontal overflow at 360, 768 and 1440 pixels, the journey works with the keyboard only, the journey works in the dark theme; `accessibility.a11y.ts` (5 runs) |

## The decisions of this package

- The Credits module owns `ledger_accounts`, `ledger_transactions`, `ledger_entries`, `credit_lots`, `idempotency_records` and `grant_claims`, and only the Credits module writes a ledger row. The entries are append only and a correction is a compensating transaction.
- The public API of this package is the wallet, the wallet history and the grant. There is no public transfer and no balance editor. The escrow primitives — reserve, settle and refund — are internal methods of `ICreditService`; the task publication and the evidence packages that will use them are not implemented, so the primitives are proven by the xUnit tests through the application service and not through a public route.
- The age of the account that decides the grant is the age of the account at the provider, because the provider owns that date. The identity module stores the creation date of the provider account on the provider identity and keeps it in step with the provider at every sign in. The creation of the product account is the fallback for a provider that reports no date. The date of the provider account is stable, so the grant of an account does not change when the row of the product account happens to be created.
- The grant is at most 60 credits once per provider subject. The platform issues at most 6000 credits in 24 hours and one address makes at most 5 claims in 24 hours. The grant expires after 30 days. A reserved grant credit that expires before the payment becomes an earned credit that never expires, and an expired refund goes to the expiry sink, so no balance ever goes negative.
- The money commands use a scoped `Idempotency-Key` record written in the same transaction. The same key with the same payload replays the original answer; the same key with another payload answers a conflict.

## The defects the integrator found and fixed

- The grant decided the age of the account from the creation date of the product account, and the local OAuth fixture put that date into the past only when the account row was created. A contributor whose row had been created earlier was therefore too young forever, and the browser journey could only pass on a database that had never seen the account. The age now comes from the creation date of the provider account, the identity module stores it on the provider identity and refreshes it at every sign in, and the fixture declares a stable age. The migration `20261020071236_AddProviderAccountCreatedAt` adds the column. The journey now passes against a database that already holds the accounts.
- The browser journey asserted a message "You already claimed the grant of your provider account." that the interface never renders; the interface says "You claimed the grant on {date}." The journey now asserts what the page shows.
- The browser journey looked for the sign in link of the whole page and found two of them, one in the navigation and one in the wallet. It now looks in the wallet region.

## Fixtures and limits

- The local OAuth fixture is the only sign in provider of this run. It is enabled with `APP_ENV=test` and `Blindmesh:Identity:LocalFixtureEnabled`, and a host that asks for it in another environment refuses to start. The seeded accounts are alice (owner), bob (eligible contributor), cara (ineligible), dana (moderator) and erin (administrator).
- The browser journey needs a plain HTTP host, so the session uses the explicitly non-`__Host` development cookie and the web address of the browser is listed in `Api:AllowedOrigins`. The production cookie mode stays the default.
- The release smoke on Firefox and WebKit did not run. Only the Chromium browser is installed in this environment.
- The `forge gate .` command did not run: the Forge binary is not on PATH in this environment. The native checks above are the evidence.

## Independent re-run by the integrator

The integrator repeated the whole command set against the working tree of this package after the implementer finished, and found the three defects listed above. After the fix the results were: 73 xUnit tests passed, the .NET chain was clean, the web chain (lint, typecheck, build, `api:check`) passed, the browser journeys of the identity, the project, the capability and the credit package passed 45 of 45, the axe runs of the four packages passed 22 of 22, the FOUND journeys passed 8 of 8 and the FOUND axe runs 4 of 4 against the rebuilt compose stack.

