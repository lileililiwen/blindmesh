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

### The eligibility evaluation and the conditions of a published task

The capabilities package owns the evaluation, because the evaluation is the matching of a declaration against a condition and not a property of a task.

The evaluation is deterministic. It takes a set of hard conditions of the kinds `region`, `device` and `role` and compares every value with the label and the tags of the public, available and not withdrawn declarations of the contributor. The values are compared trimmed, with the inner spaces collapsed and case folded, so the spelling of the contributor does not change the result. The answer holds the decision and the reason of every condition, and the reason of a condition that is not met never names a private declaration, so a private capability of the contributor stays private even in a refusal. The account restriction of the evaluation is the ban: a banned session is refused with 401 before the evaluation runs, so a restricted account is never eligible. The evaluation never trusts the data of the client; it is computed from the committed state at the time of the request, so a declaration that changed after a preview changes the next answer.

The conditions of a task reach the module through the seam `ITaskConditionReader`. The task publication package registers `PublishedTaskConditionReader` as the implementation of the seam. The reader answers with the hard requirements of a published task that the actor may see and with null for every other task: a task that does not exist, a task that is not open, a task whose parent project the actor may not see and a task whose own visibility is private for the actor. The reader never returns the conditions of a task that the actor may not see, so the endpoint `GET /v1/capabilities/tasks/{task_id}/eligibility` answers 404 for it and never leaks the task, the project or the contributor. The evaluation stays in the capabilities module; the reader only hands over the conditions.

The route `/tasks/{id}/eligibility` of the web application follows the same safe page that the project package uses for a project that may not be seen: a task that is not published shows the honest state and no eligibility. The route stays usable, because the page also offers the same evaluation for a condition that the contributor names, so the decision and its reason can be read and checked without a published task.

## Credit endpoints

The credits package owns these endpoints. They are under the same prefixes as the endpoints of the foundation, and the browser reaches them under `/api/v1`.

| Endpoint | Purpose |
|---|---|
| `GET /v1/wallet` | Reads the wallet of the signed in contributor: the available, the reserved and the pending balance, the lots with their expiry and the state of the limited grant. The optional query `user_id` names the owner of the wallet; the wallet of another contributor answers 404 without an amount. |
| `GET /v1/wallet/transactions` | Lists the transactions of the wallet of the signed in contributor in the list envelope `{items,next_cursor}`. The limit defaults to 20 and is capped at 100. Only the entries of the own accounts are part of the answer. |
| `POST /v1/wallet/grant` | Claims the limited grant of the signed in contributor. It requires the `Idempotency-Key` header (16 to 128 printable ASCII characters) and the body field `rules_accepted`. The same key with the same payload replays the original answer, the same key with another payload answers 409. |

There is no public transfer and no public balance editor. The escrow primitives are internal methods of the credit service; the package that owns the task calls them inside its own transaction.

### The escrow primitives are internal

The credit service exposes the grant, the reservation, the start of the work, the payment and the refund. Only the grant is a public command of this package, because only the grant belongs to a contributor who is not a publisher. The other four primitives move credits between the buckets of a task, so they belong to the package that owns the task and the participation, and that package is not implemented yet. They are proved by the integration tests of this package through the application service, not through a public route. A later package calls them with the same scoped database context and the same transaction as its own command, so the entries of the escrow and the idempotency record of the command are committed together.

The names of the primitives are the ones of the shared contract: `GrantOnceAsync`, `ReserveTaskAsync`, `BeginPendingAsync`, `SettleAsync` and `RefundUnstartedAsync`. The primitive of the dispute and the reconciliation of the whole ledger come with the packages that own a dispute and the periodic reconciliation; this package does not answer a dispute and does not run a scheduler.

The wallet of a contributor is private. Every read, list and command of the module is authorized: a visitor without a session receives 401, the wallet of another contributor answers 404 without an amount, and the history holds only the entries of the own accounts.

### The grant: eligibility, the cap and the uniqueness of a subject

The eligibility of the grant is derived from the committed state of the identity package and from the injected clock. The identity package stores the verified provider email and the creation date of the account at the provider; the credit service reads both through the identity store and never trusts a field of the request. An account is eligible when it has a verified provider email, when it is at least seven days old and when the contributor accepts the rules of the marketplace in the claim itself. The age of the account is the age of the account at the provider, because the provider owns that date and the date does not change; the identity module keeps it in step with the provider at every sign in. The creation of the product account is the fallback for a provider that reports no date. A body that holds a field outside `rules_accepted` is refused before anything is stored and without an echo of the refused field. The local OAuth fixture declares a stable age for every seeded account, so the seeded accounts have a real age no matter when their row was created; the seeded account of cara is one day old and has no verified email, and the grant of a young account is refused with the reason.

The uniqueness of a subject is a unique index on `grant_claims(provider, subject)`. The claim of a subject that claimed already answers 409 and issues nothing.

The daily cap of the platform is 6000 credits in 24 hours, and the daily limit of an address is 5 claims. Both are decided under the lock of the accounts and both are counted from the claims of the last 24 hours.

The claim is serialized against the global cap and against the account of the contributor. The accounts of a claim are the ordinary account of the contributor and the issuance account of the platform. They are locked in the order of their identifiers, and the identifier of an account is derived from its bucket, its owner and its reference, so the order is the same in every command. The claim reads the state of the cap and the claim of the subject again under that lock, so two concurrent claims of the same subject issue one grant, and the cap of the platform is never exceeded by two concurrent claims. A serialization failure or a deadlock of the database is retried at most three times with the same key and the same payload.

### The journal, the lots and the persistence of the package

The tables of the package are `ledger_accounts`, `ledger_transactions`, `ledger_entries`, `credit_lots` and `grant_claims`. The table `idempotency_records` comes from the runtime foundation and is used by the money commands of this package.

