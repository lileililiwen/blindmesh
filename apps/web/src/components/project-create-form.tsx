"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";

import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { Field, Select, TextArea, TextInput } from "@/components/ui/field";
import { createProject, type ProjectCommandState } from "@/lib/projects";
import type { ProjectDetailResponse } from "@blindmesh/api-client";

/** The order of the fields. The first field with an error takes the focus. */
const fieldOrder = ["name", "summary", "description", "repository_url", "visibility"] as const;

type Draft = {
  name: string;
  summary: string;
  description: string;
  repositoryUrl: string;
  visibility: string;
};

const emptyDraft: Draft = {
  name: "",
  summary: "",
  description: "",
  repositoryUrl: "",
  visibility: "private",
};

/**
 * The form that creates a project. The draft of the visitor is kept when the
 * server refuses the command, and the project is only announced after the
 * server committed it.
 */
export function ProjectCreateForm() {
  const { state: session, csrfToken } = useSession();
  const [draft, setDraft] = useState<Draft>(emptyDraft);
  const [command, setCommand] = useState<ProjectCommandState>({ kind: "idle" });
  const [announcement, setAnnouncement] = useState("");
  const [created, setCreated] = useState<ProjectDetailResponse | null>(null);
  const fieldRefs = useRef<Record<string, HTMLElement | null>>({});
  const triggerRef = useRef<HTMLButtonElement>(null);
  const idempotencyKey = useRef<string>("");

  useEffect(() => {
    idempotencyKey.current = createIdempotencyKey();
  }, []);

  if (session.kind === "loading") {
    return (
      <section aria-labelledby="project-new-heading" aria-busy="true" className="reading-width">
        <h1 id="project-new-heading" className="text-title font-semibold">
          Create a project
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Checking your session.</p>
      </section>
    );
  }

  if (session.kind !== "authenticated") {
    return (
      <section aria-labelledby="project-new-heading" className="reading-width">
        <h1 id="project-new-heading" className="text-title font-semibold">
          Create a project
        </h1>
        <p className="mt-3 text-muted-foreground">
          Sign in to create a project. The sign in keeps you on this page.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <a href="/sign-in?return=%2Fprojects%2Fnew">Sign in to continue</a>
          </Button>
        </p>
      </section>
    );
  }

  const fieldErrors = command.kind === "invalid" ? command.fieldErrors : {};
  const pending = command.kind === "saving";

  function focusFirstError(errors: Record<string, string[]>) {
    const field = fieldOrder.find((name) => (errors[name] ?? []).length > 0) ?? Object.keys(errors)[0];

    if (field) {
      fieldRefs.current[field]?.focus();
    }
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (pending || created) {
      return;
    }

    setCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await createProject(
      {
        name: draft.name,
        summary: draft.summary,
        description: draft.description,
        repository_url: draft.repositoryUrl,
        visibility: draft.visibility,
      },
      csrfToken,
      idempotencyKey.current,
    );

    setCommand(result);

    if (result.kind === "saved") {
      setCreated(result.project);
      setAnnouncement("The project is created.");
      triggerRef.current?.focus();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("Some fields need another value before the project can be created.");
      focusFirstError(result.fieldErrors);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to create the project.");
      return;
    }

    if (result.kind === "refused" || result.kind === "conflict" || result.kind === "forbidden") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The project was not created.");
  }

  return (
    <section aria-labelledby="project-new-heading" className="reading-width">
      <h1 id="project-new-heading" className="text-title font-semibold">
        Create a project
      </h1>
      <p className="mt-2 text-muted-foreground">
        A project starts as a draft. You publish it, you add the members of the team and you keep
        the visibility in your hands.
      </p>

      {created ? (
        <div className="mt-6 rounded-lg border border-success bg-surface p-4">
          <h2 className="text-large font-semibold">The project is ready</h2>
          <p className="mt-2 text-muted-foreground">
            &quot;{created.name}&quot; is a draft. Publish it and add the members of the team.
          </p>
          <p className="mt-4 flex flex-wrap gap-3">
            <Button asChild variant="primary">
              <Link href={`/projects/${created.id}`}>Open the project</Link>
            </Button>
            <Button asChild variant="secondary">
              <Link href={`/projects/${created.id}/settings`}>Open the project settings</Link>
            </Button>
          </p>
        </div>
      ) : null}

      <form className="mt-6 flex flex-col gap-5" onSubmit={submit} noValidate>
        <Field label="Name" error={fieldErrors.name} hint="At most 100 characters.">
          {({ id, describedBy, invalid }) => (
            <TextInput
              id={id}
              name="name"
              value={draft.name}
              maxLength={100}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.name = element;
              }}
              onChange={(event) => setDraft({ ...draft, name: event.target.value })}
            />
          )}
        </Field>

        <Field label="Summary" error={fieldErrors.summary} hint="At most 500 characters.">
          {({ id, describedBy, invalid }) => (
            <TextArea
              id={id}
              name="summary"
              rows={3}
              value={draft.summary}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.summary = element;
              }}
              onChange={(event) => setDraft({ ...draft, summary: event.target.value })}
            />
          )}
        </Field>

        <Field label="Description" error={fieldErrors.description} hint="At most 10000 characters.">
          {({ id, describedBy, invalid }) => (
            <TextArea
              id={id}
              name="description"
              rows={6}
              value={draft.description}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.description = element;
              }}
              onChange={(event) => setDraft({ ...draft, description: event.target.value })}
            />
          )}
        </Field>

        <Field
          label="Repository address"
          error={fieldErrors.repository_url}
          hint="An absolute HTTPS address, for example https://github.com/you/project."
        >
          {({ id, describedBy, invalid }) => (
            <TextInput
              id={id}
              name="repository_url"
              type="url"
              inputMode="url"
              value={draft.repositoryUrl}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.repository_url = element;
              }}
              onChange={(event) => setDraft({ ...draft, repositoryUrl: event.target.value })}
            />
          )}
        </Field>

        <Field
          label="Visibility"
          error={fieldErrors.visibility}
          hint="Private is visible for you and your team only. Matched waits for the matching of the marketplace."
        >
          {({ id, describedBy, invalid }) => (
            <Select
              id={id}
              name="visibility"
              value={draft.visibility}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              ref={(element) => {
                fieldRefs.current.visibility = element;
              }}
              onChange={(event) => setDraft({ ...draft, visibility: event.target.value })}
            >
              <option value="private">Private</option>
              <option value="matched">Matched</option>
              <option value="public">Public</option>
            </Select>
          )}
        </Field>

        <div className="flex flex-wrap items-center gap-3">
          <Button ref={triggerRef} type="submit" disabled={pending || Boolean(created)} aria-busy={pending}>
            {pending ? "Creating the project" : "Create the project"}
          </Button>
        </div>

        <p role="status" aria-live="polite" className="sr-only">
          {announcement}
        </p>

        {command.kind === "refused" || command.kind === "conflict" || command.kind === "forbidden" ? (
          <p className="text-small text-warning">{command.message}</p>
        ) : null}

        {command.kind === "error" || command.kind === "unavailable" ? (
          <p className="text-small text-danger">{command.message}</p>
        ) : null}
      </form>
    </section>
  );
}

/**
 * The key of the create command. The same key is used for a retry of the same
 * form, so a request that reached the server twice never creates two projects.
 */
function createIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  return `project-${Date.now()}-${Math.floor(Math.random() * 1000000)}`;
}
