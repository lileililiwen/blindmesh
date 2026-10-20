"use client";

import { useCallback, useEffect, useRef, useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useSession } from "@/components/session-provider";
import {
  acceptParticipation,
  declineParticipation,
  loadTaskApplications,
  newSeatKey,
  type ApplicationsState,
  type ParticipationCommandState,
} from "@/lib/participations";

/**
 * The applications of a task of the owner. The owner accepts or declines an
 * application here. The acceptance leases exactly one slot of the task, so
 * two owners of the same slot never book it twice.
 */
export function TaskApplications({ taskId }: { taskId: string }) {
  const { state: session, csrfToken } = useSession();
  const [state, setState] = useState<ApplicationsState>({ kind: "loading" });
  const [command, setCommand] = useState<ParticipationCommandState>({ kind: "idle" });
  const [announcement, setAnnouncement] = useState("");
  const commandKeys = useRef<Record<string, string>>({});

  function keyFor(operation: string, participationId: string): string {
    const name = `${operation}:${participationId}`;

    commandKeys.current[name] ??= newSeatKey();

    return commandKeys.current[name];
  }

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadTaskApplications(taskId);

    setState(next);
  }, [taskId]);

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

      const next = await loadTaskApplications(taskId);

      if (!cancelled) {
        setState(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [session.kind, taskId]);

  if (session.kind !== "authenticated" || state.kind === "unauthorized") {
    return null;
  }

  if (state.kind === "loading") {
    return (
      <Card className="mt-6" aria-busy="true">
        <CardHeader>
          <CardTitle>The applications</CardTitle>
          <CardDescription>Loading the applications of this task.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="h-16 w-full rounded bg-muted" aria-hidden="true" />
        </CardContent>
      </Card>
    );
  }

  if (state.kind === "not-found") {
    return null;
  }

  if (state.kind === "forbidden") {
    return (
      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The applications</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-small text-muted-foreground">{state.message}</p>
        </CardContent>
      </Card>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable") {
    return (
      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The applications</CardTitle>
        </CardHeader>
        <CardContent>
          <p role="status" className="text-small text-danger">
            {state.message}
          </p>
          <p className="mt-3">
            <Button type="button" variant="secondary" onClick={() => void load()}>
              Try again
            </Button>
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="mt-6">
      <CardHeader>
        <CardTitle>The applications</CardTitle>
        <CardDescription>
          Every applicant asks for one seat of this task. An accepted seat has to be started within 24
          hours.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {state.items.length === 0 ? (
          <p className="text-small text-muted-foreground">
            No contributor applied for a seat of this task yet.
          </p>
        ) : (
          <ul className="flex flex-col gap-4">
            {state.items.map((item) => (
              <li key={item.id} className="rounded-md border border-border p-3">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-small font-medium">
                    {item.contributor_username || "an unknown contributor"}
                  </span>
                  <Badge tone="neutral">{item.state}</Badge>
                </div>

                <p className="mt-1 text-small text-muted-foreground">
                  Applied {formatMoment(item.applied_at)}.
                </p>

                {item.state === "applied" ? (
                  <div className="mt-3 flex flex-wrap items-center gap-3">
                    <Button
                      type="button"
                      variant="primary"
                      disabled={command.kind === "saving"}
                      aria-busy={command.kind === "saving"}
                      onClick={() => void accept(item.id)}
                    >
                      Accept the application
                    </Button>
                    <Button
                      type="button"
                      variant="secondary"
                      disabled={command.kind === "saving"}
                      onClick={() => void decline(item.id)}
                    >
                      Decline the application
                    </Button>
                  </div>
                ) : null}
              </li>
            ))}
          </ul>
        )}

        <p role="status" aria-live="polite" className="sr-only">
          {announcement}
        </p>

        {command.kind === "refused" || command.kind === "conflict" || command.kind === "forbidden" ? (
          <p className="mt-3 text-small text-warning">{command.message}</p>
        ) : null}

        {command.kind === "error" || command.kind === "unavailable" ? (
          <p className="mt-3 text-small text-danger">{command.message}</p>
        ) : null}
      </CardContent>
    </Card>
  );

  async function accept(participationId: string) {
    setCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await acceptParticipation(
      participationId,
      csrfToken,
      keyFor("accept", participationId),
    );

    setCommand(result);

    if (result.kind === "saved") {
      setAnnouncement("The application is accepted and the seat is leased.");
      await load();
      return;
    }

    if (result.kind === "refused" || result.kind === "conflict" || result.kind === "forbidden") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to answer the application.");
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The application was not accepted.");
  }

  async function decline(participationId: string) {
    setCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await declineParticipation(
      participationId,
      csrfToken,
      keyFor("decline", participationId),
    );

    setCommand(result);

    if (result.kind === "saved") {
      setAnnouncement("The application is declined.");
      await load();
      return;
    }

    if (result.kind === "refused" || result.kind === "conflict" || result.kind === "forbidden") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to answer the application.");
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The application was not declined.");
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
