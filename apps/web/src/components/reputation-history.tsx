"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

import { ReputationSummary } from "@/components/reputation-summary";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  loadOwnHistory,
  loadPublicHistory,
  type ReputationHistoryState,
} from "@/lib/reputation";
import type { ReputationEventResponse } from "@blindmesh/api-client";

type Props = {
  /**
   * The contributor of a public history. It is empty for the own history of
   * the signed in contributor.
   */
  username?: string;
};

/**
 * The history of the reputation of a contributor. The public history shows the
 * sources that are public right now; the own history shows every source of
 * the signed in contributor with the references the contributor may read.
 */
export function ReputationHistory({ username }: Props) {
  const [state, setState] = useState<ReputationHistoryState>({ kind: "loading" });

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = username ? await loadPublicHistory(username) : await loadOwnHistory();

    setState(next);
  }, [username]);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = username ? await loadPublicHistory(username) : await loadOwnHistory();

      if (!cancelled) {
        setState(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [username]);

  const heading = username ? `Reputation of ${username}` : "Your reputation";

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="reputation-heading" aria-busy="true" className="reading-width">
        <h1 id="reputation-heading" className="text-title font-semibold">
          {heading}
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the history of the reputation.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-24 w-full rounded bg-muted" />
          <div className="h-16 w-full rounded bg-muted" />
          <div className="h-16 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="reputation-heading" className="reading-width">
        <h1 id="reputation-heading" className="text-title font-semibold">
          {heading}
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          The own history is private. Sign in to read it.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <Link href="/sign-in?return=%2Fsettings%2Freputation">Sign in</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "not-found") {
    return (
      <section aria-labelledby="reputation-heading" className="reading-width">
        <h1 id="reputation-heading" className="text-title font-semibold">
          {heading}
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          This contributor does not exist.
        </p>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="reputation-heading" className="reading-width">
        <h1 id="reputation-heading" className="text-title font-semibold">
          {heading}
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

  const { history } = state;
  const events = history.events ?? [];

  return (
    <section aria-labelledby="reputation-heading" className="reading-width">
      <h1 id="reputation-heading" className="text-title font-semibold">
        {heading}
      </h1>
      <p className="mt-2 text-muted-foreground">
        Every line of the history shows why the score moved. All dates are UTC.
      </p>

      <p role="status" className="sr-only">
        The history is loaded. It holds {events.length} {events.length === 1 ? "line" : "lines"}.
      </p>

      <div className="mt-5">
        <ReputationSummary summary={history} />
      </div>

      {events.length === 0 ? (
        <div className="mt-6 rounded-lg border border-border bg-surface p-6">
          <h2 className="text-large font-semibold">No contribution yet</h2>
          <p className="mt-2 text-muted-foreground">
            {username
              ? "This contributor has no accepted contribution yet. The reputation grows when an owner accepts the work of a seat."
              : "Your history is empty. The reputation grows when an owner accepts the work of one of your seats."}
          </p>
          <p className="mt-4">
            <Button asChild variant="primary">
              <Link href={username ? "/tasks" : "/work"}>{username ? "Browse the tasks" : "Open your work"}</Link>
            </Button>
          </p>
        </div>
      ) : (
        <ul className="mt-6 flex flex-col gap-3">
          {events.map((event) => (
            <li key={event.id}>
              <EventCard event={event} />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/**
 * One line of the history. It shows the reason of the change, the category and
 * the date. A source that the reader may not read never reaches this card.
 */
function EventCard({ event }: { event: ReputationEventResponse }) {
  const delta = Number(event.delta ?? 0);

  return (
    <article className="rounded-lg border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="text-body font-semibold">{event.label}</h2>
        <Badge tone={toneOf(event.category)}>{categoryText(event.category)}</Badge>
        <span className="text-small text-muted-foreground">{delta > 0 ? `+${delta}` : delta}</span>
      </div>

      <p className="mt-1 text-small text-muted-foreground">{formatDate(event.occurred_at)}</p>

      {event.project_title ? (
        <p className="mt-2 text-small">
          Project: <span className="font-medium">{event.project_title}</span>
        </p>
      ) : null}

      {event.finding_id ? (
        <p className="mt-1 text-small text-muted-foreground">Finding: {event.finding_id}</p>
      ) : null}
    </article>
  );
}

/** The name of a category of the reputation. */
function categoryText(category: string): string {
  switch (category) {
    case "accepted_contribution":
      return "Accepted contribution";
    case "confirmed_finding":
      return "Confirmed finding";
    case "reversed_contribution":
      return "Reversed contribution";
    default:
      return "Reputation event";
  }
}

/** The tone of the badge of a category. */
function toneOf(category: string): "neutral" | "success" | "warning" {
  switch (category) {
    case "accepted_contribution":
      return "success";
    case "reversed_contribution":
      return "warning";
    default:
      return "neutral";
  }
}

/** The date of a moment, written as a plain calendar day of UTC. */
function formatDate(value: string): string {
  return new Date(value).toISOString().slice(0, 10);
}
