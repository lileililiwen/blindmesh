"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

import { useSession } from "@/components/session-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { credits } from "@/lib/credits";
import { loadWork, type WorkListState } from "@/lib/participations";

/**
 * The work of the signed in contributor. Every seat shows its state and the
 * next action, so the contributor never has to guess what to do next.
 */
export function ParticipationList() {
  const { state: session } = useSession();
  const [state, setState] = useState<WorkListState>({ kind: "loading" });

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadWork();

    setState(next);
  }, []);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      if (session.kind === "loading") {
        return;
      }

      if (session.kind !== "authenticated") {
        setState({ kind: "unauthorized" });
        return;
      }

      const next = await loadWork();

      if (!cancelled) {
        setState(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [session.kind]);

  if (session.kind === "loading" || state.kind === "loading") {
    return (
      <section aria-labelledby="work-heading" aria-busy="true" className="reading-width">
        <h1 id="work-heading" className="text-title font-semibold">
          Your work
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading your work.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-20 w-full rounded bg-muted" />
          <div className="h-20 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="work-heading" className="reading-width">
        <h1 id="work-heading" className="text-title font-semibold">
          Your work
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          Sign in to see the seats of the tasks you applied for.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <a href="/sign-in?return=%2Fwork">Sign in to continue</a>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "not-found" || state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="work-heading" className="reading-width">
        <h1 id="work-heading" className="text-title font-semibold">
          Your work
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {state.kind === "not-found"
            ? "There is no work for this account."
            : state.message}
        </p>
        <p className="mt-4 flex flex-wrap gap-3">
          <Button type="button" variant="secondary" onClick={() => void load()}>
            Try again
          </Button>
          <Button asChild variant="secondary">
            <Link href="/tasks">Back to the tasks</Link>
          </Button>
        </p>
      </section>
    );
  }

  return (
    <section aria-labelledby="work-heading" className="reading-width">
      <h1 id="work-heading" className="text-title font-semibold">
        Your work
      </h1>

      <p className="mt-2 text-small text-muted-foreground">
        Every seat of a task you applied for. The state of the seat tells you what to do next.
      </p>

      {state.items.length === 0 ? (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>No seat yet</CardTitle>
            <CardDescription>The tasks of the marketplace are waiting for a contributor.</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-small text-muted-foreground">
              You did not apply for a slot of a task yet. Choose a task of the marketplace and apply for
              one of its seats.
            </p>
            <p className="mt-4">
              <Button asChild variant="primary">
                <Link href="/tasks">Browse the tasks</Link>
              </Button>
            </p>
          </CardContent>
        </Card>
      ) : (
        <ul className="mt-6 flex flex-col gap-4">
          {state.items.map((item) => (
            <li key={item.id}>
              <Card>
                <CardHeader>
                  <div className="flex flex-wrap items-center gap-2">
                    <CardTitle>{item.task_title}</CardTitle>
                    <SeatBadge state={item.state} />
                  </div>
                  <CardDescription>
                    {item.project_name ? `In the project ${item.project_name}. ` : ""}
                    Published by {item.owner_username || "an unknown contributor"}.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-small sm:grid-cols-3">
                    <div>
                      <dt className="text-muted-foreground">Reward of the seat</dt>
                      <dd>{credits(item.slot_reward)} credits</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">Applied</dt>
                      <dd>{formatMoment(item.applied_at)}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">Start before</dt>
                      <dd>{item.start_deadline ? formatMoment(item.start_deadline) : "no start window"}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">Execution deadline</dt>
                      <dd>
                        {item.execution_deadline ? formatMoment(item.execution_deadline) : "not started"}
                      </dd>
                    </div>
                  </dl>

                  <p className="mt-3 text-small text-muted-foreground">{nextAction(item.state)}</p>

                  <p className="mt-4">
                    <Button asChild variant="secondary">
                      <Link href={`/work/${item.id}`}>Open the seat</Link>
                    </Button>
                  </p>
                </CardContent>
              </Card>
            </li>
          ))}
        </ul>
      )}

    </section>
  );
}

/** The next action of a state of a seat. */
function nextAction(state: string): string {
  switch (state) {
    case "applied":
      return "The owner of the task answers your application.";
    case "accepted":
      return "Start the work within 24 hours.";
    case "running":
      return "The work runs. The deadline of the execution is visible in your seat.";
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

/** The state of a seat, shown as a badge. */
function SeatBadge({ state }: { state: string }) {
  const tone =
    state === "running" ? "success" : state === "expired" || state === "declined" ? "danger" : "neutral";

  return <Badge tone={tone}>{state}</Badge>;
}

/** A moment of the server in the readable form of the interface. */
function formatMoment(value: string): string {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toISOString().slice(0, 16).replace("T", " ") + " UTC";
}
