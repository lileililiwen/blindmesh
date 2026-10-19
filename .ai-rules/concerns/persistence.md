# Persistence concerns

EF Core migrations live only in src/Blindmesh.Infrastructure/Persistence/Migrations, applied in dependency order through a dedicated migration command. Verify fresh PostgreSQL 17 setup and upgrade from the previous package snapshot. Financial entries are immutable; recovery uses compensating transactions. Retain anonymous ledger/audit references when removing identity data.

Use transactions, row locks for monetary and slot transitions, expected version for editable records, stable idempotency keys for repeatable actions, and deterministic clock fixtures. Never treat a timeout as proof that a transaction did not commit.
