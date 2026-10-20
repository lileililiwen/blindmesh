"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";

import { CapabilityStatusBadge, KindBadge } from "@/components/capability-badges";
import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, Select, TextInput } from "@/components/ui/field";
import {
  evaluateEligibility,
  evaluateTaskEligibility,
  loadOwnCapabilities,
  type CapabilityListState,
  type EligibilityState,
} from "@/lib/capabilities";
import type { CapabilityEligibilityResponse, CapabilityViewResponse } from "@blindmesh/api-client";

const conditionKinds = ["region", "device", "role"] as const;

/**
 * The eligibility of the signed in contributor for a task. The conditions of a
 * task are read through the seam of the task package, so this page never
 * invents a task: a task that is not published yet shows the honest state
 * instead of a result.
 */
export function TaskEligibility({ taskId }: { taskId: string }) {
  const { state: session, csrfToken } = useSession();
  const [taskState, setTaskState] = useState<EligibilityState>({ kind: "evaluating" });
  const [checkState, setCheckState] = useState<EligibilityState>({ kind: "idle" });
  const [declarations, setDeclarations] = useState<CapabilityViewResponse[]>([]);
  const [declarationState, setDeclarationState] = useState<CapabilityListState>({ kind: "loading" });
  const [conditionKind, setConditionKind] = useState<string>("region");
  const [conditionValue, setConditionValue] = useState<string>("");
  const [announcement, setAnnouncement] = useState("");

  const valueRef = useRef<HTMLInputElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  const load = useCallback(async () => {
    setTaskState({ kind: "evaluating" });

    const next = await evaluateTaskEligibility(taskId, csrfToken);

    setTaskState(next);

    if (next.kind === "not-found") {
      setAnnouncement("This task does not exist or is not visible to you.");
    }
  }, [taskId, csrfToken]);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      if (session.kind === "loading") {
        return;
      }

      if (session.kind !== "authenticated") {
        setTaskState({ kind: "unauthorized" });
        return;
      }

      const [task, own] = await Promise.all([
        evaluateTaskEligibility(taskId, csrfToken),
        loadOwnCapabilities(),
      ]);

      if (cancelled) {
        return;
      }

      setTaskState(task);

      if (task.kind === "not-found") {
        setAnnouncement("This task does not exist or is not visible to you.");
      }

      if (own.kind === "ready") {
        setDeclarations(own.capabilities);
      }

      setDeclarationState(own);
    })();

    return () => {
      cancelled = true;
    };
  }, [session.kind, taskId, csrfToken]);

  async function check(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (conditionValue.trim().length === 0) {
      setAnnouncement("Enter the value of the condition first.");
      valueRef.current?.focus();
      return;
    }

    setCheckState({ kind: "evaluating" });
    setAnnouncement("");

    const result = await evaluateEligibility(
      { conditions: [{ kind: conditionKind, value: conditionValue.trim() }] },
      csrfToken,
    );

    setCheckState(result);

    if (result.kind === "evaluated") {
      setAnnouncement(
        result.evaluation.eligible
          ? "Your declarations match the condition."
          : "Your declarations do not match the condition.",
      );
      triggerRef.current?.focus();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("The condition needs another value.");
      valueRef.current?.focus();
      return;
    }

    setAnnouncement("The eligibility is not known right now.");
  }

  if (session.kind === "loading" || taskState.kind === "evaluating") {
    return (
      <section aria-labelledby="eligibility-heading" aria-busy="true" className="reading-width">
        <h1 id="eligibility-heading" className="text-title font-semibold">
          Task eligibility
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the eligibility.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-20 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (taskState.kind === "unauthorized") {
    return (
      <section aria-labelledby="eligibility-heading" className="reading-width">
        <h1 id="eligibility-heading" className="text-title font-semibold">
          Task eligibility
        </h1>
        <p className="mt-3 text-muted-foreground">
          Sign in to see whether you meet the conditions of a task. The sign in keeps you on this page.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <a href={`/sign-in?return=${encodeURIComponent(`/tasks/${taskId}/eligibility`)}`}>
              Sign in to continue
            </a>
          </Button>
        </p>
      </section>
    );
  }

  if (taskState.kind === "error") {
    return (
      <section aria-labelledby="eligibility-heading" className="reading-width">
        <h1 id="eligibility-heading" className="text-title font-semibold">
          Task eligibility
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {taskState.message}
        </p>
        <p className="mt-4">
          <Button type="button" variant="secondary" onClick={() => void load()}>
            Try again
          </Button>
        </p>
      </section>
    );
  }

  return (
    <section aria-labelledby="eligibility-heading" className="reading-width">
      <h1 id="eligibility-heading" className="text-title font-semibold">
        Task eligibility
      </h1>

      {taskState.kind === "not-found" ? (
        <div className="mt-4 rounded-lg border border-border bg-surface p-6">
          <h2 className="text-large font-semibold">This task does not exist or is not visible to you</h2>
          <p className="mt-2 text-muted-foreground">
            The conditions of a task are read from the published task. There is no published task with
            conditions that you may read, so no eligibility is shown for this task.
          </p>
          <p className="mt-4 flex flex-wrap gap-3">
            <Button asChild variant="primary">
              <Link href="/settings/capabilities">Declare a capability</Link>
            </Button>
          </p>
        </div>
      ) : null}

      {taskState.kind === "evaluated" ? (
        <div className="mt-4">
          <h2 className="text-large font-semibold">The conditions of this task</h2>
          <EvaluationResult evaluation={taskState.evaluation} />
        </div>
      ) : null}

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>Check a condition yourself</CardTitle>
          <CardDescription>
            The check uses the same evaluation as a published task. It looks at your public and available
            declarations and never names a private one.
          </CardDescription>
        </CardHeader>

        <CardContent>
          <form className="flex flex-col gap-5" onSubmit={check} noValidate>
            <Field label="Kind of the condition">
              {({ id }) => (
                <Select
                  id={id}
                  name="condition-kind"
                  value={conditionKind}
                  onChange={(event) => setConditionKind(event.target.value)}
                >
                  {conditionKinds.map((kind) => (
                    <option key={kind} value={kind}>
                      {kind}
                    </option>
                  ))}
                </Select>
              )}
            </Field>

            <Field label="Value of the condition" hint="For example the name of your device or your region.">
              {({ id, describedBy }) => (
                <TextInput
                  id={id}
                  name="condition-value"
                  value={conditionValue}
                  aria-describedby={describedBy}
                  ref={valueRef}
                  onChange={(event) => setConditionValue(event.target.value)}
                />
              )}
            </Field>

            <div>
              <Button ref={triggerRef} type="submit">
                Check the condition
              </Button>
            </div>
          </form>

          {checkState.kind === "evaluated" ? <EvaluationResult evaluation={checkState.evaluation} /> : null}

          <p role="status" aria-live="polite" className="sr-only">
            {announcement}
          </p>
        </CardContent>
      </Card>

      <h2 className="mt-10 text-large font-semibold">Your declarations</h2>

      {declarationState.kind === "ready" && declarations.length === 0 ? (
        <p className="mt-2 text-muted-foreground">
          You have not declared a capability yet. Declare one, so a task can be matched against it.
        </p>
      ) : null}

      {declarations.length > 0 ? (
        <ul className="mt-4 flex flex-col gap-4">
          {declarations.map((declaration) => (
            <li key={declaration.id}>
              <article className="rounded-lg border border-border bg-surface p-4 sm:p-5">
                <div className="flex flex-wrap items-center gap-2">
                  <h3 className="text-large font-semibold">{declaration.label}</h3>
                  <KindBadge kind={declaration.kind} />
                  <CapabilityStatusBadge status={declaration.status} />
                </div>

                {declaration.tags.length > 0 ? (
                  <p className="mt-2 text-small text-muted-foreground">Tags: {declaration.tags.join(", ")}</p>
                ) : null}

                <p className="mt-2 text-small text-muted-foreground">
                  {declaration.usable_for_matching
                    ? "This declaration is used for a match."
                    : "This declaration is not used for a match, because a match only uses public and available declarations."}
                </p>
              </article>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}

/** The decision and the reason of every condition of an evaluation. */
function EvaluationResult({ evaluation }: { evaluation: CapabilityEligibilityResponse }) {
  return (
    <div
      className="mt-5 rounded-md border border-border bg-surface p-4"
      data-testid="eligibility-result"
      data-eligible={evaluation.eligible ? "true" : "false"}
    >
      <p className="text-large font-semibold">
        {evaluation.eligible ? "You meet every condition" : "You do not meet every condition"}
      </p>

      <ul className="mt-3 flex flex-col gap-3">
        {evaluation.conditions.map((condition, index) => (
          <li key={`${condition.kind}-${condition.value}-${index}`} className="text-small">
            <span className="font-medium">
              {condition.kind}: {condition.value}
            </span>
            <span className="ml-2 text-muted-foreground">{condition.reason}</span>
          </li>
        ))}
      </ul>

      <p className="mt-3 text-small text-muted-foreground">
        {evaluation.matchable_declarations} of your declarations can be used for a match.
      </p>
    </div>
  );
}