| Table | Content |
|---|---|
| `ledger_accounts` | One row per bucket of an owner. The bucket is `UserAvailable`, `Reserved`, `Pending`, `PlatformIssuance` or `PlatformExpiry`. The ordinary account of a contributor has an empty reference, the escrow accounts carry the reference of the task and the platform accounts carry the constant platform reference. The identifier of a row is derived from the bucket, the owner and the reference, so the same account is never created twice. |
| `ledger_transactions` | One row per movement of credits. It holds the kind, the reference, the actor, the idempotency key of the command and the moment of the movement. A transaction is never changed. |
| `ledger_entries` | The entries of a transaction. Every entry holds the account, the signed amount and the lot it belongs to. An entry is never changed; a correction is a compensating transaction. The signed sum of the entries of a transaction is zero. |
| `credit_lots` | The lots of a contributor: the kind (`grant` or `earned`), the origin, the original amount, the spendable, the reserved and the pending amount and the expiry. A grant lot expires, an earned lot never does. The lot is the cached view of the journal, and the reconciliation compares it with the journal. |
| `grant_claims` | One row per claimed provider subject: the provider, the subject, the contributor, the account, the transaction, the issued amount, the address of the claim, the moment of the accepted rules, the moment of the issue and the expiry. |

The lock order of a money command follows the shared contract: the task, the participation or the slot, then the accounts sorted by their identifiers, then the ledger. The accounts of a command are locked with `SELECT ... FOR UPDATE` in the order of the identifiers of the database, and the rows of the accounts are created in the same order, so two commands never wait for each other. A command of the escrow that would overspend the ordinary account of a contributor answers 409 and writes no entry; the ordinary bucket of a contributor is never negative. A command that is retried with the same key and the same payload answers with the original result and writes no second entry.

The expiry of a grant lot removes only the spendable units of the lot; the reserved units stay valid until the reservation is resolved. A reserved grant that is paid becomes an earned credit that never expires. A reserved grant that is refunded after it expired goes to the expiry sink of the platform instead of back to the publisher, so no balance of a contributor ever goes negative. The wallet of a contributor materializes the expiry of the lots that ran out when it is read, so the balance of the wallet and the balance of the journal are the same.

## Task endpoints

The tasks package owns these endpoints. They are under the same prefixes as the endpoints of the foundation, and the browser reaches them under `/api/v1`. A task belongs to a project and keeps the visibility of that project: a task of a private project is private, and a task of a public project that is not active is not readable by a visitor.

| Endpoint | Purpose |
|---|---|
| `GET /v1/tasks` | Lists the tasks the caller may see in the list envelope `{items,next_cursor}`. The limit defaults to 20 and is capped at 100. The optional query `mode` names one mode or several, separated by commas, and a task has to carry all of them. The optional query `requirement` names a hard requirement as `kind:value`, where the kind is `region`, `device` or `role`; a value without a kind matches every kind. The optional query `project_id` narrows the list to the tasks of one project. A visitor sees the published tasks of the public and active projects; a contributor also sees the tasks of the projects they own or belong to, including their drafts. The items are summaries: they never carry the scope, the steps or the instructions of an access. |
| `POST /v1/tasks` | Writes a task in the draft state. It needs a signed in contributor who may change the project of the task. The body holds the project, the title, the scope, the modes, the visibility, the expected minutes, the base reward, the bonus, the number of slots, the deadline, the acceptance rules and the hard requirements. It never holds a credential. The optional `Idempotency-Key` header makes a retry of the same command answer with the same task. |
| `GET /v1/tasks/{id}` | Reads the committed state of a task: the mode, the scope, the acceptance rules, the deadline, the slots, the budget, the requirements and the history of the task. A task of a `matched` visibility shows its summary only to a visitor, so the instructions are released to the contributors that match. A task that may not be seen answers 404 without the name of the project or of the contributor. |
| `PATCH /v1/tasks/{id}` | Changes the editable content of a task and needs the `expected_version` of the task. The reward, the bonus and the number of slots of a published task never change, because its budget is already reserved. A stale version answers 409 with the current version. |
| `POST /v1/tasks/{id}/publish` | Publishes a task and reserves its maximum budget. It needs the `Idempotency-Key` header and the signed in owner of the task. The whole budget is reserved in the same transaction as the state of the task, so the task never becomes open without its budget. A task without the acceptance rules of its modes, without a hard requirement or with a deadline outside the window answers 422 and writes nothing. An account that does not hold the budget answers 422 with the code `insufficient_funds`. |
| `POST /v1/tasks/{id}/cancel` | Closes the admission of a task and refunds the unused slots. It needs the `Idempotency-Key` header and the owner of the task or the owner of the project. The slots that carry running, submitted or disputed work keep their budget. |

### The participation endpoints

The participation package extends the tasks module with the seat of one contributor in one slot of a task. The endpoints are under the same prefixes as the endpoints of the tasks, and the browser reaches them under `/api/v1`. A seat belongs to a task and keeps the visibility of that task: a seat of a task that the caller may not see answers 404 and never names the project, the owner or the contributor.

| Endpoint | Purpose |
|---|---|
| `POST /v1/tasks/{id}/applications` | Asks for one slot of a published task. It needs a signed in contributor and the `Idempotency-Key` header. The body holds the `scope_consent`, which is a statement and not a credential, and the `expected_version` of the task. The application stores the snapshot of the capability evaluation, the consent and the version. One participation per task and contributor; a second application answers 409. A member of the project of the task answers 403, even when the public capabilities match. |
| `GET /v1/tasks/{id}/applications` | Lists the applications of a task in the list envelope `{items,next_cursor}`. Only the owner of the task and the owner of the project may read them; every other caller answers 404. |
| `GET /v1/participations/{id}` | Reads the committed state of one seat: its state, its deadlines, its reward, its slot, the instructions and the history. The instructions are released with the seat, so an applied seat never carries the scope of the task. |
| `POST /v1/participations/{id}/accept` | Accepts an application of the owner of the task or of the project. It needs the `Idempotency-Key` header. The acceptance creates exactly one exclusive lease, checks the eligibility of the applicant again and recomputes the aggregate status of the task. A task without a free slot answers 409, so two acceptances of the last slot never book it twice. |
| `POST /v1/participations/{id}/decline` | Declines an application. It never held a slot and never touched the escrow. |
| `POST /v1/participations/{id}/start` | Starts the work of an accepted seat. It needs the `Idempotency-Key` header. The reserved budget of the slot moves into the pending bucket of the contributor in the same transaction as the state of the seat. A seat that missed its start window answers 409. |
| `POST /v1/participations/{id}/withdraw` | Withdraws a seat before the work starts. The lease is released and the slot goes back to the task; the budget of the slot stays reserved for another contributor while the task accepts work. |
| `GET /v1/work` | Lists the seats of the signed in contributor in the list envelope `{items,next_cursor}`. The limit defaults to 20 and is capped at 100. |

