"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { credits } from "@/lib/credits";
import {
  loadParticipation,
  newSeatKey,
  startParticipation,
  withdrawParticipation,
  type ParticipationCommandState,
  type ParticipationDetailState,
} from "@/lib/participations";
import { useSession } from "@/components/session-provider";

/**
 * One seat of a task. The page shows the instructions of the seat, the
 * visible deadline and the primary action of the contributor. A refusal keeps
 * the page and explains what happened.
 */
export function ParticipationDetail({ participationId }: { participationId: string }) {
  const { state: session, csrfToken } = useSession();
  const [state, setState] = useState<ParticipationDetailState>({ kind: "loading" });
  const [command, setCommand] = useState<ParticipationCommandState>({ kind: "idle" });
  const [announcement, setAnnouncement] = useState("");
  const startRef = useRef<HTMLButtonElement>(null);
  const withdrawRef = useRef<HTMLButtonElement>(null);
  const commandKeys = useRef<Record<string, string>>({});

  /** The key of a command. A retry of the same command reuses the key. */
  function keyFor(operation: string): string {
    const name = `${operation}:${participationId}`;

    commandKeys.current[name] ??= newSeatKey();

    return commandKeys.current[name];
  }

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadParticipation(participationId);

    setState(next);
  }, [participationId]);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadParticipation(participationId);

      if (!cancelled) {
        setState(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [participationId]);

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="seat-heading" aria-busy="true" className="reading-width">
        <h1 id="seat-heading" className="text-title font-semibold">
          Your seat
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the seat.</p>
        <div className="mt-4 h-32 w-full rounded bg-muted" aria-hidden="true" />
      </section>
    );
  }

  if (state.kind === "not-found") {
    return (
      <section aria-labelledby="seat-heading" className="reading-width">
        <h1 id="seat-heading" className="text-title font-semibold">
          Your seat
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          This seat does not exist. It may belong to a task of a private project.
        </p>
        <p className="mt-4">
          <Button asChild variant="secondary">
            <Link href="/work">Back to your work</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="seat-heading" className="reading-width">
        <h1 id="seat-heading" className="text-title font-semibold">
          Your seat
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          Sign in to read this seat. The sign in keeps you on this page.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <a href={`/sign-in?return=${encodeURIComponent(`/work/${participationId}`)}`}>
              Sign in to continue
            </a>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="seat-heading" className="reading-width">
        <h1 id="seat-heading" className="text-title font-semibold">
          Your seat
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

  const seat = state.participation;
  const signedIn = session.kind === "authenticated";

  return (
    <section aria-labelledby="seat-heading" className="reading-width">
      <div className="flex flex-wrap items-center gap-2">
        <h1 id="seat-heading" className="text-title font-semibold">
          {seat.task_title}
        </h1>
        <Badge tone={seat.state === "running" ? "success" : "neutral"}>{seat.state}</Badge>
      </div>

      <p className="mt-2 text-small text-muted-foreground">
        {seat.project_name ? `In the project ${seat.project_name}. ` : ""}
        Published by {seat.owner_username || "an unknown contributor"}.
      </p>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The seat</CardTitle>
          <CardDescription>The reward, the state and the deadlines of your seat.</CardDescription>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-small sm:grid-cols-3">
            <div>
              <dt className="text-muted-foreground">Reward of the seat</dt>
              <dd>{credits(seat.slot_reward)} credits</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Slot</dt>
              <dd>{seat.slot_state || "no slot"}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Applied</dt>
              <dd>{formatMoment(seat.applied_at)}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Start before</dt>
              <dd>{seat.start_deadline ? formatMoment(seat.start_deadline) : "no start window"}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Execution deadline</dt>
              <dd>{seat.execution_deadline ? formatMoment(seat.execution_deadline) : "not started"}</dd>
            </div>
          </dl>

          <p className="mt-3 text-small text-muted-foreground">{nextAction(seat.state)}</p>
        </CardContent>
      </Card>

      {seat.instructions_released && seat.scope ? (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>The scope of the task</CardTitle>
            <CardDescription>What you have to do for this seat.</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="whitespace-pre-line text-small">{seat.scope}</p>

            {seat.steps.length > 0 ? (
              <ol className="mt-4 flex flex-col gap-3 text-small">
                {seat.steps.map((step) => (
                  <li key={step.position}>
                    <p className="font-medium">{step.title}</p>
                    <p className="text-muted-foreground">Expected: {step.expected_result}</p>
                  </li>
                ))}
              </ol>
            ) : null}
          </CardContent>
        </Card>
      ) : null}

      <div className="mt-6 flex flex-wrap items-center gap-3">
        {signedIn && seat.can_start ? (
          <Button
            ref={startRef}
            type="button"
            variant="primary"
            disabled={command.kind === "saving"}
            aria-busy={command.kind === "saving"}
            onClick={() => void start()}
          >
            {command.kind === "saving" ? "Starting the work" : "Start the work"}
          </Button>
        ) : null}

        {signedIn && seat.can_withdraw ? (
          <Button
            ref={withdrawRef}
            type="button"
            variant="secondary"
            disabled={command.kind === "saving"}
            onClick={() => void withdraw()}
          >
            Withdraw the seat
          </Button>
        ) : null}

        <Button asChild variant="secondary">
          <Link href="/work">Back to your work</Link>
        </Button>
      </div>

      {seat.history.length > 0 ? (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>The history of the seat</CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="flex flex-col gap-1 text-small">
              {seat.history.map((change) => (
                <li key={`${change.changed_at}:${change.to_status}`}>
                  {change.from_status} to {change.to_status}, by {change.changed_by || "an unknown contributor"}.
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

  async function start() {
    setCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await startParticipation(participationId, csrfToken, keyFor("start"));

    setCommand(result);

    if (result.kind === "saved") {
      setState({ kind: "ready", participation: result.participation });
      setAnnouncement("The work runs. The deadline of the execution is visible on this page.");
      startRef.current?.focus();
      return;
    }

    if (result.kind === "refused" || result.kind === "conflict" || result.kind === "forbidden") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to start the work.");
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The work was not started.");
  }

  async function withdraw() {
    setCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await withdrawParticipation(participationId, csrfToken, keyFor("withdraw"));

    setCommand(result);

    if (result.kind === "saved") {
      setState({ kind: "ready", participation: result.participation });
      setAnnouncement("The seat is withdrawn and the slot goes back to the task.");
      withdrawRef.current?.focus();
      return;
    }

    if (result.kind === "refused" || result.kind === "conflict" || result.kind === "forbidden") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to withdraw the seat.");
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The seat was not withdrawn.");
  }
}

/** The next action of a state of a seat. */
function nextAction(state: string): string {
  switch (state) {
    case "applied":
      return "The owner of the task answers your application.";
    case "accepted":
      return "Start the work within 24 hours.";
    case "running":
      return "The work runs. The deadline of the execution is visible on this page.";
    case "declined":
      return "The owner declined your application.";
    case "withdrawn":
      return "You withdrew this seat.";
    case "expired":
      return "A deadline of this seat passed.";
    default:
      return "";
  }
}

/** A moment of the server in the readable form of the interface. */
function formatMoment(value: string): string {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toISOString().slice(0, 16).replace("T", " ") + " UTC";
}
