"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";

import { TaskModeBadges, TaskStateBadge, TaskVisibilityBadge } from "@/components/task-badges";
import { TaskApplications } from "@/components/task-applications";
import { TaskForm } from "@/components/task-form";
import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { credits } from "@/lib/credits";
import { cancelTask, loadTask, publishTask, type TaskCommandState, type TaskDetailState } from "@/lib/tasks";
/**
 * The committed state of a task with the actions of its owner. The publish
 * reserves the budget of the task and the cancel closes its admission. A
 * refusal keeps the page and explains what happened.
 */
export function TaskDetail({ taskId }: { taskId: string }) {
  const { state: session, csrfToken } = useSession();
  const [state, setState] = useState<TaskDetailState>({ kind: "loading" });
  const [command, setCommand] = useState<TaskCommandState>({ kind: "idle" });
  const [announcement, setAnnouncement] = useState("");
  const [editing, setEditing] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const publishRef = useRef<HTMLButtonElement>(null);
  const cancelTriggerRef = useRef<HTMLButtonElement>(null);
  const confirmRef = useRef<HTMLButtonElement>(null);
  const commandKeys = useRef<Record<string, string>>({});

  /**
   * The key of a money command. The same key is used for a retry of the same
   * command, so a request that reached the server twice never moves the
   * credits twice.
   */
  function keyFor(operation: string): string {
    const name = `${operation}:${taskId}`;

    commandKeys.current[name] ??= createIdempotencyKey();

    return commandKeys.current[name];
  }

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadTask(taskId);

    setState(next);
  }, [taskId]);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadTask(taskId);

      if (!cancelled) {
        setState(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [taskId]);

  useEffect(() => {
    if (confirming) {
      confirmRef.current?.focus();
    }
  }, [confirming]);

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="task-heading" aria-busy="true" className="reading-width">
        <h1 id="task-heading" className="text-title font-semibold">
          Task
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the task.</p>
        <div className="mt-4 h-32 w-full rounded bg-muted" aria-hidden="true" />
      </section>
    );
  }

  if (state.kind === "not-found") {
    return (
      <section aria-labelledby="task-heading" className="reading-width">
        <h1 id="task-heading" className="text-title font-semibold">
          Task
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          This task does not exist. It may belong to a private project.
        </p>
        <p className="mt-4">
          <Button asChild variant="secondary">
            <Link href="/tasks">Back to the tasks</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "unauthorized" || state.kind === "forbidden") {
    return (
      <section aria-labelledby="task-heading" className="reading-width">
        <h1 id="task-heading" className="text-title font-semibold">
          Task
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {state.kind === "unauthorized"
            ? "Sign in to read this task. The sign in keeps you on this page."
            : state.message}
        </p>
        <p className="mt-4 flex flex-wrap gap-3">
          {state.kind === "unauthorized" ? (
            <Button asChild variant="primary">
              <a href={`/sign-in?return=%2Ftasks%2F${taskId}`}>Sign in to continue</a>
            </Button>
          ) : null}
          <Button asChild variant="secondary">
            <Link href="/tasks">Back to the tasks</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="task-heading" className="reading-width">
        <h1 id="task-heading" className="text-title font-semibold">
          Task
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

  const task = state.task;
  const signedIn = session.kind === "authenticated";

  return (
    <section aria-labelledby="task-heading" className="reading-width">
      <div className="flex flex-wrap items-center gap-2">
        <h1 id="task-heading" className="text-title font-semibold">
          {task.title}
        </h1>
        <TaskStateBadge status={task.status} />
        <TaskVisibilityBadge visibility={task.visibility} />
        <TaskModeBadges modes={task.modes} />
      </div>

      <p className="mt-2 text-small text-muted-foreground">
        {task.project_name ? `In the project ${task.project_name}. ` : ""}
        Published by {task.owner_username || "an unknown contributor"}.
      </p>

      {task.summary_only ? (
        <p className="mt-4 rounded-lg border border-border bg-muted p-4 text-small">
          This task is matched. Its instructions are released to the contributors that match the
          requirements of the task.
        </p>
      ) : null}

      {task.scope ? (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>Scope</CardTitle>
            <CardDescription>What the contributor has to do.</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="whitespace-pre-line">{task.scope}</p>
          </CardContent>
        </Card>
      ) : null}

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The rules of the task</CardTitle>
          <CardDescription>The budget, the deadline and the slots of the task.</CardDescription>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-small sm:grid-cols-3">
            <div>
              <dt className="text-muted-foreground">Budget</dt>
              <dd>{credits(task.maximum_budget)} credits</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Reserved</dt>
              <dd>{credits(task.reserved_budget)} credits</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Slots</dt>
              <dd>{task.slot_count}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Expected minutes</dt>
              <dd>{task.expected_minutes}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Deadline</dt>
              <dd>{task.deadline ? formatMoment(task.deadline) : "no deadline"}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Base and bonus</dt>
              <dd>
                {credits(task.base_reward)} and {credits(task.slot_bonus)} credits
              </dd>
            </div>
          </dl>

          {task.accepts_clean_findings ? (
            <p className="mt-3 text-small">A report without a finding is a valid result.</p>
          ) : null}

          {task.access_kind ? (
            <p className="mt-3 text-small">
              Access: {task.access_kind === "owner_operated" ? "a resource of the owner" : "a delegated consent"}.
            </p>
          ) : null}

          {task.review_criteria ? (
            <p className="mt-3 text-small">The assessment is rated by {task.review_criteria}</p>
          ) : null}
        </CardContent>
      </Card>

      {task.requirements.length > 0 ? (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>Hard requirements</CardTitle>
            <CardDescription>The capability the contributor has to offer.</CardDescription>
          </CardHeader>
          <CardContent>
            <ul className="flex flex-col gap-1 text-small">
              {task.requirements.map((requirement) => (
                <li key={`${requirement.kind}:${requirement.value}`}>
                  {requirement.kind}: {requirement.value}
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      ) : null}

      {task.steps.length > 0 ? (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>Steps of the verification</CardTitle>
            <CardDescription>What the contributor does and what is expected.</CardDescription>
          </CardHeader>
          <CardContent>
            <ol className="flex flex-col gap-3 text-small">
              {task.steps.map((step) => (
                <li key={step.position}>
                  <p className="font-medium">{step.title}</p>
                  <p className="text-muted-foreground">Expected: {step.expected_result}</p>
                </li>
              ))}
            </ol>
          </CardContent>
        </Card>
      ) : null}

      {task.slots.length > 0 ? (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>The slots</CardTitle>
            <CardDescription>One slot pays one contributor.</CardDescription>
          </CardHeader>
          <CardContent>
            <ul className="flex flex-col gap-1 text-small">
              {task.slots.map((slot) => (
                <li key={slot.id}>
                  Slot {slot.position}: {credits(slot.reward)} credits, {slot.state}.
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      ) : null}

      {task.status_history.length > 0 ? (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>The history of the task</CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="flex flex-col gap-1 text-small">
              {task.status_history.map((change) => (
                <li key={`${change.changed_at}:${change.to_status}`}>
                  {change.from_status} to {change.to_status}, by {change.changed_by || "an unknown contributor"}.
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      ) : null}

      {task.can_manage ? <TaskApplications taskId={task.id} /> : null}

      <div className="mt-6 flex flex-wrap items-center gap-3">
        {signedIn && task.can_manage ? (
          <Button asChild variant="secondary">
            <Link href={`/tasks/${task.id}/candidates`}>Find the candidates</Link>
          </Button>
        ) : null}

        <Button asChild variant="secondary">
          <Link href={`/tasks/${task.id}/eligibility`}>Check my eligibility</Link>
        </Button>

        {signedIn && task.status === "open" ? (
          <Button asChild variant="primary">
            <Link href={`/tasks/${task.id}/apply`}>Apply for a slot</Link>
          </Button>
        ) : null}

        {signedIn && task.can_manage && task.status === "draft" && !editing ? (
          <Button type="button" variant="secondary" onClick={() => setEditing(true)}>
            Change the task
          </Button>
        ) : null}

        {signedIn && task.can_manage && task.status === "draft" ? (
          <Button
            ref={publishRef}
            type="button"
            variant="primary"
            disabled={command.kind === "saving" || editing}
            aria-busy={command.kind === "saving"}
            onClick={() => void publish()}
          >
            {command.kind === "saving" ? "Publishing the task" : "Publish the task"}
          </Button>
        ) : null}

        {signedIn && task.can_manage && task.status === "open" ? (
          <Button
            ref={cancelTriggerRef}
            type="button"
            variant="secondary"
            onClick={() => setConfirming(true)}
          >
            Cancel the task
          </Button>
        ) : null}
      </div>

      {confirming ? (
        <div
          role="group"
          aria-labelledby="task-cancel-heading"
          className="mt-4 rounded-lg border border-warning bg-surface p-4"
        >
          <h2 id="task-cancel-heading" className="text-large font-semibold">
            Cancel this task?
          </h2>
          <p className="mt-2 text-small text-muted-foreground">
            The admission closes and the unused slots are refunded. The work that already runs keeps
            its budget.
          </p>
          <div className="mt-4 flex flex-wrap items-center gap-3">
            <Button
              ref={confirmRef}
              type="button"
              variant="primary"
              disabled={command.kind === "saving"}
              onClick={() => void cancel()}
            >
              {command.kind === "saving" ? "Cancelling the task" : "Yes, cancel the task"}
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => {
                setConfirming(false);
                cancelTriggerRef.current?.focus();
              }}
            >
              Keep the task
            </Button>
          </div>
        </div>
      ) : null}

      {editing ? (
        <div className="mt-6">
          <h2 className="text-large font-semibold">Change the task</h2>
          <TaskForm
            variant="edit"
            projects={[]}
            task={task}
            csrfToken={csrfToken}
            onCommitted={(saved) => {
              setEditing(false);
              setState({ kind: "ready", task: saved });
              setAnnouncement("The task is saved.");
              publishRef.current?.focus();
            }}
          />
        </div>
      ) : null}

      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>

      {command.kind === "refused" || command.kind === "conflict" || command.kind === "forbidden" ? (
        <p className="mt-4 text-small text-warning">{command.message}</p>
      ) : null}

      {command.kind === "invalid" ? (
        <ul className="mt-4 flex flex-col gap-1 text-small text-danger">
          {Object.entries(command.fieldErrors).map(([field, messages]) => (
            <li key={field}>
              {messages.join(" ")}
            </li>
          ))}
        </ul>
      ) : null}

      {command.kind === "error" || command.kind === "unavailable" ? (
        <p className="mt-4 text-small text-danger">{command.message}</p>
      ) : null}
    </section>
  );

  async function publish() {
    setCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await publishTask(taskId, csrfToken, keyFor("publish"));

    setCommand(result);

    if (result.kind === "saved") {
      setState({ kind: "ready", task: result.task });
      setAnnouncement("The task is published and its budget is reserved.");
      publishRef.current?.focus();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("The task is not ready to be published yet.");
      return;
    }

    if (result.kind === "refused" || result.kind === "conflict" || result.kind === "forbidden") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to publish the task.");
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The task was not published.");
  }

  async function cancel() {
    setCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await cancelTask(taskId, csrfToken, keyFor("cancel"));

    setCommand(result);

    if (result.kind === "saved") {
      setState({ kind: "ready", task: result.task });
      setConfirming(false);
      setAnnouncement("The task is cancelled and the unused slots are refunded.");
      cancelTriggerRef.current?.focus();
      return;
    }

    if (result.kind === "refused" || result.kind === "conflict" || result.kind === "forbidden") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to cancel the task.");
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The task was not cancelled.");
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

/**
 * The key of the money command. It holds no secret, it only names the
 * command.
 */
function createIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  return `task-${Date.now()}-${Math.floor(Math.random() * 1000000)}`;
}