The routes of the interface are `/tasks/{id}/apply`, `/work` and `/work/{id}`. The page of a seat shows the state, the next action, the visible deadline and the instructions, and the page of the task of the owner shows the applications with the actions that accept or decline them.

### The participation model and its persistence

The tables of the participation package are `participations`, `applications`, `slot_leases` and `participation_history`. They are owned by the package and are not changed by any other module.

| Table | Content |
|---|---|
| `participations` | One row per seat. It holds the task, the application, the contributor, the state, the slot, the lease, the moments of the application, of the acceptance, of the start and of the end, the visible execution deadline and the optimistic concurrency version. |
| `applications` | The application of one contributor for one slot of a task. It holds the snapshot of the capability evaluation, the consent of the scope and the version of the task the contributor saw. It is written once and is never changed. |
| `slot_leases` | The exclusive lease of one slot. Only one live lease may exist for a slot, so two owners cannot hold the same seat. |
| `participation_history` | The applied transitions of the lifecycle of a seat: the state before, the state after, the contributor and the moment. Every transition is written in the same transaction as the seat. |

The state machine of a participation is its own and never shares a state with a project, a capability, a task, a submission, a finding or a dispute. This package owns the part of the row of the shared table from `Applied` to `Running`: `Applied -> Accepted` when the owner accepts the application, `Applied -> Declined` when the owner declines it, `Applied -> Expired` when the owner does not answer within 48 hours, `Accepted -> Running` when the contributor starts within 24 hours, `Accepted -> Withdrawn` when the contributor gives the seat back and `Accepted -> Expired` when the start window passes. The states `Submitted`, `Approved`, `ChangesRequested` and `Disputed` belong to the package that owns the submissions, because the evidence causes them, and the withdrawal of a running seat belongs to the package that owns the settlement of the escrow.

The visible execution deadline of a seat is the smaller of the deadline of the task and the start of the work plus seven days. The deadlines of the architecture are the hours to apply after the publication, the hours to start after the acceptance and the days of the execution after the start.

### The escrow of a participation

The acceptance writes no ledger row: the budget of the task stays reserved while the seat waits for its start. The start command locks the task, then the seat, then the slot and calls `BeginPendingAsync` of the credit service inside the same scoped database context and the same transaction as the write of the seat. The reference of the escrow is `task:{id}` and the amount is the base reward of the slot, because the bonus of the slot stays reserved until the settlement of the work releases it. The credit module writes every entry of the escrow; the participation package never writes a ledger row.

A seat that is withdrawn or that expires before its start never moved its budget, so the slot goes back to the task while the task still accepts work and its deadline did not pass, and the budget of the slot is refunded to the publisher with `RefundUnstartedAsync` otherwise. A running seat keeps its budget and its submission grace when the task is cancelled, because the cancellation refunds only the slots that are still available. The settlement of the pending credit of a seat and the release of its bonus belong to the package that owns the submissions and the disputes, so the escrow of a running seat is settled there and not here.

The sweep of the deadlines of the worker finds the applications that wait longer than 48 hours for a decision and the seats that wait longer than 24 hours for the start. It uses the injected clock and the same lock order as the commands, so a sweep that races a command produces one legal transition and one financial outcome.

### The task model and its persistence

The tables of the package are `tasks`, `task_requirements`, `task_steps`, `task_slots` and `task_history`.

| Table | Content |
|---|---|
| `tasks` | One row per task. It holds the project, the owner, the title, the scope, the modes as a bit set, the visibility, the lifecycle state, the expected minutes, the base reward, the bonus, the number of slots, the deadline, the acceptance rules of the modes, the reserved budget, the optimistic concurrency version and the moments of the creation, the publication and the cancellation. |
| `task_requirements` | The hard requirements of a task: the kind (`region`, `device` or `role`) and the value. They are the conditions that the capabilities module matches against the declarations of a contributor. |
| `task_steps` | The steps of the verification of a task, with their position, their title and their expected result. A verify task holds at least one step with an expected result. |
| `task_slots` | The slots of a task, one per contributor, with their position, their reward, their state and the contributor that holds them. The states are `available`, `active`, `submitted`, `disputed`, `settled` and `refunded`. |
| `task_history` | The applied transitions of the lifecycle of a task: the state before, the state after, the contributor and the moment. Every transition is written in the same transaction as the task. |

The state machine of a task is its own and never shares a state with a project, a capability, a participation, a submission, a finding or a dispute. The package owns the publication part of the row of the shared table: `Draft -> Open` on a reserved publication and `Open -> Cancelled` on a cancellation. The execution states of the shared table belong to the packages that own the participations, the submissions and the disputes, so `InProgress`, `Review` and `Completed` follow the seats of the task under its lock. A task is never deleted; it keeps its row, its slots and its history.

The budget of a task is `(base_reward + bonus_cap) * slot_count`, as the shared contract of the publication demands. The base reward and the bonus are the amounts of one slot, so every slot holds the base and the bonus of the task. The whole amount is reserved when the task is published and every unused slot is refunded when the task is cancelled.

The visibility of a task never exceeds the visibility of its project. The rules are: the owner of the project and its members see the whole task; a visitor sees a published task of a public and active project, and sees its summary only when the visibility of the task is `matched`; every other task is not found. The visibility of a project and the membership of a project belong to the project package, and the tasks package asks the project service instead of repeating the rule.

### The escrow of the publication

The publish command locks the task with `SELECT ... FOR UPDATE` and calls `ReserveTaskAsync` of the credit service inside the same scoped database context and the same transaction as the write of the task. The reference of the escrow is `task:{id}` and the amount is the maximum budget of the task. The lock order of the shared contract is respected: the task first, then the accounts sorted by their identifiers, then the ledger.

The credit module writes every entry of the escrow; the tasks package never writes a ledger row. A refused reservation answers 422 with the code `insufficient_funds` and the transaction is rolled back, so a failed publication leaves neither an open task nor a partial reservation. A publication that is retried with the same `Idempotency-Key` answers with the same task and the same reservation, because the scoped record of the command is written in the same transaction as the escrow.

