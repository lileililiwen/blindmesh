# Rollback of Blindmesh

The rollback of a release is a run of the operator. The product never rolls
itself back: there is no endpoint that deploys, promotes or rolls back
anything.

## 1. When the operator rolls back

- The gate of the pilot is blocked and the failed command names the release.
- The readiness of the API fails after the deployment.
- A migration of the release broke the product and the fix needs a new release.

The operator writes the reason and the moment of the rollback in the log of the
pilot. The reason never holds a secret.

## 2. The image of the previous release

The images of the releases carry their version. The previous image is named in
the environment of the host before the rollback:

```bash
export BLINDMESH_API_IMAGE="blindmesh-api:<previous version>"
export BLINDMESH_WORKER_IMAGE="blindmesh-worker:<previous version>"

# The API and the worker of the release that is running are stopped.
docker compose -f infra/compose.prod.yaml stop api worker

# The previous image is started.
docker compose -f infra/compose.prod.yaml up -d api worker

# The readiness of the previous release is read.
curl --fail --silent "$BLINDMESH_API_URL/v1/health/ready"
```

The web application is never rolled back on its own: it reads the API of the
release and it stays compatible with it.

## 3. The database of the previous release

The schema of the database is never moved backward. A release that needs the
old schema is rolled back with a restore of the last backup:

```bash
# The backup of the release is restored into a database of its own first.
# The rehearsal of the product does exactly that, see
# infra/runbooks/backup-restore.md.
curl --fail --silent --cookie "$BLINDMESH_COOKIE" \
  --header "Content-Type: application/json" \
  --header "X-CSRF-Token: $BLINDMESH_CSRF_TOKEN" \
  --data '{}' \
  "$BLINDMESH_API_URL/v1/admin/operations/restores"

# The database of the product is replaced by the restored database.
# The name of the database and the password come from the environment.
PGPASSWORD="$BLINDMESH_PG_PASSWORD" \
  pg_restore --clean --if-exists --no-owner --no-privileges --exit-on-error \
  --host="$BLINDMESH_PG_HOST" --port="${BLINDMESH_PG_PORT:-5432}" \
  --username="$BLINDMESH_PG_USER" --dbname="$BLINDMESH_PG_DATABASE" \
  "$BLINDMESH_DUMP_PATH"
```

The restore replaces the data of the release that is rolled back. It also
removes the rows of the release, so the operator reads the gate of the pilot
again before the traffic returns.

## 4. The proof of the rollback

```bash
# The API of the previous release is ready.
curl --fail --silent "$BLINDMESH_API_URL/v1/health/ready"

# The gate of the pilot is read. It reports the measured reality of the
# restored database.
curl --fail --silent --cookie "$BLINDMESH_COOKIE" \
  "$BLINDMESH_API_URL/v1/admin/operations/gate"
```

## 5. What the rollback never does

- It never moves the schema backward with a migration.
- It never deletes a backup. The backups of the failed release stay available
  until their retention day, so the failure can be read afterwards.
- It never writes a ledger row by hand. The ledger of the restored database is
  the ledger of the backup, and the reconciliation of the rehearsal proves it.
- It never puts a secret into a command that is written down. The password of
  the database belongs to the environment of the host.
