# Deletion of an identity of Blindmesh

A deletion request is a run of an administrator of the product. The identity of
the subject is removed, the account keeps a pseudonym and the account is
closed. The settled financial records keep their provenance and they never keep
the identity of the subject.

## 1. Who may ask for a deletion

Only an administrator of the product may handle a deletion request:

| Actor | What happens |
|---|---|
| A visitor without a session | The request is refused with `401`. |
| An ordinary account | The request is refused with `403`. |
| A moderator | The request is refused with `403`. |
| An administrator | The request is handled. |

The role is read again on every request, so an account that loses the role
loses the access at once.

## 2. The request

```bash
curl --fail --silent --cookie "$BLINDMESH_COOKIE" \
  --header "Content-Type: application/json" \
  --header "X-CSRF-Token: $BLINDMESH_CSRF_TOKEN" \
  --data "{\"subject_user_id\":\"$BLINDMESH_SUBJECT_USER_ID\",\"scope\":\"$BLINDMESH_SCOPE\"}" \
  "$BLINDMESH_API_URL/v1/admin/operations/deletions"
```

The answer carries the pseudonymous reference of the subject:

```json
{
  "id": "…",
  "received_at": "…",
  "scope": "profile and sessions",
  "status": "completed",
  "pseudonymized_user_id": "deleted-…",
  "audit_reference": "operations_deletion_request:…"
}
```

The answer never holds the email, the username, the display name or the
biography of the subject.

## 3. What is removed and what is kept

| Field | What happens |
|---|---|
| The email of the subject | It is cleared. |
| The display name of the subject | It is cleared. |
| The biography of the subject | It is cleared. |
| The skills of the subject | They are cleared. |
| The languages of the subject | They are cleared. |
| The provider login of the subject | It is cleared. |
| The username of the subject | It becomes the pseudonym of the account. |
| The provider identity of the subject | It is removed, so the account can never sign in again. |
| The sessions of the subject | They are revoked. |
| The profile of the subject | It becomes private. |
| The rows of the credit ledger | They keep the pseudonym of the account. |
| The rows of the audit | They keep the pseudonym of the account. |

The rows of the ledger and the rows of the audit keep the identifier of the
account, and the account keeps only the pseudonym. The settled financial
records stay readable for the audit, and they never hold the identity of the
person.

## 4. The proof of the deletion

```bash
# The requests of the deletion are listed for the operator.
curl --fail --silent --cookie "$BLINDMESH_COOKIE" \
  "$BLINDMESH_API_URL/v1/admin/operations/deletions"

# The account of the subject holds the pseudonym and nothing else.
PGPASSWORD="$BLINDMESH_PG_PASSWORD" \
  psql --host="$BLINDMESH_PG_HOST" --port="${BLINDMESH_PG_PORT:-5432}" \
  --username="$BLINDMESH_PG_USER" --dbname="$BLINDMESH_PG_DATABASE" \
  -c 'SELECT username, email, display_name, github_login, is_public FROM users WHERE id = '"'"$BLINDMESH_SUBJECT_USER_ID"';'
```

The second command proves that the identity is gone. The account of the subject
keeps a pseudonym and the rows of the ledger still belong to it.

## 5. What the deletion never does

- It never deletes a row of the credit ledger. The ledger of the product is
  the truth of the settled money and it keeps its provenance.
- It never deletes a row of the audit.
- It never holds the identity of the subject in the answer, in a log or in a
  capture. The answer carries the pseudonym.
- It never removes the account itself, because the rows of the ledger and the
  rows of the audit refer to it.