The cancel command locks the task, reads its slots and calls `RefundUnstartedAsync` for the slots that are still `available`. The slots that carry running, submitted or disputed work keep their part of the escrow, and the admission of the task is closed, so no new contributor takes them.

## Findings endpoints

The findings package owns the evidence of a seat and the review of the owner. The endpoints are under the same prefixes as the endpoints of the tasks, and the browser reaches them under `/api/v1`. A submission belongs to a seat and keeps the visibility of the task of that seat: a submission that the caller may not see answers 404 and never names the project, the owner or the contributor. The state of the seat moves with the evidence, so the seat, its slot and the submission always carry one state.

| Endpoint | Purpose |
|---|---|
| `POST /v1/participations/{id}/submissions` | Sends the evidence of a seat that the caller runs. It needs a signed in contributor and the `Idempotency-Key` header. The body holds the summary of 1 to 5000 characters, the `scope_completed` flag, the result of every step of a verification, the tested areas and the explanation of a clean result of an exploration, and the findings. The first revision opens the submission; a correction adds a revision to the same submission and keeps the revisions the owner already read. A seat that does not run answers 409. |
| `GET /v1/submissions/{id}` | Reads the committed state of a submission: its state, its revisions, its findings, its history, the deadlines of the review and the next action. Only the contributor of the seat and the owner of the task or of the project may read it; every other caller answers 404. |
| `GET /v1/projects/{id}/reviews` | Lists the evidence of the tasks of a project that the owner may review. Every other caller answers 404 or 403. |
| `POST /v1/submissions/{id}/accept` | Accepts the work of a seat. It needs the `Idempotency-Key` header and the owner of the task or of the project. The body holds the explicit bonus, which may not be more than the bonus of the slot, and the `expected_version` of the submission. The settlement and the state of the submission are written in the same transaction. A submission that does not wait for a decision answers 409. |
| `POST /v1/submissions/{id}/request-changes` | Asks the contributor for the one correction of the evidence. It needs the `Idempotency-Key` header and the owner of the task or of the project. The body holds the note of the correction and the `expected_version`. The contributor has 48 hours to answer it, and a second request answers 409. |
| `POST /v1/submissions/{id}/dispute` | Opens a dispute about the completion of a seat. It needs the `Idempotency-Key` header and the owner of the task or of the project. The body holds the reason of the dispute and the `expected_version`. The funds of the seat wait for the arbitration and this package moves no credit. |
| `GET /v1/findings/{id}` | Reads one finding of the evidence with its history. Only the contributor of the seat and the owner of the task or of the project may read it. |
| `PATCH /v1/findings/{id}` | Classifies one finding. It needs the `Idempotency-Key` header and the owner of the task or of the project. The body holds the new state, the optional note and the `expected_version`. The decision never decides the earned base pay of the submission, and it moves no credit. |
| `POST /v1/attachments/intents` | Declares a private file of the evidence. It needs a signed in contributor and the `Idempotency-Key` header. The body holds the submission, the name of the file, its content type and its size. The file is quarantined from this moment and it is never readable while it waits. An executable, an SVG, an HTML file and an archive are refused, and a revision carries at most ten files. |
| `POST /v1/attachments/complete` | Uploads the bytes of a quarantined file with a form and releases it. It needs the `Idempotency-Key` header and the contributor of the seat. The bytes are checked against the declaration and against the signature of the file before they reach the private bucket, and the row of the file is written in the same transaction as the record of the command. The bytes never travel inside a transaction. |
| `GET /v1/attachments/{id}/download` | Signs the address of a private file. The authorization is checked again for every address, so a reader that lost the membership never renews an address it held before, and the address lives 60 seconds. A file that is still quarantined answers 409. |

The routes of the interface are `/work/{id}/submit`, `/projects/{id}/reviews` and `/findings/{id}`. The page of the evidence shows the summary, the flag of the completed scope, the result of every step, the findings and the private files of the revision, and it keeps the draft of the contributor after a refusal. The page of the reviews lists the evidence of the project and offers the acceptance with a bonus, the one correction and the dispute. The page of a finding shows the finding with its history and offers the classification of the owner.

### The findings model and its persistence

The tables of the findings package are `submissions`, `submission_revisions`, `submission_history`, `findings`, `finding_history` and `attachments`. They are owned by the package and are not changed by any other module.

| Table | Content |
|---|---|
| `submissions` | One row per seat that sent its evidence. It holds the seat, the task, the project, the contributor, the owner, the state, the summary and the flag of the current revision, the number of the revision, the moments of the submission, of the review and of the correction, the deadline of the correction, the granted bonus, the settled credits, the reason of the dispute and the optimistic concurrency version. |
| `submission_revisions` | One immutable revision of a submission. It holds the summary, the flag of the completed scope, the result of every step, the tested areas, the explanation of a clean result and the moment of the revision. A revision is written once and is never changed, so the evidence the owner read stays readable. |
| `submission_history` | The applied transitions of the lifecycle of a submission: the state before, the state after, the contributor and the moment. Every transition is written in the same transaction as the submission. |
| `findings` | One finding of a revision. It holds the category, the severity, the title, the reproduction, the expected and the actual result, the state, the note and the moment of the decision, the contributor that reported it and the optimistic concurrency version. |
| `finding_history` | The applied transitions of the lifecycle of a finding: the state before, the state after, the owner and the moment. Every transition is written in the same transaction as the finding. |
| `attachments` | One private file of a revision. It holds the name, the content type, the size, the key of the object in the private bucket, the state, the reason of a refusal, the digest of the bytes and the moments of the declaration and of the check. |

The state machine of a submission is its own and never shares a state with a project, a capability, a task, a participation, a finding or a dispute. This package owns it: `Draft -> Submitted` when the contributor sends the evidence, `Submitted -> ChangesRequested` when the owner asks for the one correction, `ChangesRequested -> Submitted` when the contributor answers it and `Submitted -> Accepted` or `Submitted -> Disputed` when the owner decides. The states `Submitted`, `ChangesRequested`, `Approved` and `Disputed` of a seat belong to this package as well, because the evidence causes them.

