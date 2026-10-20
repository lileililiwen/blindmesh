"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

import { TaskModeBadges, TaskStateBadge, TaskVisibilityBadge } from "@/components/task-badges";
import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { Field, Select, TextInput } from "@/components/ui/field";
import { credits } from "@/lib/credits";
import { loadTasks, type TaskListState } from "@/lib/tasks";
import type { TaskSummaryResponse } from "@blindmesh/api-client";

/**
 * The list of the tasks of the marketplace. The list holds the public tasks
 * and the own tasks of a signed in contributor; a task that may not be seen is
 * never part of the answer. The filter of the mode and of the hard
 * requirement narrows the list without leaving the page.
 */
export function TaskList({ projectId }: { projectId?: string }) {
  const { state: session } = useSession();
  const [state, setState] = useState<TaskListState>({ kind: "loading" });
  const [tasks, setTasks] = useState<TaskSummaryResponse[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);
  const [mode, setMode] = useState("");
  const [requirementKind, setRequirementKind] = useState("");
  const [requirementValue, setRequirementValue] = useState("");
  const [applied, setApplied] = useState<{ mode: string; requirement: string }>({
    mode: "",
    requirement: "",
  });

  const filters = useCallback(() => {
    return {
      mode: applied.mode || undefined,
      requirement: applied.requirement || undefined,
      projectId,
    };
  }, [applied, projectId]);

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadTasks(filters());

    if (next.kind === "ready") {
      setTasks(next.tasks);
      setCursor(next.nextCursor);
    }

    setState(next);
  }, [filters]);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      if (session.kind === "loading") {
        return;
      }

      const next = await loadTasks(filters());

      if (cancelled) {
        return;
      }

      if (next.kind === "ready") {
        setTasks(next.tasks);
        setCursor(next.nextCursor);
      }

      setState(next);
    })();

    return () => {
      cancelled = true;
    };
  }, [session.kind, filters]);

  async function loadMore() {
    if (!cursor || loadingMore) {
      return;
    }

    setLoadingMore(true);

    const next = await loadTasks(filters(), 20, cursor);

    if (next.kind === "ready") {
      setTasks((current) => [...current, ...next.tasks]);
      setCursor(next.nextCursor);
    } else {
      setState(next);
    }

    setLoadingMore(false);
  }

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="tasks-heading" aria-busy="true" className="reading-width">
        <h1 id="tasks-heading" className="text-title font-semibold">
          Tasks
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the tasks.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-20 w-full rounded bg-muted" />
          <div className="h-20 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="tasks-heading" className="reading-width">
        <h1 id="tasks-heading" className="text-title font-semibold">
          Tasks
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

  const signedIn = session.kind === "authenticated";
  const filtered = applied.mode !== "" || applied.requirement !== "";

  return (
    <section aria-labelledby="tasks-heading" className="reading-width">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 id="tasks-heading" className="text-title font-semibold">
            Tasks
          </h1>
          <p className="mt-2 text-muted-foreground">
            {signedIn
              ? "The public tasks of the marketplace and the tasks you own."
              : "The public tasks of the marketplace. Sign in to publish a task of your own."}
          </p>
        </div>

        {signedIn ? (
          <Button asChild variant="primary">
            <Link href="/tasks/new">Publish a task</Link>
          </Button>
        ) : null}
      </div>

      <form
        className="mt-6 flex flex-col gap-4 rounded-lg border border-border bg-surface p-4"
        aria-labelledby="task-filter-heading"
        onSubmit={(event) => {
          event.preventDefault();
          setApplied({
            mode,
            requirement: requirementKind && requirementValue ? `${requirementKind}:${requirementValue}` : "",
          });
        }}
      >
        <h2 id="task-filter-heading" className="text-large font-semibold">
          Filter the tasks
        </h2>

        <div className="grid gap-4 sm:grid-cols-3">
          <Field label="Mode" hint="A task has to carry the mode you choose.">
            {({ id, describedBy }) => (
              <Select
                id={id}
                name="mode"
                value={mode}
                aria-describedby={describedBy}
                onChange={(event) => setMode(event.target.value)}
              >
                <option value="">Every mode</option>
                <option value="explore">Explore</option>
                <option value="verify">Verify</option>
                <option value="access">Access</option>
                <option value="review">Review</option>
              </Select>
            )}
          </Field>

          <Field label="Requirement kind" hint="The kind of the capability the task needs.">
            {({ id, describedBy }) => (
              <Select
                id={id}
                name="requirement_kind"
                value={requirementKind}
                aria-describedby={describedBy}
                onChange={(event) => setRequirementKind(event.target.value)}
              >
                <option value="">Every kind</option>
                <option value="region">Region</option>
                <option value="device">Device</option>
                <option value="role">Role</option>
              </Select>
            )}
          </Field>

          <Field label="Requirement value" hint="The value the declaration has to match.">
            {({ id, describedBy }) => (
              <TextInput
                id={id}
                name="requirement_value"
                value={requirementValue}
                aria-describedby={describedBy}
                onChange={(event) => setRequirementValue(event.target.value)}
              />
            )}
          </Field>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <Button type="submit" variant="primary">
            Apply the filter
          </Button>

          {filtered ? (
            <Button
              type="button"
              variant="secondary"
              onClick={() => {
                setMode("");
                setRequirementKind("");
                setRequirementValue("");
                setApplied({ mode: "", requirement: "" });
              }}
            >
              Clear the filter
            </Button>
          ) : null}
        </div>
      </form>

      {tasks.length === 0 ? (
        <div className="mt-6 rounded-lg border border-border bg-surface p-6">
          <h2 className="text-large font-semibold">No task is visible yet</h2>
          <p className="mt-2 text-muted-foreground">
            {filtered
              ? "No task matches this filter. Clear the filter to see every task."
              : signedIn
                ? "Publish the first task and describe the work you want tested."
                : "No task is public right now. Sign in to publish the first one."}
          </p>
          <p className="mt-4 flex flex-wrap gap-3">
            {signedIn ? (
              <Button asChild variant="primary">
                <Link href="/tasks/new">Publish a task</Link>
              </Button>
            ) : (
              <Button asChild variant="primary">
                <Link href="/sign-in?return=%2Ftasks">Sign in</Link>
              </Button>
            )}
          </p>
        </div>
      ) : (
        <>
          <ul className="mt-6 flex flex-col gap-4">
            {tasks.map((task) => (
              <li key={task.id}>
                <article className="rounded-lg border border-border bg-surface p-4 sm:p-5">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="text-large font-semibold">
                      <Link href={`/tasks/${task.id}`} className="underline underline-offset-4">
                        {task.title}
                      </Link>
                    </h2>
                    <TaskStateBadge status={task.status} />
                    <TaskVisibilityBadge visibility={task.visibility} />
                    <TaskModeBadges modes={task.modes} />
                  </div>

                  <dl className="mt-3 grid grid-cols-2 gap-x-4 gap-y-2 text-small sm:grid-cols-4">
                    <div>
                      <dt className="text-muted-foreground">Budget</dt>
                      <dd>{credits(task.maximum_budget)} credits</dd>
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
                      <dt className="text-muted-foreground">Project</dt>
                      <dd>{task.project_name || "an unknown project"}</dd>
                    </div>
                  </dl>

                  {task.requirements.length > 0 ? (
                    <p className="mt-3 text-small text-muted-foreground">
                      Needs {task.requirements.map((item) => `${item.kind} ${item.value}`).join(", ")}.
                    </p>
                  ) : null}
                </article>
              </li>
            ))}
          </ul>

          {cursor ? (
            <p className="mt-5">
              <Button type="button" variant="secondary" disabled={loadingMore} onClick={() => void loadMore()}>
                {loadingMore ? "Loading more tasks" : "Load more tasks"}
              </Button>
            </p>
          ) : null}
        </>
      )}
    </section>
  );
}
