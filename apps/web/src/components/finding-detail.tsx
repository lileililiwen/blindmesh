"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, TextArea } from "@/components/ui/field";
import { useSession } from "@/components/session-provider";
import {
  loadFinding,
  newEvidenceKey,
  patchFinding,
  type FindingCommandState,
  type FindingState,
} from "@/lib/findings";

const states = ["triaged", "confirmed", "rejected", "duplicate", "fixing", "resolved"];

/**
 * One finding of the evidence. The owner classifies the finding, and the
 * decision never decides the earned base pay of the submission: a rejected
 * finding confiscates nothing. The page shows the committed state and the
 * history of the decision.
 */
export function FindingDetail({ findingId }: { findingId: string }) {
  const { state: session, csrfToken } = useSession();
  const [state, setState] = useState<FindingState>({ kind: "loading" });
  const [command, setCommand] = useState<FindingCommandState>({ kind: "idle" });
  const [announcement, setAnnouncement] = useState("");
  const [target, setTarget] = useState("triaged");
  const [note, setNote] = useState("");
  const saveRef = useRef<HTMLButtonElement>(null);
  const commandKeys = useRef<Record<string, string>>({});

  function keyFor(operation: string): string {
    const name = `${operation}:${findingId}`;

    commandKeys.current[name] ??= newEvidenceKey();

    return commandKeys.current[name];
  }

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadFinding(findingId);

    setState(next);
  }, [findingId]);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadFinding(findingId);

      if (!cancelled) {
        setState(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [findingId]);

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="finding-heading" aria-busy="true" className="reading-width">
        <h1 id="finding-heading" className="text-title font-semibold">
          The finding
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the finding.</p>
        <div className="mt-4 h-32 w-full rounded bg-muted" aria-hidden="true" />
      </section>
    );
  }

  if (state.kind === "not-found") {
    return (
      <section aria-labelledby="finding-heading" className="reading-width">
        <h1 id="finding-heading" className="text-title font-semibold">
          The finding
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          This finding does not exist. It may belong to a project you may not see.
        </p>
        <p className="mt-4">
          <Button asChild variant="secondary">
            <Link href="/projects">Back to your projects</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="finding-heading" className="reading-width">
        <h1 id="finding-heading" className="text-title font-semibold">
          The finding
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          Sign in to read this finding. The sign in keeps you on this page.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <a href={`/sign-in?return=${encodeURIComponent(`/findings/${findingId}`)}`}>
              Sign in to continue
            </a>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "forbidden" || state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="finding-heading" className="reading-width">
        <h1 id="finding-heading" className="text-title font-semibold">
          The finding
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {state.message}
        </p>
        <p className="mt-4">
          <Button type="button" variant="secondary" onClick={() => void load()}>
            Try again
          </Button>
        </p>
      </section>
    );
  }

  const finding = state.finding;
  const signedIn = session.kind === "authenticated";
  const fieldErrors = command.kind === "invalid" ? command.fieldErrors : {};

  return (
    <section aria-labelledby="finding-heading" className="reading-width">
      <div className="flex flex-wrap items-center gap-2">
        <h1 id="finding-heading" className="text-title font-semibold">
          {finding.title}
        </h1>
        <Badge tone={finding.state === "confirmed" ? "warning" : "neutral"}>{finding.state}</Badge>
      </div>

      <p className="mt-2 text-small text-muted-foreground">
        {finding.category} / {finding.severity}. The decision about a finding never decides the earned
        base pay of the seat.
      </p>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The finding</CardTitle>
          <CardDescription>How to reproduce it and what the contributor saw.</CardDescription>
        </CardHeader>
        <CardContent>
          <dl className="flex flex-col gap-3 text-small">
            <div>
              <dt className="font-medium">How to reproduce it</dt>
              <dd className="whitespace-pre-line text-muted-foreground">
                {finding.reproduction || "The contributor wrote no reproduction."}
              </dd>
            </div>
            <div>
              <dt className="font-medium">The expected result</dt>
              <dd className="whitespace-pre-line text-muted-foreground">
                {finding.expected_result || "The contributor wrote no expected result."}
              </dd>
            </div>
            <div>
              <dt className="font-medium">The actual result</dt>
              <dd className="whitespace-pre-line text-muted-foreground">
                {finding.actual_result || "The contributor wrote no actual result."}
              </dd>
            </div>
            {finding.decision_note ? (
              <div>
                <dt className="font-medium">The note of the decision</dt>
                <dd className="whitespace-pre-line text-muted-foreground">{finding.decision_note}</dd>
              </div>
            ) : null}
          </dl>
        </CardContent>
      </Card>

      {signedIn ? (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>Classify the finding</CardTitle>
            <CardDescription>
              A rejected finding confiscates nothing. A disagreement about the completion of the seat
              opens the dispute on the page of the reviews.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="flex flex-col gap-4">
              <Field label="The new state of the finding" error={fieldErrors.state}>
                {({ id, describedBy, invalid }) => (
                  <select
                    id={id}
                    aria-describedby={describedBy}
                    aria-invalid={invalid}
                    className="min-h-[var(--component-control-min-height)] w-full rounded-md border border-border bg-surface px-3 py-2 text-body"
                    value={target}
                    onChange={(event) => setTarget(event.target.value)}
                  >
                    {states.map((value) => (
                      <option key={value} value={value}>
                        {value}
                      </option>
                    ))}
                  </select>
                )}
              </Field>

              <Field label="The note of the decision" error={fieldErrors.note}>
                {({ id, describedBy, invalid }) => (
                  <TextArea
                    id={id}
                    rows={3}
                    aria-describedby={describedBy}
                    aria-invalid={invalid}
                    value={note}
                    onChange={(event) => setNote(event.target.value)}
                  />
                )}
              </Field>

              <div>
                <Button
                  ref={saveRef}
                  type="button"
                  variant="primary"
                  disabled={command.kind === "saving"}
                  onClick={() => void save()}
                >
                  {command.kind === "saving" ? "Saving the decision" : "Save the decision"}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      ) : null}

      {finding.history.length > 0 ? (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>The history of the finding</CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="flex flex-col gap-1 text-small">
              {finding.history.map((change) => (
                <li key={`${change.changed_at}:${change.to_state}`}>
                  {change.from_state} to {change.to_state}, by {change.changed_by_username || "an unknown contributor"}.
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      ) : null}

      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>

      {command.kind === "refused" || command.kind === "conflict" || command.kind === "forbidden" ? (
        <p className="mt-4 text-small text-warning">{command.message}</p>
      ) : null}

      {command.kind === "error" || command.kind === "unavailable" ? (
        <p className="mt-4 text-small text-danger">{command.message}</p>
      ) : null}
    </section>
  );

  async function save() {
    setCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await patchFinding(
      findingId,
      { state: target, note: note.length > 0 ? note : undefined },
      csrfToken,
      keyFor("patch"),
    );

    setCommand(result);

    if (result.kind === "saved") {
      setState({ kind: "ready", finding: result.finding });
      setAnnouncement(`The finding is now ${result.finding.state}.`);
      saveRef.current?.focus();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("Some fields of the decision are not valid.");
      return;
    }

    if (result.kind === "refused" || result.kind === "conflict" || result.kind === "forbidden") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to classify the finding.");
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The decision was not saved.");
  }
}