The state machine of a finding is its own: `Submitted -> Triaged -> Confirmed -> Fixing -> Resolved`, and `Triaged -> Rejected` or `Triaged -> Duplicate`. A rejected finding never confiscates the pay of a completed scope, and a disagreement about the completion opens a dispute on the submission, not on the finding. A regression reopens a confirmed finding.

The limits of the evidence are the limits of the design: a summary of 1 to 5000 characters, a title of a finding of at most 140 characters, a reproduction of at most 10000 characters, an expected and an actual result of at most 5000 characters, ten files per revision, a text or a log of at most 2 MiB and a picture of at most 10 MiB. A verification needs a result for every step of its task, an exploration needs the areas it covered, and a result without a finding needs the explanation of the contributor and a task that accepts a clean result.

### The escrow of a submission

The acceptance locks the task, then the seat, then the slot and then the submission, and it calls the credit service inside the same scoped database context and the same transaction as the write of the submission. The reference of the escrow is `task:{id}`, the same reference the start of the seat used. The granted bonus moves from the reserved bucket of the publisher into the pending bucket of the contributor with `BeginPendingAsync`, the base of the seat and the bonus are paid out with `SettleAsync`, and the unused bonus of the slot goes back to the publisher with `RefundUnstartedAsync`. The credit module writes every entry of the escrow; the findings package never writes a ledger row.

A dispute moves no credit at all: the base of the seat stays pending and the bonus of the slot stays reserved until the arbitration of another package decides. The rejection of a finding moves no credit either, because the decision about a finding never decides the earned base pay of a completed scope.

The sweep of the deadlines of the worker accepts the base of a seat that waited longer than 72 hours for the review of the owner, when no correction and no dispute is open, and it opens a dispute for a correction that was not answered in time. It uses the injected clock and the same lock order as the commands, so a sweep that races a manual acceptance produces one settled result and one refund.

### The pipeline of a private file

The private object storage speaks the S3 API and holds the files of the evidence in a private bucket. The product never stores the bytes of a file in the database, and it never publishes a permanent address of a file.

The pipeline of a file has three steps. The declaration checks the name, the content type and the size of the file against the allowlist and writes a quarantined row. The upload reads the bytes, checks them against the declaration and against the signature of the file, writes them into the private bucket and releases the row in the transaction of the command. The download checks the authorization again and signs an address that lives 60 seconds. A quarantined or a refused file is never readable, and a reader that lost the membership never renews an address it held before.

The check of the bytes is the honest quarantine of this package: it proves the declared kind of the file against the signature of the bytes, it refuses the signatures of an executable, of an archive, of an SVG and of an HTML page, and it refuses the marker of the standard test file of the antivirus programs. It is not an antivirus engine; the package does not claim a malware scan that it does not run. The state of a file moves from `quarantined` to `scanned` or to `rejected`, and it never moves back.

## Moderation endpoints

The moderation package owns the reports of the marketplace and the cases of the arbitration. A report belongs to the reporter and keeps its body away from everybody else: the reporter reads a receipt and only the moderation staff read the body. A case belongs to the parties of the arbitration and to the moderation staff, and nobody else finds it. The browser reaches the endpoints under `/api/v1`.

| Endpoint | Purpose |
|---|---|
| `POST /v1/reports` | Writes a report of visible content. It needs a signed in contributor and the `Idempotency-Key` header. The body holds the reason of 20 to 5000 characters and the optional kind and identifier of the content. The answer is the receipt of the report: a stable code the reporter reads back, never the body and never the identity of the reporter. |
| `GET /v1/reports/{id}` | Reads the receipt of a report. The reporter reads the receipt, the moderation staff read the body of the report, and every other account answers 404. |
| `GET /v1/admin/reports` | Lists the reports of the moderation staff with their body. Every other account answers 403. |
| `POST /v1/submissions/{id}/disputes` | Opens the dispute of a seat. It needs the `Idempotency-Key` header and the owner of the task or of the project. The body holds the reason of 20 to 5000 characters and the `expected_version` of the evidence. The funds of the seat wait for the arbitration. The route of the findings package, `POST /v1/submissions/{id}/dispute`, opens the same case through the same code path. |
| `GET /v1/disputes/{id}` | Reads the case of an arbitration with its reason, the note of a request of more evidence, its decision, the ledger result of it, its evidence and its trail. The parties of the case and the moderation staff read it; every other account answers 404. |
| `GET /v1/admin/disputes` | Lists the cases of the moderation staff. Every other account answers 403. |
| `POST /v1/admin/disputes/{id}/request-evidence` | Asks the parties of a case for more evidence. It needs the `Idempotency-Key` header and a moderator without a conflict. The body holds the note of 20 to 5000 characters and the `expected_version`. The escrow of the case stays exactly as it was. |
| `POST /v1/admin/disputes/{id}/resolve` | Decides a case and moves the escrow of the seat. It needs the `Idempotency-Key` header and a moderator without a conflict. The body holds the `award_base` flag, the reason of 20 to 5000 characters, the optional bonus in whole credits and the `expected_version`. A bonus may only be awarded together with the base of the seat, and it may never be more than the reserved escrow of the task: a bonus without the base answers 422 and a bonus beyond the escrow answers 409. A case that was already decided answers 409. |
| `POST /v1/admin/disputes/{id}/close` | Closes a case whose decision stands. It needs the `Idempotency-Key` header and a moderator without a conflict. A case that was not decided answers 409, and a case whose window of the appeal is still open answers 409 as well. |
| `POST /v1/disputes/{id}/appeal` | Appeals the decision of a case. It needs the `Idempotency-Key` header and a party of the case. The body holds the reason of 20 to 5000 characters and the `expected_version`. The appeal is possible once and only within seven days of the decision. |
| `POST /v1/admin/users/{id}/ban` | Bans an account. It needs the `Idempotency-Key` header and an administrator. The body holds the reason of 20 to 1000 characters and the optional end of the ban. An administrator may not ban the own account, and a second ban of the same account overwrites the first one. |

