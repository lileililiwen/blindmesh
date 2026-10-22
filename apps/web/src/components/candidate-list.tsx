"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { loadCandidates, type CandidateListState } from "@/lib/matching";
import type { CandidateMatchResponse } from "@blindmesh/api-client";

/**
 * The list of the accounts that fit a task. The list belongs to the members of
 * the project of the task. It shows the public profile of every account that
 * fits, the declaration that satisfied every condition and the reason of the
 * place in the order. It never shows a score, a percentage, a rank or a
 * quality claim.
 */
export function CandidateList({ taskId }: { taskId: string }) {
  const { state: session } = useSession();
  const [state, setState] = useState<CandidateListState>({ kind: "loading" });
  const [announcement, setAnnouncement] = useState("");

  const load = useCallback(async () => {
    setState({ kind: "loading" });
    setAnnouncement("");

    const next = await loadCandidates(taskId);

    setState(next);

    if (next.kind === "ready") {
      setAnnouncement(
        next.list.candidates.length === 0
          ? "No account fits the conditions of this task today."
          : `${next.list.candidates.length} accounts fit the conditions of this task.`,
      );
    }
  }, [taskId]);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      if (session.kind === "loading") {
        return;
      }

      if (session.kind !== "authenticated") {
        setState({ kind: "unauthorized" });
        setAnnouncement("Sign in to read the candidates of this task.");
        return;
      }

      const next = await loadCandidates(taskId);

      if (cancelled) {
        return;
      }

      setState(next);

      if (next.kind === "ready") {
        setAnnouncement(
          next.list.candidates.length === 0
            ? "No account fits the conditions of this task today."
            : `${next.list.candidates.length} accounts fit the conditions of this task.`,
        );
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [session.kind, taskId]);

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="candidates-heading" aria-busy="true" className="reading-width">
        <h1 id="candidates-heading" className="text-title font-semibold">
          Candidates
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the candidates of the task.</p>
        <div className="mt-4 h-32 w-full rounded bg-muted" aria-hidden="true" />
      </section>
    );
  }

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="candidates-heading" className="reading-width">
        <h1 id="candidates-heading" className="text-title font-semibold">
          Candidates
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          Sign in to read the candidates of this task. The sign in keeps you on this page.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary" className="min-h-[2.75rem]">
            <a href={`/sign-in?return=%2Ftasks%2F${taskId}%2Fcandidates`}>Sign in to continue</a>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "forbidden" || state.kind === "not-found") {
    return (
      <section aria-labelledby="candidates-heading" className="reading-width">
        <h1 id="candidates-heading" className="text-title font-semibold">
          Candidates
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {state.kind === "forbidden"
            ? state.message
            : "This task does not exist or is not visible to you."}
        </p>
        <p className="mt-4">
          <Button asChild variant="secondary" className="min-h-[2.75rem]">
            <Link href={`/tasks/${taskId}`}>Back to the task</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "unavailable" || state.kind === "error") {
    return (
      <section aria-labelledby="candidates-heading" className="reading-width">
        <h1 id="candidates-heading" className="text-title font-semibold">
          Candidates
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {state.message}
        </p>
        <p className="mt-4">
          <Button type="button" variant="secondary" className="min-h-[2.75rem]" onClick={() => void load()}>
            Try again
          </Button>
        </p>
      </section>
    );
  }

  const list = state.list;
  const conditions = list.conditions ?? [];
  const candidates = list.candidates ?? [];

  return (
    <section aria-labelledby="candidates-heading" className="reading-width">
      <h1 id="candidates-heading" className="text-title font-semibold">
        Candidates
      </h1>

      <p className="mt-2 text-small text-muted-foreground">
        The accounts whose declarations satisfy every hard condition of this task. The order is an
        exploration rule and not a ranking: an account without an accepted contribution comes
        first.
      </p>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The conditions of the task</CardTitle>
          <CardDescription>Every account of the list satisfies all of them.</CardDescription>
        </CardHeader>
        <CardContent>
          {conditions.length === 0 ? (
            <p className="text-small text-muted-foreground">
              This task holds no hard condition, so no account is matched with it.
            </p>
          ) : (
            <ul className="flex flex-col gap-1 text-small">
              {conditions.map((condition) => (
                <li key={`${condition.kind}:${condition.value}`}>
                  {condition.kind}: {condition.value}
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      {candidates.length === 0 ? (
        <p role="status" className="mt-6 text-small">
          No account fits the conditions of this task today.
        </p>
      ) : (
        <ul className="mt-6 flex flex-col gap-4">
          {candidates.map((candidate) => (
            <li key={candidate.account_id}>
              <CandidateCard candidate={candidate} />
            </li>
          ))}
        </ul>
      )}

      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>
    </section>
  );
}

/**
 * One account of the list. The card shows the public profile of the account,
 * the number of its accepted contributions, the reason of its place in the
 * order and the declaration that satisfied every condition. It never shows a
 * score, a percentage or a rank.
 */
function CandidateCard({ candidate }: { candidate: CandidateMatchResponse }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <Link
            href={candidate.profile_reference}
            className="inline-flex min-h-[2.75rem] items-center rounded-md focus-visible:outline-3 focus-visible:outline-offset-2 focus-visible:outline-[var(--focus-ring)]"
          >
            {candidate.display_name || candidate.username}
          </Link>
        </CardTitle>
        <CardDescription>
          {candidate.username} · {candidate.order_reason.text} · {candidate.accepted_contributions}{" "}
          accepted contribution{candidate.accepted_contributions === 1 ? "" : "s"}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <ul className="flex flex-col gap-2 text-small">
          {(candidate.conditions ?? []).map((condition) => (
            <li key={`${condition.kind}:${condition.value}`}>
              <span className="font-medium">
                {condition.kind}: {condition.value}
              </span>{" "}
              <span className={condition.satisfied ? "text-success" : "text-danger"}>
                {condition.satisfied ? "satisfied" : "not satisfied"}
              </span>
              {condition.declaration_label ? (
                <span className="text-muted-foreground">
                  {" "}
                  by the declaration {condition.declaration_label}
                  {condition.declaration_tag ? ` (${condition.declaration_tag})` : ""}
                </span>
              ) : null}
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
