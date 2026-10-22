# Deployment of Blindmesh

The deployment of the pilot is a run of the operator. The product never
deploys itself: there is no endpoint that deploys, promotes or rolls back
anything.

Every secret of the stack comes from the environment of the host. This document
holds no password, no key and no token, and the connection string of a run
belongs to the command of the run, never to a committed file.

## 1. What is deployed

The stack of `infra/compose.prod.yaml` holds seven services:

| Service | What it is |
|---|---|
| `api` | The HTTP API of the product. |
| `worker` | The worker that claims the outbox and delivers the notifications. |
| `web` | The web application of the product. |
| `postgres` | The database of the product. |
| `object-storage` | The private bucket of the evidence. |
| `mail` | The mail relay of the stack. |
| `caddy` | The proxy that publishes the API and the web application. |

## 2. The environment of the host

The host exports the values below before the stack is started. The file
`infra/compose.prod.yaml` reads them and holds no value of its own.

| Variable | What it is |
|---|---|
| `BLINDMESH_PG_DATABASE` | The name of the database. |
| `BLINDMESH_PG_USER` | The user of the database. |
| `BLINDMESH_PG_PASSWORD` | The password of the database. |
| `BLINDMESH_STORAGE_ACCESS_KEY` | The access key of the private bucket. |
| `BLINDMESH_STORAGE_SECRET_KEY` | The secret key of the private bucket. |
| `BLINDMESH_STORAGE_BUCKET` | The name of the private bucket. |
| `BLINDMESH_MAIL_HOST` | The host of the mail relay. |
| `BLINDMESH_MAIL_PORT` | The port of the mail relay. |
| `BLINDMESH_IDENTITY_PUBLIC_BASE_URL` | The public address of the product. |
| `BLINDMESH_API_IMAGE` | The image of the API. |
| `BLINDMESH_WORKER_IMAGE` | The image of the worker. |
| `BLINDMESH_WEB_IMAGE` | The image of the web application. |

## 3. The steps of the deployment

```bash
# 1. The images of the release are pulled.
docker compose -f infra/compose.prod.yaml pull

# 2. The schema of the database is moved to the version of the release.
docker compose -f infra/compose.prod.yaml run --rm api migrate

# 3. The stack is started.
docker compose -f infra/compose.prod.yaml up -d

# 4. The API and its dependencies report that they are ready.
curl --fail --silent "$BLINDMESH_API_URL/v1/health/ready"

# 5. The public status of the product is read.
curl --fail --silent "$BLINDMESH_API_URL/v1/status"
```

Step 2 is the only step that changes the schema. The migration stream of the
product is applied forward and it is never applied backward. A release that
needs the old schema is rolled back with a restore, see
[rollback.md](rollback.md).

## 4. The client of PostgreSQL

The restore rehearsal runs the client of PostgreSQL on the host of the API. The
client must have the same major version as the server, otherwise it refuses to
dump it:

```bash
pg_dump --version
psql --version
```

The image of the API must hold the client of the same major version as the
server. The paths of the client are configuration, so the operator may point
the product at a client of a specific version:

| Variable | What it is |
|---|---|
| `Blindmesh__Operations__PgDumpPath` | The client that dumps the database. |
| `Blindmesh__Operations__PgRestorePath` | The client that restores a dump. |
| `Blindmesh__Operations__PsqlPath` | The client that creates the isolated database. |

## 5. The proof of the deployment

A deployment is only finished when the recovery path is proven as well:

```bash
# The backups of the product are listed.
curl --fail --silent --cookie "$BLINDMESH_COOKIE" \
  "$BLINDMESH_API_URL/v1/admin/operations/backups"

# The restore rehearsal runs against the live database.
curl --fail --silent --cookie "$BLINDMESH_COOKIE" \
  --header "Content-Type: application/json" \
  --header "X-CSRF-Token: $BLINDMESH_CSRF_TOKEN" \
  --data '{"browser_journeys":0,"axe_runs":0}' \
  "$BLINDMESH_API_URL/v1/admin/operations/restores"

# The gate of the pilot is read.
curl --fail --silent --cookie "$BLINDMESH_COOKIE" \
  "$BLINDMESH_API_URL/v1/admin/operations/gate"
```

The gate reports the measured reality. A release is only promoted when every
item of the gate that has an observation window is passed. The items that have
no observation window yet stay a target and they block the promotion, see
[backup-restore.md](backup-restore.md).

## 6. What the deployment never does

- It never deploys itself and it never promotes a release on its own.
- It never writes a ledger row. The credits module stays the only owner of the
  ledger.
- It never puts a secret into a document, a log or a capture. The password of
  the database belongs to the environment of the host.
- It never deletes a backup before the retention day of the backup.