The routes of the interface are `/reports/new`, `/disputes/{id}`, `/admin/reports` and `/admin/disputes`. The page of a report shows the receipt of the case and never the identity of the reporter. The page of a case shows the parties, the reason the case was opened, the note of a request of more evidence, the escrow at stake, the decision, the ledger result of it, the evidence and the trail, and it offers the request of evidence, the decision and the closure to a moderator without a conflict. The form of the decision offers the bonus of the decision while the base of the seat is awarded, and it tells the moderator that the bonus may never be more than the funded escrow of the task. The pages of the administration list the open reports and the open cases and explain the restriction to a moderator that may not act.

### The moderation model and its persistence

The tables of the moderation package are `reports`, `disputes`, `dispute_evidence`, `moderator_assignments`, `moderation_audit` and `account_bans`. They are owned by the package and are not changed by any other module. The migration adds only these tables and their indexes; it does not change a table of an earlier package.

| Table | Content |
|---|---|
| `reports` | One row per report. It holds the reporter, the receipt, the body, the kind and the identifier of the reported content, the state, the moderator that read it, the moment of the reading, the answer of the moderation and the optimistic concurrency version. |
| `disputes` | One row per case of an arbitration. It holds the submission, the participation, the task, the project, the publisher, the contributor, the optional report the case came from, the state, the reason, the note of a request of more evidence, the base reward at stake, the award, the amount the award moved, the note of the decision, the moderator that decided it, the moment of the decision, the ledger transaction of the decision, the party that appealed it, the moment and the reason of the appeal, the moderator that closed it and the moment of the closing. |
| `dispute_evidence` | One piece of evidence of a case. It holds the case, the account that wrote it, the kind, the note and the moment. The rows are append only. |
| `moderator_assignments` | The assignment of a case to a moderator. It holds the case, the moderator, the kind of the assignment, the moderator that made it, the moment of the assignment and the moment of the release. |
| `moderation_audit` | One line of the trail of the module. It holds the case or the report, the account that caused the line, the action, the reason, the free details and the moment. The lines are append only: a decision, a refusal and a compensation never change the lines that came before them. |
| `account_bans` | One row per banned account. It holds the account, the moment the ban ends, the reason, the moderator that applied it, the moment of the ban and the optimistic concurrency version. An account without a row is never banned. |

The state machine of a dispute is its own and never shares a state with a project, a capability, a task, a participation, a submission, a finding or a report: `Open -> EvidenceRequested -> Resolved -> Closed`, and one appeal within seven days of the decision reopens the case to `Open` for a different moderator. The state machine of a report is its own as well: `Open -> UnderReview -> Resolved` or `Open -> Dismissed`. The trail of the module records every applied transition of a case in the same transaction as the case.

The reason of a report, of a dispute and of a decision holds 20 to 5000 characters. The reason of a ban holds 20 to 1000 characters. The window of the appeal is seven days and every case carries at most one appeal.

### The escrow of a dispute

The escrow of a seat waits for the arbitration. When the contributor started the seat, the base of the seat moved from the reserved bucket of the publisher into the pending bucket of the contributor with the reference `task:{id}`, and the bonus of the slot stayed reserved. At the moment of the dispute the base of the seat is therefore pending and the reserved budget of the publisher is untouched.

The decision of a case moves the base of the seat and, with it, an optional bonus within the funded escrow of the task. The credit module owns the ledger and writes every entry of the movement. A bonus of the decision leaves the reserved bucket of the publisher and lands in the pending bucket of the contributor with `BeginPendingAsync`, because the bonus of a decision is paid out of the escrow of the task and not out of the pending base of the seat; a bonus that the reserved bucket of the task does not carry is refused with 409 and the escrow stays exactly as it was. Then `ArbitrateAsync` moves the base and the bonus from the pending bucket of the contributor either into the available bucket of the contributor, when the award is `award_base = true`, or into the available bucket of the publisher, when the award is `award_base = false`. A bonus without the base of the seat answers 422 and writes nothing. The lot of the award keeps the reference of the seat and the source `arbitration`, so that a later appeal can compensate exactly that lot. The row of the case remembers the amount the award moved, so the appeal compensates the base and the bonus together. The rest of the reserved budget of the task is not touched by the decision: the unused budget stays reserved and is released by the lifecycle of the task, exactly like every other unused credit of a task.

The decision ends the seat: the participation leaves the disputed state, the lease of the slot is released and the slot is free again, so the task may take another contributor. The settlement of the seat is frozen while the case is open, so the owner of the task may not accept the evidence of a disputed seat.

The appeal is possible once, within seven days of the decision and only by a party of the case. The appeal reopens the case and it never changes the money: another moderator decides the case again. A decision that confirms the first one moves no credit a second time. A decision that takes the award back from the contributor moves the amount with `CompensateArbitrationAsync`, which writes new, balanced entries and never touches an entry of an earlier transaction, so the rows of the first decision and the lines of the trail stay exactly as they were written. When the available balance of the contributor does not cover the award any more, the change is refused with 409 and the code `arbitration_not_reversible`, the case stays open and the refusal is written to the trail. A decision that would take the money back from the ordinary balance of the publisher is refused the same way, because the credit module never debits the ordinary balance of a publisher.

### The conflict of a moderator

A moderator may act on a case only when the moderator is none of these, and the rule is read again on every command, list, detail and export:

- the publisher of the task,
- the contributor of the seat,
- a member of the project of the task, in any role, including the owner of the project,
- the reporter of the report the case came from,
- a moderator that already acted on the case.

Otherwise the answer is 403 and the escrow of the case stays exactly as it was. The rule is decided in one place, in the store of the moderation module, which reads the memberships of the project, the task, the participation and the trail of the case. The role of the account decides whether it may act at all: the role `moderator` and the role `administrator` are the staff of the moderation, and the role `user` never acts on a case.

### The ban of an account

The ban of an account lives in the table `account_bans` of the moderation package. The `users` table of the identity package is not changed.

The ban is enforced in one place, in the session guard of the API. The guard reads the standing of the account through the `IAccountStandingReader` of the application layer, after it resolved the session and before it hands out the actor of the request. A banned account is refused with 403 and the code `account_banned` on every request, read or write, before any domain or monetary effect. The guard refuses a request without a session with 401 as before, and a visitor is never refused by the ban, because the ban belongs to an account and not to a visitor.

