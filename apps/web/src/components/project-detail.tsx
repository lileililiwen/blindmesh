"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

import { StatusBadge, VisibilityBadge } from "@/components/project-badges";
import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { loadProject, type ProjectDetailState } from "@/lib/projects";
import type { ProjectDetailResponse } from "@blindmesh/api-client";

/**
 * The page of a project. It shows the committed state, the released versions
 * and the history of the lifecycle. A project that may not be seen is not
 * found, so the page never reveals a title that the visitor may not read.
 */
export function ProjectDetail({ id }: { id: string }) {
  const { state: session } = useSession();
  const [state, setState] = useState<ProjectDetailState>({ kind: "loading" });
  const [project, setProject] = useState<ProjectDetailResponse | null>(null);

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadProject(id);

    if (next.kind === "ready") {
      setProject(next.project);
    }

    setState(next);
  }, [id]);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      if (session.kind === "loading") {
        return;
      }

      const next = await loadProject(id);

      if (cancelled) {
        return;
      }

      if (next.kind === "ready") {
        setProject(next.project);
      }

      setState(next);
    })();

    return () => {
      cancelled = true;
    };
  }, [id, session.kind]);

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="project-heading" aria-busy="true" className="reading-width">
        <h1 id="project-heading" className="text-title font-semibold">
          Project
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the project.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-8 w-2/3 rounded bg-muted" />
          <div className="h-24 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (state.kind === "not-found") {
    return (
      <section aria-labelledby="project-heading" className="reading-width">
        <h1 id="project-heading" className="text-title font-semibold">
          Project
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          This project does not exist or is not visible to you.
        </p>
        <p className="mt-4">
          <Button asChild variant="secondary">
            <Link href="/projects">Back to the projects</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="project-heading" className="reading-width">
        <h1 id="project-heading" className="text-title font-semibold">
          Project
        </h1>
        <p className="mt-3 text-muted-foreground">
          Sign in to read this project. The sign in keeps you on this page.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <a href={`/sign-in?return=${encodeURIComponent(`/projects/${id}`)}`}>Sign in to continue</a>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable" || state.kind === "forbidden") {
    return (
      <section aria-labelledby="project-heading" className="reading-width">
        <h1 id="project-heading" className="text-title font-semibold">
          Project
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

  if (!project) {
    return null;
  }

  const user = session.kind === "authenticated" ? session.user : null;
  const isOwner = Boolean(user && project.owner_username === user.username);
  const member = user
    ? project.members.find((item) => item.user_id === user.id)
    : undefined;
  const mayManage = isOwner || member?.role === "editor";

  return (
    <section aria-labelledby="project-heading" className="reading-width">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 id="project-heading" className="text-title font-semibold">
            {project.name}
          </h1>
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <StatusBadge status={project.status} />
            <VisibilityBadge visibility={project.visibility} />
            <span className="text-small text-muted-foreground">
              Owned by {project.owner_username || "an unknown contributor"}.
            </span>
          </div>
        </div>

        {mayManage ? (
          <Button asChild variant="secondary">
            <Link href={`/projects/${project.id}/settings`}>Manage the project</Link>
          </Button>
        ) : null}

        <Button asChild variant="secondary">
          <Link href={`/projects/${project.id}/tasks`}>The tasks of the project</Link>
        </Button>

        {mayManage ? (
          <Button asChild variant="secondary">
            <Link href={`/projects/${project.id}/reviews`}>The reviews of the project</Link>
          </Button>
        ) : null}
      </div>

      {project.summary ? (
        <p className="mt-4 text-body">{project.summary}</p>
      ) : null}

      {project.description ? (
        <p className="mt-3 whitespace-pre-line text-muted-foreground">{project.description}</p>
      ) : null}

      <p className="mt-4">
        <a
          href={project.repository_url}
          target="_blank"
          rel="noreferrer noopener"
          className="underline underline-offset-4"
        >
          {project.repository_url}
        </a>{" "}
        <span className="text-small text-muted-foreground">(opens in a new tab)</span>
      </p>

      {project.members.length > 0 ? (
        <section aria-labelledby="project-members-heading" className="mt-8">
          <h2 id="project-members-heading" className="text-large font-semibold">
            Team
          </h2>
          <ul className="mt-3 flex flex-col gap-2">
            {project.members.map((item) => (
              <li key={item.user_id} className="text-small">
                <span className="font-medium">{item.username}</span>{" "}
                <span className="text-muted-foreground">is a {item.role}.</span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <section aria-labelledby="project-versions-heading" className="mt-8">
        <h2 id="project-versions-heading" className="text-large font-semibold">
          Versions
        </h2>

        {project.versions.length === 0 ? (
          <p className="mt-2 text-muted-foreground">
            No version was released yet. The owner or an editor records the released versions.
          </p>
        ) : (
          <ul className="mt-3 flex flex-col gap-2">
            {project.versions.map((version) => (
              <li key={version.id} className="text-small">
                <span className="font-medium">{version.label}</span>
                {version.commit_reference ? (
                  <span className="text-muted-foreground"> ({version.commit_reference})</span>
                ) : null}
                <span className="text-muted-foreground"> recorded by {version.created_by}.</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby="project-history-heading" className="mt-8">
        <h2 id="project-history-heading" className="text-large font-semibold">
          History
        </h2>

        {project.status_history.length === 0 ? (
          <p className="mt-2 text-muted-foreground">
            The project is still a draft. Every change of the state is listed here.
          </p>
        ) : (
          <ol className="mt-3 flex flex-col gap-2">
            {project.status_history.map((change, index) => (
              <li key={`${change.changed_at}-${index}`} className="text-small">
                <span className="font-medium">{change.changed_by}</span>{" "}
                <span className="text-muted-foreground">
                  moved the project from {change.from_status} to {change.to_status}.
                </span>
              </li>
            ))}
          </ol>
        )}
      </section>
    </section>
  );
}
