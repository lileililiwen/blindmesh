"use client";

import { useCallback, useEffect, useState } from "react";

import { useSession } from "@/components/session-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { CheckboxField, Field, TextInput } from "@/components/ui/field";
import {
  createDeletionRequest,
  loadDeletionRequests,
  loadOperationBackups,
  loadPilotGate,
  loadRestoreRehearsals,
  runRestoreRehearsal,
  type BackupListState,
  type DeletionListState,
  type DeletionRequestState,
  type GateState,
  type PilotGateItem,
  type RehearsalListState,
  type RehearsalRunState,
} from "@/lib/operations";

/**
 * The operations of the pilot for an administrator. The panel shows the
 * measured gate with the exact command of every measurement, the backups, the
 * restore rehearsals and the two commands of the operator: the rehearsal and
 * the deletion request.
 *
 * The panel never shows a target as a measurement and it never shows the
 * identity of a subject of a deletion request.
 */
export function OperationsPanel() {
  const { csrfToken } = useSession();
  const [gate, setGate] = useState<GateState>({ kind: "loading" });
  const [backups, setBackups] = useState<BackupListState>({ kind: "loading" });
  const [rehearsals, setRehearsals] = useState<RehearsalListState>({ kind: "loading" });
  const [deletions, setDeletions] = useState<DeletionListState>({ kind: "loading" });

  const [journeys, setJourneys] = useState("0");
  const [axeRuns, setAxeRuns] = useState("0");
  const [running, setRunning] = useState(false);
  const [run, setRun] = useState<RehearsalRunState | null>(null);

  const [subject, setSubject] = useState("");
  const [scope, setScope] = useState("profile and sessions");
  const [confirmed, setConfirmed] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [deletion, setDeletion] = useState<DeletionRequestState | null>(null);

  const load = useCallback(async () => {
    setGate({ kind: "loading" });
    setBackups({ kind: "loading" });
    setRehearsals({ kind: "loading" });
    setDeletions({ kind: "loading" });

    const [nextGate, nextBackups, nextRehearsals, nextDeletions] = await Promise.all([
      loadPilotGate(),
      loadOperationBackups(),
      loadRestoreRehearsals(),
      loadDeletionRequests(),
    ]);

    setGate(nextGate);
    setBackups(nextBackups);
    setRehearsals(nextRehearsals);
    setDeletions(nextDeletions);
  }, []);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const [nextGate, nextBackups, nextRehearsals, nextDeletions] = await Promise.all([
        loadPilotGate(),
        loadOperationBackups(),
        loadRestoreRehearsals(),
        loadDeletionRequests(),
      ]);

      if (cancelled) {
        return;
      }

      setGate(nextGate);
      setBackups(nextBackups);
      setRehearsals(nextRehearsals);
      setDeletions(nextDeletions);
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const startRehearsal = useCallback(async () => {
    setRunning(true);
    setRun(null);

    const result = await runRestoreRehearsal(
      {
        browser_journeys: toNumber(journeys),
        axe_runs: toNumber(axeRuns),
      },
      csrfToken,
    );

    setRun(result);
    setRunning(false);

    const [nextGate, nextRehearsals] = await Promise.all([loadPilotGate(), loadRestoreRehearsals()]);

    setGate(nextGate);
    setRehearsals(nextRehearsals);
  }, [journeys, axeRuns, csrfToken]);

  const sendDeletion = useCallback(async () => {
    setDeleting(true);
    setDeletion(null);

    const result = await createDeletionRequest(
      { subject_user_id: subject.trim(), scope: scope.trim() },
      csrfToken,
    );

    setDeletion(result);
    setDeleting(false);

    if (result.kind === "requested") {
      setSubject("");
      setConfirmed(false);

      const nextDeletions = await loadDeletionRequests();

      setDeletions(nextDeletions);
    }
  }, [subject, scope, csrfToken]);

  if (gate.kind === "loading") {
    return (
      <section aria-labelledby="operations-heading" aria-busy="true" className="reading-width">
        <h1 id="operations-heading" className="text-title font-semibold">
          The operations of the pilot
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the measured gate of the pilot.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-32 w-full rounded bg-muted" />
          <div className="h-24 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (gate.kind === "unauthorized") {
    return (
      <section aria-labelledby="operations-heading" className="reading-width">
        <h1 id="operations-heading" className="text-title font-semibold">
          The operations of the pilot
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          The operations of the pilot belong to the administrators of the product. Sign in as an administrator to
          read them.
        </p>
      </section>
    );
  }

  if (gate.kind === "forbidden") {
    return (
      <section aria-labelledby="operations-heading" className="reading-width">
        <h1 id="operations-heading" className="text-title font-semibold">
          The operations of the pilot
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          This account may not read the operations of the pilot. The area belongs to the administrators of the
          product.
        </p>
      </section>
    );
  }

  if (gate.kind === "error") {
    return (
      <section aria-labelledby="operations-heading" className="reading-width">
        <h1 id="operations-heading" className="text-title font-semibold">
          The operations of the pilot
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {gate.message}
        </p>
        <p className="mt-4">
          <Button type="button" variant="secondary" onClick={() => void load()}>
            Try again
          </Button>
        </p>
      </section>
    );
  }

  const items = gate.gate.items ?? [];

  return (
    <section aria-labelledby="operations-heading" className="reading-width">
      <h1 id="operations-heading" className="text-title font-semibold">
        The operations of the pilot
      </h1>
      <p className="mt-2 text-muted-foreground">
        Every number below was measured by the product. A target that has no observation window yet stays a
        target and it blocks the promotion.
      </p>

      <p role="status" className="sr-only">
        The gate of the pilot is loaded. {items.filter((item) => item.passed).length} of {items.length} checks
        passed.
      </p>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The measured gate</CardTitle>
          <CardDescription>
            {gate.gate.ready
              ? "Every check of the gate passed."
              : "The release stays blocked. Every check that did not pass names the command that has to run."}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ul className="flex flex-col gap-3">
            {items.map((item) => (
              <li key={item.name}>
                <GateRow item={item} />
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>

      <div className="mt-6 grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>The restore rehearsal</CardTitle>
            <CardDescription>
              The rehearsal dumps the live database, restores it into a database of its own, runs the four checks
              against it and drops it again.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form
              className="flex flex-col gap-3"
              onSubmit={(event) => {
                event.preventDefault();
                void startRehearsal();
              }}
            >
              <Field label="The browser journeys of the pilot run" hint="The number of the journeys you recorded.">
                {({ id, describedBy }) => (
                  <TextInput
                    id={id}
                    aria-describedby={describedBy}
                    inputMode="numeric"
                    min={0}
                    value={journeys}
                    onChange={(event) => setJourneys(event.target.value)}
                  />
                )}
              </Field>

              <Field label="The runs of axe of the pilot run" hint="The number of the axe runs you recorded.">
                {({ id, describedBy }) => (
                  <TextInput
                    id={id}
                    aria-describedby={describedBy}
                    inputMode="numeric"
                    min={0}
                    value={axeRuns}
                    onChange={(event) => setAxeRuns(event.target.value)}
                  />
                )}
              </Field>

              <p>
                <Button type="submit" disabled={running}>
                  {running ? "Running the rehearsal" : "Run the rehearsal"}
                </Button>
              </p>
            </form>

            <div aria-live="polite">
              {run && run.kind === "rehearsed" ? (
                <RehearsalResult rehearsal={run.rehearsal} />
              ) : null}

              {run && run.kind === "not-found" ? (
                <p className="text-small text-danger">
                  The backup of the rehearsal does not exist. Record the backup first.
                </p>
              ) : null}

              {run && run.kind === "error" ? (
                <p className="text-small text-danger">{run.message}</p>
              ) : null}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>The deletion request</CardTitle>
            <CardDescription>
              The identity of the account is removed and the account keeps a pseudonym. The settled financial
              records keep their provenance.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form
              className="flex flex-col gap-3"
              onSubmit={(event) => {
                event.preventDefault();
                void sendDeletion();
              }}
            >
              <Field label="The account of the request" hint="The identifier of the account.">
                {({ id, describedBy }) => (
                  <TextInput
                    id={id}
                    aria-describedby={describedBy}
                    value={subject}
                    onChange={(event) => setSubject(event.target.value)}
                  />
                )}
              </Field>

              <Field label="The scope of the request">
                {({ id, describedBy }) => (
                  <TextInput
                    id={id}
                    aria-describedby={describedBy}
                    value={scope}
                    onChange={(event) => setScope(event.target.value)}
                  />
                )}
              </Field>

              <CheckboxField
                id="deletion-confirmation"
                label="I understand that the identity of this account is removed for good."
                checked={confirmed}
                onChange={(event) => setConfirmed(event.target.checked)}
              />

              <p>
                <Button type="submit" variant="secondary" disabled={!confirmed || deleting}>
                  {deleting ? "Handling the request" : "Handle the deletion request"}
                </Button>
              </p>
            </form>

            <div aria-live="polite">
              {deletion && deletion.kind === "requested" ? (
                <p className="text-small">
                  The request was handled. The account keeps the pseudonym{" "}
                  <span className="font-mono">{deletion.deletion.pseudonymized_user_id}</span>.
                </p>
              ) : null}

              {deletion && deletion.kind === "not-found" ? (
                <p className="text-small text-danger">
                  The account of the request does not exist. The request was not handled.
                </p>
              ) : null}

              {deletion && deletion.kind === "error" ? (
                <p className="text-small text-danger">{deletion.message}</p>
              ) : null}
            </div>
          </CardContent>
        </Card>
      </div>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The backups</CardTitle>
          <CardDescription>The backups the operator recorded, the newest first.</CardDescription>
        </CardHeader>
        <CardContent>
          {backups.kind === "loading" ? (
            <p className="text-small text-muted-foreground" aria-busy="true">
              Loading the backups.
            </p>
          ) : null}

          {backups.kind === "error" ? (
            <div>
              <p className="text-small text-danger">{backups.message}</p>
              <p className="mt-3">
                <Button type="button" variant="secondary" onClick={() => void load()}>
                  Try again
                </Button>
              </p>
            </div>
          ) : null}

          {backups.kind === "ready" && backups.items.length === 0 ? (
            <div className="rounded-lg border border-border bg-surface p-6">
              <h3 className="text-large font-semibold">No backup yet</h3>
              <p className="mt-2 text-muted-foreground">
                Take the first backup of the database and record it with its checksum and its retention day. The
                runbook of the backup and the restore holds the exact commands.
              </p>
            </div>
          ) : null}

          {backups.kind === "ready" && backups.items.length > 0 ? (
            <ul className="flex flex-col gap-3">
              {backups.items.map((backup) => (
                <li key={backup.id} className="rounded-lg border border-border bg-surface p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="text-body font-semibold">{backup.location}</h3>
                    <Badge tone="neutral">{backup.kind}</Badge>
                    <Badge tone={backup.status === "verified" ? "success" : "warning"}>{backup.status}</Badge>
                  </div>
                  <p className="mt-2 break-words font-mono text-small">{backup.checksum_sha256}</p>
                  <p className="mt-1 text-small text-muted-foreground">
                    {formatDate(backup.taken_at)} · {backup.size_bytes} bytes · keep it until{" "}
                    {formatDate(backup.retention_until)}
                  </p>
                  {backup.detail ? <p className="mt-1 text-small">{backup.detail}</p> : null}
                </li>
              ))}
            </ul>
          ) : null}
        </CardContent>
      </Card>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The restore rehearsals</CardTitle>
          <CardDescription>The rehearsals of the pilot, the newest first.</CardDescription>
        </CardHeader>
        <CardContent>
          {rehearsals.kind === "loading" ? (
            <p className="text-small text-muted-foreground" aria-busy="true">
              Loading the rehearsals.
            </p>
          ) : null}

          {rehearsals.kind === "error" ? (
            <div>
              <p className="text-small text-danger">{rehearsals.message}</p>
              <p className="mt-3">
                <Button type="button" variant="secondary" onClick={() => void load()}>
                  Try again
                </Button>
              </p>
            </div>
          ) : null}

          {rehearsals.kind === "ready" && rehearsals.items.length === 0 ? (
            <div className="rounded-lg border border-border bg-surface p-6">
              <h3 className="text-large font-semibold">No rehearsal yet</h3>
              <p className="mt-2 text-muted-foreground">
                Run the first rehearsal. It proves that a backup may be restored, and it blocks the promotion
                when a check fails.
              </p>
            </div>
          ) : null}

          {rehearsals.kind === "ready" && rehearsals.items.length > 0 ? (
            <ul className="flex flex-col gap-3">
              {rehearsals.items.map((rehearsal) => (
                <li key={rehearsal.id}>
                  <RehearsalResult rehearsal={rehearsal} />
                </li>
              ))}
            </ul>
          ) : null}
        </CardContent>
      </Card>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The deletion requests</CardTitle>
          <CardDescription>
            The requests of the operator. They hold the pseudonym of the account and never the identity of the
            subject.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {deletions.kind === "loading" ? (
            <p className="text-small text-muted-foreground" aria-busy="true">
              Loading the requests.
            </p>
          ) : null}

          {deletions.kind === "error" ? (
            <div>
              <p className="text-small text-danger">{deletions.message}</p>
              <p className="mt-3">
                <Button type="button" variant="secondary" onClick={() => void load()}>
                  Try again
                </Button>
              </p>
            </div>
          ) : null}

          {deletions.kind === "ready" && deletions.items.length === 0 ? (
            <div className="rounded-lg border border-border bg-surface p-6">
              <h3 className="text-large font-semibold">No request yet</h3>
              <p className="mt-2 text-muted-foreground">
                A request removes the identity of an account and keeps the pseudonym. The runbook of the deletion
                holds the exact commands.
              </p>
            </div>
          ) : null}

          {deletions.kind === "ready" && deletions.items.length > 0 ? (
            <ul className="flex flex-col gap-3">
              {deletions.items.map((request) => (
                <li key={request.id} className="rounded-lg border border-border bg-surface p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="text-body font-semibold">{request.scope}</h3>
                    <Badge tone={request.status === "completed" ? "success" : "warning"}>{request.status}</Badge>
                  </div>
                  <p className="mt-2 break-words font-mono text-small">{request.pseudonymized_user_id}</p>
                  <p className="mt-1 text-small text-muted-foreground">
                    {formatDate(request.received_at)} · {request.audit_reference}
                  </p>
                </li>
              ))}
            </ul>
          ) : null}
        </CardContent>
      </Card>
    </section>
  );
}

/**
 * One item of the measured gate. It shows the value that was measured, the
 * value that is expected and the exact command that produced the measurement.
 */
function GateRow({ item }: { item: PilotGateItem }) {
  return (
    <article className="rounded-lg border border-border bg-surface p-4" aria-labelledby={`gate-${item.name}`}>
      <div className="flex flex-wrap items-center gap-2">
        <h3 id={`gate-${item.name}`} className="text-body font-semibold">
          {item.name.replace(/_/g, " ")}
        </h3>
        <Badge tone={item.passed ? "success" : "warning"}>
          <span aria-hidden="true">{item.passed ? "Passed" : "Blocked"}</span>
          <span className="sr-only">{item.passed ? "The check passed." : "The check did not pass."}</span>
        </Badge>
      </div>

      <dl className="mt-2 flex flex-col gap-1 text-small">
        <div className="flex flex-wrap gap-2">
          <dt className="font-medium">Measured</dt>
          <dd className="break-words">{item.measured}</dd>
        </div>
        <div className="flex flex-wrap gap-2">
          <dt className="font-medium">Expected</dt>
          <dd className="break-words">{item.expected}</dd>
        </div>
      </dl>

      <p className="mt-2 break-words font-mono text-small text-muted-foreground">{item.command}</p>
    </article>
  );
}

/** The four verdicts of one rehearsal. */
function RehearsalResult({ rehearsal }: { rehearsal: { status: string; namespace: string; schema_check: string; data_check: string; object_check: string; ledger_check: string; started_at: string; notes: string | null } }) {
  return (
    <article className="rounded-lg border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="text-body font-semibold">The rehearsal of {formatDate(rehearsal.started_at)}</h3>
        <Badge tone={rehearsal.status === "passed" ? "success" : "warning"}>{rehearsal.status}</Badge>
      </div>

      <p className="mt-1 break-words font-mono text-small text-muted-foreground">{rehearsal.namespace}</p>

      <dl className="mt-2 flex flex-col gap-1 text-small">
        <Verdict label="Schema" value={rehearsal.schema_check} />
        <Verdict label="Data" value={rehearsal.data_check} />
        <Verdict label="Objects" value={rehearsal.object_check} />
        <Verdict label="Ledger" value={rehearsal.ledger_check} />
      </dl>

      {rehearsal.notes ? <p className="mt-2 text-small text-muted-foreground">{rehearsal.notes}</p> : null}
    </article>
  );
}

/** One verdict of a rehearsal. */
function Verdict({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-wrap gap-2">
      <dt className="font-medium">{label}</dt>
      <dd className="break-words">{value}</dd>
    </div>
  );
}

/** The number of a field of the operator, or zero when the field is empty. */
function toNumber(value: string): number {
  const parsed = Number.parseInt(value.trim(), 10);

  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

/** The date of a moment, written as a plain calendar day of UTC. */
function formatDate(value: string): string {
  return new Date(value).toISOString().slice(0, 16).replace("T", " ");
}