The end of a ban is optional: a ban without an end lasts until the account is banned again, and a ban with an end in the future ends itself when the moment has passed. A second ban of the same account overwrites the row of the first one, so the standing of an account is always one row.

### The interface of the moderation

The web application of the moderation package has four routes and one client library. The library lives in `apps/web/src/lib/moderation.ts` and holds the state machine of every read and every command, exactly like the other packages of the product. It calls the API only through the same origin path `/api/v1` of the proxy route of the web application, so the session cookie and the CSRF token stay with the request, and every read uses `no-store`. The types of the library come from the generated client, and the generated client is the only place where the contract of the API is written down.

| Route | Page | What it shows |
|---|---|---|
| `/reports/new` | `apps/web/src/components/report-form.tsx` | The form of a report: the kind of the subject, the identifier of the subject and the reason of 20 to 5000 characters. After the server committed the report, the page shows the receipt of it: the code, the state and the moment. The identity of the reporter is in no answer of the API and in no page. |
| `/disputes/{id}` | `apps/web/src/components/dispute-detail.tsx` | One case: the state, the stake of the seat, the award, the decision with its reason, the move of the credits and the appeal. A party of the case finds the appeal once, while the case waits for a decision and carries no appeal. |
| `/admin/reports` | `apps/web/src/components/report-queue.tsx` | The queue of the reports for the staff: the receipt, the state, the reported content and the body of the report. |
| `/admin/disputes` | `apps/web/src/components/dispute-queue.tsx` | The queue of the cases for the staff with the three commands: the request of more evidence, the decision with the award of the base and the reason, and the closing. Every command runs on the committed state of the case and sends the `expected_version` that the detail of the case carries. |

The four routes share the primitives of the product: the same cards, the same fields with their labels and their errors, the same buttons, the same badge of a state, the same `aria-live` status and the same loading, empty and error states. The navigation of the shell shows the entry `Report content` to every signed in contributor and the entries `The reports` and `The cases` to the staff, that is to the role `moderator` and to the role `administrator`. The navigation wraps itself, so it never overflows at 360 or at 768 pixels.

Every page of the package shows the honest state of the API. A visitor without a session finds the sign in with the return path of the page. A case that does not exist answers `404` and the page says that the case does not exist; a case that the caller may not read answers `404` as well, because the API does not name the existence of a case to an account outside of it. An account without the role of the staff finds the answer of the API on the pages of the administration, and the pages name the restriction of the area instead of a dashboard of invented data. A request that fails keeps the draft of the reporter, marks the field of the refusal and gives it the focus, and no page announces a success before the server committed it.

Two commands of the package have no page of their own yet, and the interface does not hide them: the command that opens the case of a seat is sent by the owner of the task through the API, and the ban of an account belongs to the administration of the accounts, which is not a route of this package. The client library carries both commands, so the next page of the product can use them without a new contract.

The answer of the API carries the state, the stake, the award, the reason of the decision, the move of the credits and the reason of the appeal. It does not carry the reason of the case and it does not carry the note of a request of more evidence: the trail of the case keeps both, and the pages of the package say so instead of showing an empty field. The API also leaves out every field of a null value, so the interface reads the absent fields as absent.

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

Roles: user, moderator, administrator. Developer/tester/resource-provider are concurrent activities, not exclusive roles. Moderators cannot decide disputes involving their own project/account; administrators use audited reasoned actions, never a raw balance editor. The ban of an account is written by the moderation package and refused by the session guard before any effect; see the section of the moderation package. Basic member management ships with project-workspaces; organizations/team billing remain deferred.

## Credit and participation invariants

Credits owns immutable, balanced entries and caches rebuildable balances. Ledger buckets: user available, publisher reserved, contributor pending, platform issuance/expiry. Every transaction's signed entry sum is zero; ordinary user buckets cannot be negative. Platform issuance is the explicitly authorized supply source. Never sum available+reserved as spendable.

User buckets track credit lots: grant (expires), earned (non-expiring). Spending reserves oldest-expiring grant lots first, then earned lots; split entry provenance is preserved. Grant expiry removes only unreserved available units. A reserved grant stays valid until resolution, becomes non-expiring earned credit when paid, and returns to available or expiry sink on refund depending on current expiry.

Default limited grant: 60 credits once per GitHub subject, only after verified email, accepted rules and account age >=7 days. 24h global issuance cap 6,000; exhausted cap returns ineligible/retry tomorrow, never overdrafts. Application records normalized provider/subject eligibility; rate limits (5 claims/IP/day plus account limit) and moderator anomaly review reduce abuse without promising perfect identity detection. Grant expires after 30 days; registration itself remains available without grant eligibility.

Publication reserves (base_reward+bonus_cap)*slot_count in one transaction with task Open; valid base 1..10,000, bonus 0..10,000, slots 1..20. No floating point, cash transfer, purchases, or withdrawals. ICreditService exposes GrantOnceAsync, ReserveTaskAsync, BeginPendingAsync, SettleAsync, RefundUnstartedAsync, ArbitrateAsync and CompensateArbitrationAsync; the two last primitives are internal and are called by the package that owns the dispute, inside the transaction of that caller. All operate within the caller's scoped DbContext/IDbContextTransaction with actor, references, long integer amounts, clock and idempotency key; reject mismatched ownership, references or budget.

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

The tasks package implements the task row of the table as follows. The tables are `tasks`, `task_requirements`, `task_steps`, `task_slots` and `task_history`. A task is written as a draft and becomes open with the reserved publication; the owner of the task or the owner of the project cancels it, the admission closes and the unused slots are refunded. The state machine of a task is its own and never shares a state with a project, a capability, a participation, a submission, a finding or a dispute. This package owns the slots and their states, so the later packages find the slot they have to fill. Every applied transition is written to the history in the same transaction as the task. The version of a task is the optimistic concurrency version of its content and of its state; the publication and the cancellation increase it as well.

