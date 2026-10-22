# Backup and restore of Blindmesh

The backup of the pilot is a run of the operator. The product never backs
itself up and it never restores itself into the live database on its own.

Every secret of the run comes from the environment of the host. The password of
the database belongs to the environment of the process and never to a
document, a log or a capture.

## 1. The client of PostgreSQL

The client of the host must have the same major version as the server,
otherwise it refuses to dump it:

```bash
pg_dump --version
psql --version
```

The paths of the client are configuration:

| Variable | What it is |
|---|---|
| `Blindmesh__Operations__PgDumpPath` | The client that dumps the database. |
| `Blindmesh__Operations__PgRestorePath` | The client that restores a dump. |
| `Blindmesh__Operations__PsqlPath` | The client that creates the isolated database. |

## 2. The daily backup

```bash
# The dump of the database of the product. The password belongs to the
# environment of the shell, never to the command.
export PGPASSWORD="$BLINDMESH_PG_PASSWORD"

pg_dump --format=custom --no-owner --no-privileges \
  --host="$BLINDMESH_PG_HOST" --port="${BLINDMESH_PG_PORT:-5432}" \
  --username="$BLINDMESH_PG_USER" --dbname="$BLINDMESH_PG_DATABASE" \
  --file="$BLINDMESH_DUMP_PATH"

# The checksum of the dump. The operator writes it down with the backup.
sha256sum "$BLINDMESH_DUMP_PATH"
```

The backup of the private bucket is taken with the client of the object
storage and it is recorded the same way, with the kind `objects`.

## 3. The record of the backup

The operator records the backup in the product, so the restore rehearsal and
the gate know what the backups of the pilot are:

```bash
curl --fail --silent --cookie "$BLINDMESH_COOKIE" \
  --header "Content-Type: application/json" \
  --header "X-CSRF-Token: $BLINDMESH_CSRF_TOKEN" \
  --data "{\"kind\":\"database\",\"location\":\"$BLINDMESH_BACKUP_LOCATION\",\"size_bytes\":$BLINDMESH_BACKUP_SIZE,\"checksum_sha256\":\"$BLINDMESH_BACKUP_CHECKSUM\",\"retention_until\":\"$BLINDMESH_RETENTION_UNTIL\",\"detail\":\"$BLINDMESH_BACKUP_DETAIL\"}" \
  "$BLINDMESH_API_URL/v1/admin/operations/backups"
```

The retention of a backup is 30 days. The product never deletes a backup on
its own.

## 4. The restore rehearsal

The rehearsal is the proof that a backup may be restored. It is real: the
product dumps the live database, restores the dump into a database of its own,
runs four checks against that database and drops the isolated database again.

```bash
curl --fail --silent --cookie "$BLINDMESH_COOKIE" \
  --header "Content-Type: application/json" \
  --header "X-CSRF-Token: $BLINDMESH_CSRF_TOKEN" \
  --data "{\"backup_id\":\"$BLINDMESH_BACKUP_ID\",\"browser_journeys\":$BLINDMESH_BROWSER_JOURNEYS,\"axe_runs\":$BLINDMESH_AXE_RUNS}" \
  "$BLINDMESH_API_URL/v1/admin/operations/restores"
```

The answer holds the four verdicts of the rehearsal:

| Verdict | What it proves |
|---|---|
| `schema_check` | The restored database holds the schema of the release. |
| `data_check` | The row counts of the tables match the live database. |
| `object_check` | The objects the database refers to and the checksum of their keys match the live database. |
| `ledger_check` | Every transaction of the journal sums to zero and no account with an owner holds a negative balance. |

A rehearsal with a failed check has the status `failed` and it blocks the
promotion. The gate of the pilot then reports the item `restore_rehearsal` as
failed and it names the command of the check that has to run again.

## 5. The restore into the live database

The restore into the live database is a run of the operator and it happens only
after a failed rehearsal or a failed release:

```bash
# The database of the product is replaced by the dump of the backup.
PGPASSWORD="$BLINDMESH_PG_PASSWORD" \
  pg_restore --clean --if-exists --no-owner --no-privileges --exit-on-error \
  --host="$BLINDMESH_PG_HOST" --port="${BLINDMESH_PG_PORT:-5432}" \
  --username="$BLINDMESH_PG_USER" --dbname="$BLINDMESH_PG_DATABASE" \
  "$BLINDMESH_DUMP_PATH"

# The rehearsal runs again against the restored database.
curl --fail --silent --cookie "$BLINDMESH_COOKIE" \
  --header "Content-Type: application/json" \
  --header "X-CSRF-Token: $BLINDMESH_CSRF_TOKEN" \
  --data '{}' \
  "$BLINDMESH_API_URL/v1/admin/operations/restores"
```

The rehearsal after the restore is the proof that the restored database is
usable. The traffic returns to the product only when the rehearsal passed.

## 6. The isolated database of a manual rehearsal

The operator may run the rehearsal of the product by hand. The name of the
isolated database is quoted, otherwise it is folded to lower case and the
client reads another database:

```bash
PGPASSWORD="$BLINDMESH_PG_PASSWORD" \
  psql --host="$BLINDMESH_PG_HOST" --port="${BLINDMESH_PG_PORT:-5432}" \
  --username="$BLINDMESH_PG_USER" --dbname=postgres \
  -c 'CREATE DATABASE "bm_rehearsal_manual";'

PGPASSWORD="$BLINDMESH_PG_PASSWORD" \
  pg_restore --no-owner --no-privileges --exit-on-error \
  --host="$BLINDMESH_PG_HOST" --port="${BLINDMESH_PG_PORT:-5432}" \
  --username="$BLINDMESH_PG_USER" --dbname="bm_rehearsal_manual" \
  "$BLINDMESH_DUMP_PATH"

# The isolated database is dropped again.
PGPASSWORD="$BLINDMESH_PG_PASSWORD" \
  psql --host="$BLINDMESH_PG_HOST" --port="${BLINDMESH_PG_PORT:-5432}" \
  --username="$BLINDMESH_PG_USER" --dbname=postgres \
  -c 'DROP DATABASE IF EXISTS "bm_rehearsal_manual" WITH (FORCE);'
```

## 7. What the backup never does

- It never writes a ledger row. The ledger of the backup is the ledger of the
  moment of the backup and the rehearsal proves that it is balanced.
- It never puts a password into the arguments of a command. The password
  belongs to the environment of the process.
- It never deletes the evidence of a failure. The dumps of a failed release
  stay available until their retention day.
