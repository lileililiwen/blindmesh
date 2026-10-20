"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

import { StatusBadge, VisibilityBadge } from "@/components/project-badges";
import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { loadProjects, type ProjectListState } from "@/lib/projects";
import type { ProjectSummaryResponse } from "@blindmesh/api-client";

/**
 * The list of the projects the visitor may see. The list holds the public
 * projects and the own projects of a signed in contributor; a project that may
 * not be seen is never part of the answer.
 */
export function ProjectList() {
  const { state: session } = useSession();
  const [state, setState] = useState<ProjectListState>({ kind: "loading" });
  const [projects, setProjects] = useState<ProjectSummaryResponse[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadProjects();

    if (next.kind === "ready") {
      setProjects(next.projects);
      setCursor(next.nextCursor);
    }

    setState(next);
  }, []);

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
        setCursor(next.nextCursor);
      }

      setState(next);
    })();

    return () => {
      cancelled = true;
    };
  }, [session.kind]);

  async function loadMore() {
    if (!cursor || loadingMore) {
      return;
    }

    setLoadingMore(true);

    const next = await loadProjects(20, cursor);

    if (next.kind === "ready") {
      setProjects((current) => [...current, ...next.projects]);
      setCursor(next.nextCursor);
    } else {
      setState(next);
    }

    setLoadingMore(false);
  }

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="projects-heading" aria-busy="true" className="reading-width">
        <h1 id="projects-heading" className="text-title font-semibold">
          Projects
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the projects.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-20 w-full rounded bg-muted" />
          <div className="h-20 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="projects-heading" className="reading-width">
        <h1 id="projects-heading" className="text-title font-semibold">
          Projects
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

  return (
    <section aria-labelledby="projects-heading" className="reading-width">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 id="projects-heading" className="text-title font-semibold">
            Projects
          </h1>
          <p className="mt-2 text-muted-foreground">
            {signedIn
              ? "The public projects and the projects you own or belong to."
              : "The public projects of the marketplace. Sign in to see your own projects."}
          </p>
        </div>

        {signedIn ? (
          <Button asChild variant="primary">
            <Link href="/projects/new">Create a project</Link>
          </Button>
        ) : null}
      </div>

      {projects.length === 0 ? (
        <div className="mt-6 rounded-lg border border-border bg-surface p-6">
          <h2 className="text-large font-semibold">No project is visible yet</h2>
          <p className="mt-2 text-muted-foreground">
            {signedIn
              ? "Create the first project and describe the software you want tested."
              : "No project is public right now. Sign in to create the first one."}
          </p>
          <p className="mt-4 flex flex-wrap gap-3">
            {signedIn ? (
              <Button asChild variant="primary">
                <Link href="/projects/new">Create a project</Link>
              </Button>
            ) : (
              <Button asChild variant="primary">
                <Link href="/sign-in?return=%2Fprojects">Sign in</Link>
              </Button>
            )}
          </p>
        </div>
      ) : (
        <>
          <ul className="mt-6 flex flex-col gap-4">
            {projects.map((project) => (
              <li key={project.id}>
                <article className="rounded-lg border border-border bg-surface p-4 sm:p-5">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="text-large font-semibold">
                      <Link href={`/projects/${project.id}`} className="underline underline-offset-4">
                        {project.name}
                      </Link>
                    </h2>
                    <StatusBadge status={project.status} />
                    <VisibilityBadge visibility={project.visibility} />
                  </div>

                  {project.summary ? (
                    <p className="mt-2 text-muted-foreground">{project.summary}</p>
                  ) : null}

                  <p className="mt-2 text-small text-muted-foreground">
                    Owned by {project.owner_username || "an unknown contributor"}.
                  </p>
                </article>
              </li>
            ))}
          </ul>

          {cursor ? (
            <p className="mt-5">
              <Button type="button" variant="secondary" disabled={loadingMore} onClick={() => void loadMore()}>
                {loadingMore ? "Loading more projects" : "Load more projects"}
              </Button>
            </p>
          ) : null}
        </>
      )}
    </section>
  );
}