The participation package implements the participation row of the table as follows. The tables are `participations`, `applications`, `slot_leases` and `participation_history`. A seat is asked for with an application, it is confirmed by the owner of the task, it starts with the contributor and it never shares a state with a task, a slot, a submission, a finding or a dispute. An applied seat holds no slot and carries no instruction; an accepted seat holds exactly one exclusive lease, so the unique index on the live lease of a slot prevents the overbooking. Every applied transition is written to the history in the same transaction as the seat. The version of a seat is the optimistic concurrency version of its state. The aggregate status of a task follows its seats under the lock of the task: a task with a running seat is in progress, a task that waits for the review of a submission or for the arbitration is in review, a task whose slots are all settled or refunded is completed and a task without one is open again, while a cancelled task keeps its state. The aggregate status does not change the version of the task, because the content the contributor saw did not change.

The findings package implements the submission and the finding rows of the table as follows. The tables are `submissions`, `submission_revisions`, `submission_history`, `findings`, `finding_history` and `attachments`. A seat that sends its evidence opens a submission with an immutable first revision, and a correction adds a revision to the same submission. The owner of the task accepts the work, asks for the one correction or opens a dispute, and the state of the seat follows the state of the submission. Every applied transition of a submission and of a finding is written in the history in the same transaction as the row. The version of a submission and the version of a finding are the optimistic concurrency versions of their state. A submission and a finding never share a state with a project, a capability, a task, a participation or a dispute, and the decision about a finding never decides the earned base pay of a submission.

## UI contract for every change

Routes are specified in each design. Navigation: public Home/Tasks/Capabilities; authenticated Dashboard/My projects/My work/Wallet/Notifications/Profile; moderator-only Admin. Default English UI with i18n-ready labels and UTC storage plus labeled local timezone rendering; additional locales/RTL are later packages.

Use shared semantic CSS tokens and shadcn/ui primitives. Light/dark theme follows pre-paint data-theme (stored preference or media query); preserve reduced-motion. Minimum 44px pointer targets, 16px body, proper heading order, single main and skip link, associated errors, aria-live status, keyboard focus order/restoration, WCAG 2.2 AA. Measure text >=4.5:1, UI/focus >=3:1 in both themes. No color-only status.

The identity package adds the routes `/sign-in`, `/onboarding`, `/settings/profile` and `/people/{username}`. The navigation of the shell shows the state of the session: a visitor sees the sign in, a contributor sees the link to the public profile, the profile settings and the sign out. A private route without a session offers the sign in with an allowlisted return path.

The project package adds the routes `/projects`, `/projects/new`, `/projects/{id}` and `/projects/{id}/settings`. The list shows the projects the visitor may see and explains the next action when it is empty. The create form and the settings page keep the draft of the contributor after a refusal, mark the field of a validation error and give it the focus, and announce a success only after the server committed it. The settings page shows the content, the visibility, the lifecycle, the team and the versions of a project, and it explains the restriction to a member without the right to change it. The archive and the removal of a member confirm their consequences. A project that may not be seen shows the same safe page as a project that does not exist.

The capability package adds the routes `/capabilities`, `/settings/capabilities` and `/tasks/{id}/eligibility`. The directory shows the capabilities the visitor may see, marks the ones that a match uses and explains the next action when it is empty. The settings page declares, edits and withdraws the own declarations, keeps the draft of the contributor after a refusal, marks the field of a validation error and gives it the focus, shows the history of a withdrawn declaration and confirms the consequences of a withdrawal. The eligibility route shows the honest state of a task that is not published and offers the same evaluation for a condition that the contributor names, so the decision and its reason stay readable. A private capability of another contributor is in no answer.

The credits package adds the routes `/wallet` and `/wallet/history`. The wallet shows the available, the reserved and the pending balance of the contributor, the lots with their expiry and the state of the limited grant. The grant of a contributor who is not eligible is not a dead end: the page names every condition that is not met, so the account knows what to do. The history shows every movement of the credits of the wallet with its reference and the change of every bucket, and it explains the next action when the wallet has no movement yet. A wallet without a session offers the sign in with an allowlisted return path, and the wallet of another contributor is not found. The claim of the grant waits for the acceptance of the rules, announces its result only after the server committed it and never announces a success that the server refused.

The tasks package adds the routes `/tasks`, `/tasks/new`, `/tasks/{id}` and `/projects/{id}/tasks`. The list shows the tasks the visitor may see with the mode, the budget, the slots, the deadline and the requirements of every task, and it filters them by mode and by hard requirement without leaving the page. It explains the next action when it is empty. The form of a task names the project, the scope, the modes, the acceptance rules, the deadline and the slots, keeps the draft of the contributor after a refusal, marks the field of a validation error and gives it the focus, and announces the success only after the server committed it. The detail shows the scope, the acceptance rules, the deadline, the slots, the budget and the history of the task, and it offers the publication and the cancellation to the owner of the task. The cancellation confirms its consequence before it happens and restores the focus to the button that opened the confirmation. A task that may not be seen shows the same safe page as a task that does not exist, and it never names the project or the contributor.

The findings package adds the routes `/work/{id}/submit`, `/projects/{id}/reviews` and `/findings/{id}`. The page of the evidence shows the state of the seat, the next action, the summary, the flag of the completed scope, the result of every step, the findings and the private files of the revision; it keeps the draft of the contributor after a refusal, marks the field of a validation error and announces the committed result only after the server wrote it. The page of the reviews lists the evidence of the project with the base and the bonus of every seat and offers the acceptance with a bonus, the one correction and the dispute; it shows the committed result of the decision and never hides the pay of a completed scope behind a rejected finding. The page of a finding shows the finding with its category, its severity, its reproduction, its history and the classification of the owner. A seat, a submission or a finding that may not be read shows the same safe page as one that does not exist, and it never names the project, the owner or the contributor.

The moderation package adds the routes `/reports/new`, `/disputes/{id}`, `/admin/reports` and `/admin/disputes`. The page of a report names the kind of the subject, its identifier and the reason, keeps the draft of the reporter after a refusal, marks the field of the refusal and gives it the focus, and shows the receipt of the report only after the server committed it; the identity of the reporter is in no answer and in no page. The page of a case shows the state, the stake of the seat, the award, the decision with its reason, the move of the credits and the appeal, and it offers the appeal once to a party of the case. The pages of the administration list the reports and the cases for the staff, offer the request of more evidence, the decision and the closing on the committed state of the case, and explain the restriction of the area to an account without the role of the staff. A case that does not exist and a case that the caller may not read show the same safe page.

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
