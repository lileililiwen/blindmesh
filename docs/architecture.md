# Architecture and implementation contract

Decision date: 2026-10-19. Source: requirements.md §§3–15,18. These are selected implementation targets, not installed manifests or runtime evidence.

## Runtime and ownership

Use a modular monolith: one ASP.NET Core API, one .NET worker sharing domain modules, one Next.js web app, one PostgreSQL database, and S3-compatible private object storage. Do not create microservices, a generic architecture framework, Redis, search servers, Python AI, or a second frontend. C# replaces the PRD's Rust recommendation by explicit user direction on 2026-10-19; preserve requirements.md unchanged.

| Area | Selected boundary |
|---|---|
| Toolchains | C# 14, .NET SDK 10.0.400, net10.0, NuGet; Node.js 24, npm 11, TypeScript 5 |
| Web | Next.js 16 App Router, React 19, Tailwind 4, shadcn/ui (Radix), TanStack Query 5 |
| API | ASP.NET Core 10 Minimal APIs, System.Text.Json, built-in DI/logging/health checks/OpenAPI; EF Core 10 with Npgsql 10 |
| Data | PostgreSQL 17; UUID identifiers; UTC timestamptz; bigint integer credits; explicit SQL migrations |
| Files/mail | S3-compatible private object storage with AWS SDK for .NET, local MinIO; SMTP with MailKit, local Mailpit |
| Local deploy | Docker Compose v2; Caddy 2 routes /api/* to API (strips /api) and other requests to Next |
| Tests | xUnit and WebApplicationFactory; real isolated PostgreSQL integration tests; Node built-in tests for small tooling; Playwright 1 and @axe-core/playwright 4 for browser/accessibility |

These are compatibility lines, not claims about latest patches. Foundation resolves secure compatible patches, pins global.json and central NuGet versions, commits packages.lock.json and package-lock.json plus exact image versions, and records the resolution. A dependency that fails .NET/Node compatibility blocks foundation until its compatibility line is explicitly amended; do not silently upgrade a major. Foundation owns the package.json scripts listed below.

Future tree: Blindmesh.sln contains src/Blindmesh.Api, src/Blindmesh.Worker, src/Blindmesh.Domain, src/Blindmesh.Application, src/Blindmesh.Infrastructure and tests/Blindmesh.Tests. apps/web is the only web application. Domain/Application modules are Identity, Projects, Capabilities, Credits, Tasks, Findings, Moderation, Reputation and Notifications; create each module only in its owning change, without one project per module. Matching starts inside Capabilities. Shared holds IDs, actor/error types and transaction/outbox seams, not domain rules. Infrastructure/Persistence/Migrations is the sole EF Core migration stream. packages/api-client is generated; no local packages/ui until real repetition exists. infra holds Compose/Caddy configuration; tests/e2e holds browser journeys.

API composition injects domain services. Cross-domain writes use internal service functions sharing the caller's PostgreSQL transaction; API modules never update another domain's tables. Credits has exclusive ledger write ownership. Findings calls Credits; Credits never calls Findings. Worker claims bounded outbox/deadline jobs using FOR UPDATE SKIP LOCKED, applies domain methods and commits events atomically. No network call occurs inside a monetary transaction.

## Runtime foundation endpoints

The runtime foundation owns the endpoints of the shell. They are infrastructure, not a business capability, and every later package adds its own endpoints under the same prefixes.

| Endpoint | Purpose |
|---|---|
| `GET /v1/health/live` | Liveness of the process. It never reports a dependency. |
| `GET /v1/health/ready` | Readiness of the API and of the database, the object storage and the mail sink. It answers 503 with the real dependency states while one of them is unreachable. |
| `GET /v1/openapi/v1.json` | The versioned OpenAPI document. The served document does not depend on the host that produced it. |
| `POST /v1/runtime/heartbeat` | The runtime command of the foundation. It exercises the idempotency, the transaction and the outbox seams. It has no business meaning. |

The browser uses the same origin path `/api/v1`. The web application forwards it to the API, and Caddy does the same in the compose stack. Unsafe requests without an allowlisted exact `Origin` are rejected with 403 before any side effect. The session bound CSRF token check is added with the session in the identity package.

## Identity endpoints

The identity package owns these endpoints. They are under the same prefixes as the endpoints of the foundation, and the browser reaches them under `/api/v1`.

| Endpoint | Purpose |
|---|---|
| `POST /v1/auth/github/start` | Starts the GitHub sign in. It answers the address of the provider and sets the pre-auth cookie. The return path must be an allowlisted relative path. |
| `GET /v1/auth/github/callback` | Finishes the sign in. It consumes the one-use state, issues the session cookie and returns to the allowlisted relative path. A refused sign in returns to `/sign-in` with a coarse error slug. |
| `GET /v1/session` | Reads the session of the caller and issues the session and the CSRF token. A visitor without a session receives an anonymous session. |
| `POST /v1/logout` | Removes the server session of the caller. |
| `GET /v1/profile` | Reads the private profile of the signed in contributor. |
| `PATCH /v1/profile` | Saves the profile of the signed in contributor. It requires `expected_version`; a stale version answers 409, a foreign account answers 403 and a field error answers 422. |
| `GET /v1/people/{username}` | Reads the public profile of a contributor. A missing profile and a private profile answer with the same 404. |

The local OAuth fixture is reachable under `GET /v1/test/github/authorize`. It only answers with `APP_ENV=test`, and a host that asks for the fixture in another environment refuses to start.

## Project endpoints

The project package owns these endpoints. They are under the same prefixes as the endpoints of the foundation, and the browser reaches them under `/api/v1`.

| Endpoint | Purpose |
|---|---|
| `GET /v1/projects` | Lists the projects the caller may see in the list envelope `{items,next_cursor}`. The limit defaults to 20 and is capped at 100. |
| `POST /v1/projects` | Creates a project in the draft state. The owner is the caller. It accepts an optional `Idempotency-Key`; the same key with the same payload replays the original answer. |
| `GET /v1/projects/{id}` | Reads the committed state of a project with its membership, its versions and the history of its lifecycle. |
| `PATCH /v1/projects/{id}` | Saves the content or the visibility of a project. It requires `expected_version`; a stale version answers 409 with the current version. |
| `POST /v1/projects/{id}/transitions` | Applies a transition of the lifecycle. It requires `expected_version`; an illegal transition answers 409. |
| `PUT /v1/projects/{id}/members/{user_id}` | Adds a member to a project or changes the role of a member. The segment carries the id or the name of the contributor. |
| `DELETE /v1/projects/{id}/members/{user_id}` | Removes a member of a project. |
| `POST /v1/projects/{id}/versions` | Records a released version of a project. |

The fields of a project are the name (1 to 100 characters), the summary (at most 500 characters), the description (at most 10000 characters), the repository address (an absolute HTTPS address), the visibility (`public`, `private` or `matched`) and the expected version. A version carries a label of at most 100 characters and an optional commit reference.

The visibility is enforced on every read, list, detail and membership operation. A public project is readable by everybody, also without a session. A private project and a matched project are readable by the owner and the members only, because the matching scope of the marketplace is owned by a later package. A project that the caller may not see answers 404 without its title, its counts and its members. The membership is part of the answer for the owner and the members only.

The owner and the editors change the content and record the versions. Only the owner manages the members, the visibility, the pause and the archive. A member reads the project and applies for its work, but never manages it. The actor policy is checked again on every command, so a removed member and a changed visibility are honoured by the next read.

## Capability endpoints

The capability package owns these endpoints. They are under the same prefixes as the endpoints of the foundation, and the browser reaches them under `/api/v1`.

| Endpoint | Purpose |
|---|---|
| `GET /v1/capabilities` | Lists the capabilities the caller may see in the list envelope `{items,next_cursor}`. The limit defaults to 20 and is capped at 100. The query `scope=own` answers the own declarations of the contributor, including the withdrawn ones. |
| `POST /v1/capabilities` | Declares a capability of the signed in contributor. It accepts an optional `Idempotency-Key`; the same key with the same payload replays the original answer. |
| `GET /v1/capabilities/{id}` | Reads the committed state of a capability with its tags, its metadata and the history of its lifecycle. |
| `PATCH /v1/capabilities/{id}` | Saves the content, the visibility or the availability of a capability. It requires `expected_version`; a stale version answers 409 with the current version. |
| `POST /v1/capabilities/{id}/withdraw` | Withdraws a capability of the signed in contributor. It requires `expected_version`; a withdrawn capability answers 409. |
| `POST /v1/capabilities/eligibility` | Evaluates a set of hard conditions against the declarations of the signed in contributor. It answers the decision and the reason of every condition. |
| `GET /v1/capabilities/tasks/{task_id}/eligibility` | Evaluates the hard conditions of a published task against the declarations of the signed in contributor. |

The fields of a capability are the kind (`account`, `device`, `skill`, `region`, `network` or `role`), the label (1 to 100 characters), at most 20 tags, the visibility (`public` or `private`), the availability and the metadata. The metadata is a map of non sensitive identifiers; the allowlist of its keys is `os`, `os_version`, `browser`, `browser_version`, `device`, `device_model`, `region` and `provider`, and a value holds letters, digits, dots, dashes and underscores. A key outside the allowlist is refused, so a credential never reaches the database. A body that holds a field outside the allowed ones is refused as well, before anything is stored and without an echo of the refused field or of its value.

The visibility is enforced on every read, list, detail and command. A public capability is readable by everybody, also without a session. A private capability is readable by its owner only; a capability of another contributor answers 404 without its label, its tags and its metadata. Only the owner of a capability changes it, and a capability that is visible but foreign answers 403. A withdrawn capability keeps its row, its tags and its history, but it never matches a condition again.

The verification of a capability is `self_declared` until a moderator accepts the evidence of a verification. The moderation package is a later package, so this package never moves a capability to `verified` and the request bodies of the module do not accept a verification field at all.

### The eligibility evaluation and the task that does not exist yet

The task publication package is not implemented, so the marketplace holds no published task with hard conditions. The capabilities package owns the evaluation anyway, because the evaluation is the matching of a declaration against a condition and not a property of a task.

The evaluation is deterministic. It takes a set of hard conditions of the kinds `region`, `device` and `role` and compares every value with the label and the tags of the public, available and not withdrawn declarations of the contributor. The values are compared trimmed, with the inner spaces collapsed and case folded, so the spelling of the contributor does not change the result. The answer holds the decision and the reason of every condition, and the reason of a condition that is not met never names a private declaration, so a private capability of the contributor stays private even in a refusal. The account restriction of the evaluation is the ban: a banned session is refused with 401 before the evaluation runs, so a restricted account is never eligible. The evaluation never trusts the data of the client; it is computed from the committed state at the time of the request, so a declaration that changed after a preview changes the next answer.

The conditions of a task reach the module through a seam, `ITaskConditionReader`. The only implementation of this package, `NoPublishedTaskConditionReader`, reports that no condition is available, because no task is published. The task publication package registers its own reader and the endpoint `GET /v1/capabilities/tasks/{task_id}/eligibility` then answers the evaluation of the conditions of that task. Until then the endpoint answers 404, and it never invents a task, a condition or a decision.

The route `/tasks/{id}/eligibility` of the web application follows the same safe page that the project package uses for a project that may not be seen: a task that is not published shows the honest state and no eligibility. The route stays usable, because the page also offers the same evaluation for a condition that the contributor names, so the decision and its reason can be read and checked without a published task.

## Local runtime

```sh
docker compose -f infra/compose.dev.yaml up -d --build
dotnet run --project src/Blindmesh.Api -- migrate
dotnet run --project src/Blindmesh.Api
dotnet run --project src/Blindmesh.Worker
npm --prefix apps/web ci && npm --prefix apps/web run build && npm --prefix apps/web run start
```

The API, the worker and the web application read their addresses from configuration only. `Blindmesh:Database:ConnectionString`, `Blindmesh:ObjectStorage:Endpoint`, `Blindmesh:Mail:Host` and `Blindmesh:Mail:Port` are the runtime locations; `Api:AllowedOrigins` holds the exact browser origins that may send unsafe requests. The compose stack moves the host ports with the `BLINDMESH_*` variables, and the committed defaults are the documented local ports.

The migration stream is owned by `src/Blindmesh.Infrastructure/Persistence/Migrations` and is applied through the migrate command only. The `dotnet run --project src/Blindmesh.Api -- openapi --output <file>` command writes the OpenAPI document that `npm --prefix apps/web run api:generate` turns into `packages/api-client`.

## Shared architecture reconnaissance

Live evidence was inspected in the Forge checkout; formerly separate governance/platform sibling repositories can be stale after consolidation.

| Candidate | Evidence path/symbol in Forge | Reusable element and gap | Owner/release boundary | Decision |
|---|---|---|---|---|
| Governance | modules/workspace-governance/scripts/init_project.py, schemas/project.schema.json, scripts/bind_ui_gate.py | Declaration, Gate binding and policy; tooling, not business runtime | Forge governance module / workspace-governance@1 | adopt |
| UI token artifact | modules/dotnet-platform-libs/ui/packages/design-tokens/dist/tokens.css | Stack-neutral CSS; React primitives remain product-owned; source SHA 7ca6b7956b3f6b475254b4a5d0cf599724e4e1af5f0f88aba4fbccb9f38fc19b | Shared UI owner; immutable vendored bytes | adopt |
| .NET foundation | modules/dotnet-platform-libs/README.md, eng/package-manifest.json, docs/packages.md | net10.0 Platform.Core, Identity.Contracts, Authorization, Storage and Mailing contracts; application owns durable adapters and migrations | Forge .NET module; exact versioned NuGet packages | adopt after isolated feed verification |
| Shared wire schemas | contracts/manifest.json | Governance/identity/audit contracts; no Blindmesh task, finding or ledger contract | Forge contract owner, versioned artifacts | keep local |
| Project scaffolding | docs/architecture.md, src/core/manifest.rs, forge import --help | Project manifest, discovery and observed evidence; no combined Blindmesh product implementation | Forge CLI/registry | adopt |

Use exact Platform.* package versions through one defaulted PlatformPackageVersion. NuGet.config maps Platform.* exclusively to the anonymous platform feed and other packages to nuget.org. Foundation must verify isolated restore/build without a user-level local feed, then record the package/version/provenance receipt before marking adoption complete. Never copy platform implementations or add sibling ProjectReference paths. Missing feed availability blocks adoption; do not silently implement replacement frameworks. Product PostgreSQL/OAuth/S3 adapters and financial rules remain local. No Forge runtime or sibling checkout is permitted in product dependencies.

## HTTP and persistence contract

API native prefix /v1; browser prefix /api/v1 under the same origin. JSON uses snake_case. UUIDs are lowercase strings, timestamps RFC3339 UTC, credits decimal integer JSON values limited to 9,000,000,000,000 (within exact JS integers). Lists return {items,next_cursor}; cursor encodes created_at,id; limit defaults 20, maximum 100. Stable descending order, no unbounded queries. Escape user text; markdown disallows raw HTML.

Success: 200 reads/updates, 201 creates, 204 deletes. Error: {error:{code,message,field_errors?,request_id}}; 400 malformed syntax, 401 no session, 403 disallowed action on visible resource, 404 missing/invisible, 409 state/version/idempotency conflict, 413 oversized upload, 422 field validation or insufficient funds, 429 rate limit, 503 required dependency unavailable. Never expose SQL or provider details. PATCH editable resources requires expected_version; stale version returns 409 plus current version when visible.

Mutating money, publication, application, submission, review and moderation commands require Idempotency-Key (ASCII, 16–128 chars). Scope is actor+operation+key; persist canonical payload hash and original response in the same transaction as effects. Same key+payload returns original status/body; altered payload returns 409; never replay a partial commit. Financial and state-transition keys persist with audit history. Lock order: task, slot/participation, accounts sorted by UUID, ledger transaction. Retry serialization/deadlock at most 3 times; return retryable 503 on exhaustion. Never retry a provider side effect without its own delivery key.

Public objects are readable anonymously only if parent and object are public and active. matched visibility reveals only non-sensitive summary to eligible authenticated candidates; full instructions require accepted participation. private projects/tasks are owner/member/invitee-only. Project owner may add existing users as members by username with viewer/editor role; owner is the only member/visibility/archive manager. Invitees may view/apply but never manage. Task author or any project member cannot earn on that project's tasks. Capabilities separate declaration, verification and per-task eligibility; private capabilities do not enter matching.

Audit events contain actor pseudonymous ID, object IDs, transition, request/idempotency IDs and timestamp, never raw evidence or credentials. Outbox event shape: {id,type,version:1,actor_id,aggregate_id,occurred_at,payload}; recipient access is rechecked by consumers. Event producer owns payload and atomic write. Operational retention/deletion rules are in pilot-operations.

## Identity and authorization

GitHub OAuth authorization-code flow is API-owned; scopes read:user and user:email only. State is random, one-use, 10-minute expiry, bound to a pre-auth cookie. Store provider+subject unique; do not persist the GitHub access token after identity fetch. Redirect only to an allowlisted relative path. No resource-account credentials are collected.

Session cookie __Host-blindmesh, Secure/HttpOnly/SameSite=Lax/Path=/; 32-byte random opaque token stored as a hash, 7-day idle and 30-day absolute expiry. Local HTTP uses an explicitly dev-only non-__Host cookie. Unsafe methods require exact Origin and X-CSRF-Token bound to the server session; OAuth callback consumes state instead. Session and CSRF are issued by GET /v1/session. Ban/revoke takes effect on the next request; logout removes the server session. Local OAuth fixture is enabled only with APP_ENV=test and must fail startup in production.

The identity package implements this contract as follows. The session token is a 32-byte random value that only leaves the process in the cookie; the database keeps its hash. The CSRF token is derived from the session token with HMAC-SHA256, so it is bound to the server session and can be handed out with `GET /v1/session` without storing a second secret. An unsafe request that carries a session cookie must carry the matching `X-CSRF-Token`; a request without a session cookie is refused by the endpoint that needs a session. The OAuth state and the pre-auth cookie are stored as hashes and are consumed in the same transaction as the session, so a replayed, expired or mismatched state never creates a session. The tables are `users`, `external_identities`, `sessions` and `oauth_states`; the username is stored case folded and unique. A public profile carries the display name, the bio, the time zone, the skill and language tags and the join date, and never the email, the provider identity, the session or the state of the account.

Roles: user, moderator, administrator. Developer/tester/resource-provider are concurrent activities, not exclusive roles. Moderators cannot decide disputes involving their own project/account; administrators use audited reasoned actions, never a raw balance editor. Basic member management ships with project-workspaces; organizations/team billing remain deferred.

## Credit and participation invariants

Credits owns immutable, balanced entries and caches rebuildable balances. Ledger buckets: user available, publisher reserved, contributor pending, platform issuance/expiry. Every transaction's signed entry sum is zero; ordinary user buckets cannot be negative. Platform issuance is the explicitly authorized supply source. Never sum available+reserved as spendable.

User buckets track credit lots: grant (expires), earned (non-expiring). Spending reserves oldest-expiring grant lots first, then earned lots; split entry provenance is preserved. Grant expiry removes only unreserved available units. A reserved grant stays valid until resolution, becomes non-expiring earned credit when paid, and returns to available or expiry sink on refund depending on current expiry.

Default limited grant: 60 credits once per GitHub subject, only after verified email, accepted rules and account age >=7 days. 24h global issuance cap 6,000; exhausted cap returns ineligible/retry tomorrow, never overdrafts. Application records normalized provider/subject eligibility; rate limits (5 claims/IP/day plus account limit) and moderator anomaly review reduce abuse without promising perfect identity detection. Grant expires after 30 days; registration itself remains available without grant eligibility.

Publication reserves (base_reward+bonus_cap)*slot_count in one transaction with task Open; valid base 1..10,000, bonus 0..10,000, slots 1..20. No floating point, cash transfer, purchases, or withdrawals. ICreditService exposes GrantOnceAsync, ReserveTaskAsync, BeginPendingAsync, SettleAsync, RefundUnstartedAsync, ResolveDisputeAsync and ReconcileAsync. All operate within the caller's scoped DbContext/IDbContextTransaction with actor, references, long integer amounts, clock and idempotency key; reject mismatched ownership, references or budget.

Submission moves base from the assigned reservation into contributor pending; bonus stays reserved. Acceptance moves pending to available plus an optional explicit bonus <=cap, releasing unused bonus. Base pay depends on completed scope/evidence, not number of bugs. Clean exploration can be valid. A publisher cannot confiscate earned pay by rejecting a finding; disputed completion routes to moderation. Correction may be requested once for missing required evidence; contributor has 48h; if uncorrected, open a dispute rather than silently burn pay. Owner review timeout 72h auto-accepts base with zero bonus if no correction/dispute is open. All expiry/acceptance races lock the same rows.

Applications last 48h; accepted participants must start within 24h, then submit within min(task deadline, start+7 days). Publication requires deadline between now+24h and now+30 days, and a user-visible statement of deadlines. Unstarted expiry releases the assigned reservation and reopens the slot only if task is Open and still before deadline; its funds remain held for reallocation, otherwise refund them. Running work past deadline gets a 24h submission grace, then expires/refunds its unsubmitted budget. Submitted and disputed work keeps its budget until resolution. A cancellation closes admission and refunds unused slots, preserving all running/submitted/disputed work and evidence.

## State machines

| Object | Allowed transitions and authority |
|---|---|
| Project | Draft -> Active -> Paused -> Active; any non-Archived -> Archived by owner; no hard delete after activity |
| Capability | Available -> Withdrawn by owner; temporary Reserved/InUse is per participation resource lease, not the capability declaration; lease -> Released on terminal participation |
| Task | Draft -> Open on reserved publication; InProgress when any participation Running; Review when admissions closed and no active execution but submissions/disputes remain; Completed when every slot terminal and escrow zero; Cancelled only after cancellation and every active slot resolved |
| Participation | Applied -> Accepted -> Running -> Submitted -> Approved; Applied -> Declined/Withdrawn/Expired; Accepted/Running -> Withdrawn/Expired; Submitted -> ChangesRequested -> Submitted or Disputed; Submitted -> Disputed; Disputed -> Approved/Rejected by arbitration |
| Submission | Draft -> Submitted -> ChangesRequested -> Submitted or Disputed; Submitted -> Accepted; disputed completion -> Accepted/Rejected by moderator |
| Finding | Draft -> Submitted -> Triaged -> Confirmed/Rejected/Duplicate; Confirmed -> Fixing -> Resolved; regression reopens Confirmed; Duplicate requires canonical finding in same project |
| Dispute | Open -> EvidenceRequested -> Resolved -> Closed; one appeal within 7 days reopens to Open for a different moderator |

These resolve the PRD's overloaded participation Accepted label: Accepted means seat confirmed, Approved means work accepted. Task aggregate status is recomputed under lock; one lost participant cannot cancel or settle others. Applied does not occupy a slot; Accepted creates a unique active lease. Unique(task_id,user_id) prevents duplicate participation, unique(task_id,slot_number) and exclusive live lease prevent overbooking. Keep status history and version fields.

The project package implements the project row of the table as follows. The tables are `projects`, `project_members`, `project_versions` and `project_status_history`. A project starts as a draft and is activated by the owner or an editor, paused and archived by the owner only; an archived project is final, keeps its versions and its history and accepts no new work. Every applied transition is written to the status history in the same transaction as the project. The version of a project is the optimistic concurrency version of its content, its visibility and its state; a change of the membership is not part of it. The membership of a project is a set of members with the role viewer or editor; the owner is not a member row.

The capability package implements the capability row of the table as follows. The tables are `capabilities`, `capability_tags` and `capability_status_history`. A capability is declared and withdrawn by its owner; the state machine of a capability is its own and never shares a state with a task, a participation, a submission, a finding or a dispute. A withdrawn capability keeps its row, its tags and its history, and it is not deleted. Every applied transition is written to the status history in the same transaction as the capability. The version of a capability is the optimistic concurrency version of its content, its visibility and its availability. The tags of a capability are stored case folded and are unique per capability; the metadata of a capability is a JSON object on the row, and only the allowlist of the domain reaches it.

## UI contract for every change

Routes are specified in each design. Navigation: public Home/Tasks/Capabilities; authenticated Dashboard/My projects/My work/Wallet/Notifications/Profile; moderator-only Admin. Default English UI with i18n-ready labels and UTC storage plus labeled local timezone rendering; additional locales/RTL are later packages.

Use shared semantic CSS tokens and shadcn/ui primitives. Light/dark theme follows pre-paint data-theme (stored preference or media query); preserve reduced-motion. Minimum 44px pointer targets, 16px body, proper heading order, single main and skip link, associated errors, aria-live status, keyboard focus order/restoration, WCAG 2.2 AA. Measure text >=4.5:1, UI/focus >=3:1 in both themes. No color-only status.

The identity package adds the routes `/sign-in`, `/onboarding`, `/settings/profile` and `/people/{username}`. The navigation of the shell shows the state of the session: a visitor sees the sign in, a contributor sees the link to the public profile, the profile settings and the sign out. A private route without a session offers the sign in with an allowlisted return path.

The project package adds the routes `/projects`, `/projects/new`, `/projects/{id}` and `/projects/{id}/settings`. The list shows the projects the visitor may see and explains the next action when it is empty. The create form and the settings page keep the draft of the contributor after a refusal, mark the field of a validation error and give it the focus, and announce a success only after the server committed it. The settings page shows the content, the visibility, the lifecycle, the team and the versions of a project, and it explains the restriction to a member without the right to change it. The archive and the removal of a member confirm their consequences. A project that may not be seen shows the same safe page as a project that does not exist.

The capability package adds the routes `/capabilities`, `/settings/capabilities` and `/tasks/{id}/eligibility`. The directory shows the capabilities the visitor may see, marks the ones that a match uses and explains the next action when it is empty. The settings page declares, edits and withdraws the own declarations, keeps the draft of the contributor after a refusal, marks the field of a validation error and gives it the focus, shows the history of a withdrawn declaration and confirms the consequences of a withdrawal. The eligibility route shows the honest state of a task that is not published and offers the same evaluation for a condition that the contributor names, so the decision and its reason stay readable. A private capability of another contributor is in no answer.

360px: one column, dialogs fit viewport, tables become labeled lists; 768px: collapsed navigation; 1440px: persistent navigation and restrained readable widths. No page-level horizontal scrolling. Every route defines initial/loading/empty/validation/error/unavailable/permission/success/recovery; preserve draft values after recoverable errors, never announce success before server commit. Destructive operations confirm consequences and restore focus. Server-only private reads use no-store; TanStack keys include actor identity and are cleared on logout; public views omit sensitive fields and query caches.

## Verification commands and evidence

Current command: python3 scripts/verify_bootstrap.py; openspec validate --all --strict --no-interactive.

Foundation must implement these exact native interfaces:
- dotnet restore Blindmesh.sln --locked-mode; dotnet format Blindmesh.sln --verify-no-changes; dotnet build Blindmesh.sln -c Release --no-restore; dotnet test Blindmesh.sln -c Release --no-build.
- npm --prefix apps/web ci; npm --prefix apps/web run lint; npm --prefix apps/web run typecheck; npm --prefix apps/web run build.
- npm --prefix apps/web run api:generate and run api:check generate/check packages/api-client from API-owned OpenAPI.
- docker compose -f infra/compose.dev.yaml up -d --build; dotnet run --project src/Blindmesh.Api -- migrate.
- npm --prefix apps/web run test:e2e -- --project=chromium --grep <requirement-prefix>.
- npm --prefix apps/web run test:a11y -- --grep <requirement-prefix>; Playwright covers both themes and 360/768/1440px, Chromium/Firefox/WebKit smoke on release.
- dotnet test Blindmesh.sln --filter FullyQualifiedName~<requirement-prefix> runs named unit/integration fixture groups against a dedicated PostgreSQL test database, never production.
- forge gate . executes the Forge-owned shared Gate; no Gate runtime is copied into this repository.

APP_ENV=test uses isolated DB, MinIO, Mailpit and mock GitHub fixtures (alice owner, bob eligible contributor, cara ineligible, dana moderator, erin administrator). API startup refuses test auth or fictional metrics in production. Contract tests use generated client, isolated real PostgreSQL migrations, deterministic injected clock, and concurrent requests. Every change has verification.md and traceability.md tying stable requirements/scenarios to these command groups. Structural validation cannot satisfy a behavioral oracle.
