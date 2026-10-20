"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import { TaskForm } from "@/components/task-form";
import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { loadProjects, type ProjectListState } from "@/lib/projects";
import type { ProjectSummaryResponse, TaskDetailResponse } from "@blindmesh/api-client";

/**
 * The form that writes a task. The task belongs to a project, so the projects
 * of the contributor are loaded first. The draft of the visitor is kept when
 * the server refuses the command.
 */
export function TaskCreateForm() {
  const { state: session, csrfToken } = useSession();
  const [projects, setProjects] = useState<ProjectSummaryResponse[]>([]);
  const [projectsState, setProjectsState] = useState<ProjectListState>({ kind: "loading" });
  const [created, setCreated] = useState<TaskDetailResponse | null>(null);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      if (session.kind === "loading") {
        return;
      }

      const next = await loadProjects();

      if (cancelled) {
        return;
      }

      if (next.kind === "ready") {
        setProjects(next.projects);
      }

      setProjectsState(next);
    })();

    return () => {
      cancelled = true;
    };
  }, [session.kind]);

  if (session.kind === "loading") {
    return (
      <section aria-labelledby="task-new-heading" aria-busy="true" className="reading-width">
        <h1 id="task-new-heading" className="text-title font-semibold">
          Write a task
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Checking your session.</p>
      </section>
    );
  }

  if (session.kind !== "authenticated") {
    return (
      <section aria-labelledby="task-new-heading" className="reading-width">
        <h1 id="task-new-heading" className="text-title font-semibold">
          Write a task
        </h1>
        <p className="mt-3 text-muted-foreground">
          Sign in to write a task. The sign in keeps you on this page.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <a href="/sign-in?return=%2Ftasks%2Fnew">Sign in to continue</a>
          </Button>
        </p>
      </section>
    );
  }

  return (
    <section aria-labelledby="task-new-heading" className="reading-width">
      <h1 id="task-new-heading" className="text-title font-semibold">
        Write a task
      </h1>
      <p className="mt-2 text-muted-foreground">
        A task starts as a draft. It names the modes, the acceptance rules, the deadline and the
        slots. The whole budget is reserved when you publish it.
      </p>

      {created ? (
        <div className="mt-6 rounded-lg border border-success bg-surface p-4">
          <h2 className="text-large font-semibold">The task is written</h2>
          <p className="mt-2 text-muted-foreground">
            &quot;{created.title}&quot; is a draft. Publish it to reserve its budget and to show it
            in the marketplace.
          </p>
          <p className="mt-4 flex flex-wrap gap-3">
            <Button asChild variant="primary">
              <Link href={`/tasks/${created.id}`}>Open the task</Link>
            </Button>
            <Button asChild variant="secondary">
              <Link href="/tasks">Back to the tasks</Link>
            </Button>
          </p>
        </div>
      ) : null}

      {projectsState.kind === "loading" ? (
        <p className="mt-6 text-small text-muted-foreground" aria-busy="true">
          Loading your projects.
        </p>
      ) : null}

      {projectsState.kind === "error" || projectsState.kind === "unavailable" ? (
        <p className="mt-6 text-small text-danger" role="status">
          {projectsState.message}
        </p>
      ) : null}

      {projectsState.kind === "ready" && projects.length === 0 && !created ? (
        <div className="mt-6 rounded-lg border border-border bg-surface p-6">
          <h2 className="text-large font-semibold">No project is visible yet</h2>
          <p className="mt-2 text-muted-foreground">
            A task belongs to a project. Create a project first and describe the software you want
            tested.
          </p>
          <p className="mt-4">
            <Button asChild variant="primary">
              <Link href="/projects/new">Create a project</Link>
            </Button>
          </p>
        </div>
      ) : null}

      {projects.length > 0 && !created ? (
        <div className="mt-6">
          <TaskForm
            variant="create"
            projects={projects}
            csrfToken={csrfToken}
            onCommitted={(task) => setCreated(task)}
          />
        </div>
      ) : null}
    </section>
  );
}
