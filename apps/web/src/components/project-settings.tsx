"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";

import { StatusBadge, VisibilityBadge } from "@/components/project-badges";
import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, Select, TextArea, TextInput } from "@/components/ui/field";
import {
  addProjectMember,
  addProjectVersion,
  loadProject,
  removeProjectMember,
  transitionProject,
  updateProject,
  type ProjectCommandState,
  type ProjectDetailState,
} from "@/lib/projects";
import type { ProjectDetailResponse } from "@blindmesh/api-client";

/** The order of the fields of the details form. */
const detailFieldOrder = ["name", "summary", "description", "repository_url"] as const;

type DetailsDraft = {
  name: string;
  summary: string;
  description: string;
  repositoryUrl: string;
};

type MemberDraft = {
  username: string;
  role: string;
};

type VersionDraft = {
  label: string;
  commitReference: string;
};

/**
 * The settings of a project. The owner manages the members, the visibility,
 * the pause and the archive; the owner and the editors change the content and
 * record the versions. Every command carries the version that was read, so a
 * lost change is refused instead of overwritten.
 */
export function ProjectSettings({ id }: { id: string }) {
  const { state: session, csrfToken } = useSession();
  const [state, setState] = useState<ProjectDetailState>({ kind: "loading" });
  const [project, setProject] = useState<ProjectDetailResponse | null>(null);
  const [announcement, setAnnouncement] = useState("");

  const [details, setDetails] = useState<DetailsDraft | null>(null);
  const [detailsCommand, setDetailsCommand] = useState<ProjectCommandState>({ kind: "idle" });

  const [visibility, setVisibility] = useState<string>("private");
  const [visibilityCommand, setVisibilityCommand] = useState<ProjectCommandState>({ kind: "idle" });

  const [lifecycleCommand, setLifecycleCommand] = useState<ProjectCommandState>({ kind: "idle" });

  const [member, setMember] = useState<MemberDraft>({ username: "", role: "viewer" });
  const [memberCommand, setMemberCommand] = useState<ProjectCommandState>({ kind: "idle" });
  const [removing, setRemoving] = useState<string | null>(null);
  const [archiving, setArchiving] = useState(false);

  const [versionDraft, setVersionDraft] = useState<VersionDraft>({ label: "", commitReference: "" });
  const [versionCommand, setVersionCommand] = useState<ProjectCommandState>({ kind: "idle" });

  const detailRefs = useRef<Record<string, HTMLElement | null>>({});
  const memberRef = useRef<HTMLInputElement>(null);
  const versionRef = useRef<HTMLInputElement>(null);
  const detailsTrigger = useRef<HTMLButtonElement>(null);
  const visibilityTrigger = useRef<HTMLButtonElement>(null);
  const lifecycleTrigger = useRef<HTMLButtonElement>(null);
  const memberTrigger = useRef<HTMLButtonElement>(null);
  const versionTrigger = useRef<HTMLButtonElement>(null);

  /**
   * The committed state of the project. The draft of the contributor is only
   * replaced when the content itself changed, so a command that touches the
   * team or the lifecycle never costs a typed summary.
   */
  const applyProject = useCallback((next: ProjectDetailResponse, replaceDraft = false) => {
    setProject(next);
    setVisibility(next.visibility);

    if (replaceDraft) {
      setDetails({
        name: next.name,
        summary: next.summary,
        description: next.description,
        repositoryUrl: next.repository_url,
      });
    }
  }, []);

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadProject(id);

    if (next.kind === "ready") {
      applyProject(next.project, true);
    }

    setState(next);
  }, [id, applyProject]);

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
        applyProject(next.project, true);
      }

      setState(next);
    })();

    return () => {
      cancelled = true;
    };
  }, [id, session.kind, applyProject]);

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="project-settings-heading" aria-busy="true" className="reading-width">
        <h1 id="project-settings-heading" className="text-title font-semibold">
          Project settings
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the project.</p>
      </section>
    );
  }

  if (state.kind === "not-found") {
    return (
      <section aria-labelledby="project-settings-heading" className="reading-width">
        <h1 id="project-settings-heading" className="text-title font-semibold">
          Project settings
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
      <section aria-labelledby="project-settings-heading" className="reading-width">
        <h1 id="project-settings-heading" className="text-title font-semibold">
          Project settings
        </h1>
        <p className="mt-3 text-muted-foreground">
          Sign in to manage this project. The sign in keeps you on this page.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <a href={`/sign-in?return=${encodeURIComponent(`/projects/${id}/settings`)}`}>
              Sign in to continue
            </a>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable" || state.kind === "forbidden") {
    return (
      <section aria-labelledby="project-settings-heading" className="reading-width">
        <h1 id="project-settings-heading" className="text-title font-semibold">
          Project settings
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

  if (!project || !details) {
    return null;
  }

  const user = session.kind === "authenticated" ? session.user : null;
  const isOwner = Boolean(user && project.owner_username === user.username);
  const memberOfProject = user
    ? project.members.find((item) => item.user_id === user.id)
    : undefined;
  const isEditor = memberOfProject?.role === "editor";
  const isViewer = memberOfProject?.role === "viewer";
  const mayEdit = isOwner || isEditor;
  const projectVersion = Number(project.version);

  function focusFirstDetailError(errors: Record<string, string[]>) {
    const field =
      detailFieldOrder.find((name) => (errors[name] ?? []).length > 0) ?? Object.keys(errors)[0];

    if (field) {
      detailRefs.current[field]?.focus();
    }
  }

  function report(result: ProjectCommandState, message: string): void {
    if (result.kind === "saved") {
      applyProject(result.project);
      setAnnouncement(message);
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("Some fields need another value before the project can be saved.");
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to manage the project.");
      return;
    }

    if (result.kind === "not-found") {
      setAnnouncement("This project does not exist or is not visible to you any more.");
      return;
    }

    if (result.kind === "conflict" || result.kind === "refused" || result.kind === "forbidden") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unavailable" || result.kind === "error") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The project was not changed.");
  }

  async function saveDetails(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!details || detailsCommand.kind === "saving") {
      return;
    }

    setDetailsCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await updateProject(
      project!.id,
      {
        expected_version: projectVersion,
        name: details.name,
        summary: details.summary,
        description: details.description,
        repository_url: details.repositoryUrl,
      },
      csrfToken,
    );

    setDetailsCommand(result);

    if (result.kind === "saved") {
      applyProject(result.project, true);
      setAnnouncement("The project is saved.");
      detailsTrigger.current?.focus();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("Some fields need another value before the project can be saved.");
      focusFirstDetailError(result.fieldErrors);
      return;
    }

    report(result, "The project is saved.");
  }

  async function saveVisibility(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (visibilityCommand.kind === "saving") {
      return;
    }

    setVisibilityCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await updateProject(
      project!.id,
      { expected_version: projectVersion, visibility },
      csrfToken,
    );

    setVisibilityCommand(result);

    if (result.kind === "saved") {
      applyProject(result.project);
      setAnnouncement("The visibility is saved.");
      visibilityTrigger.current?.focus();
      return;
    }

    report(result, "The visibility is saved.");
  }

  async function applyTransition(target: string, trigger: React.RefObject<HTMLButtonElement | null>) {
    if (lifecycleCommand.kind === "saving") {
      return;
    }

    setLifecycleCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await transitionProject(
      project!.id,
      { expected_version: projectVersion, to: target },
      csrfToken,
    );

    setLifecycleCommand(result);

    if (result.kind === "saved") {
      applyProject(result.project);
      setAnnouncement(`The project is ${target} now.`);
      trigger.current?.focus();
      return;
    }

    report(result, `The project is ${target} now.`);
  }

  async function addMember(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (memberCommand.kind === "saving") {
      return;
    }

    setMemberCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await addProjectMember(
      project!.id,
      member.username.trim(),
      { role: member.role },
      csrfToken,
    );

    setMemberCommand(result);

    if (result.kind === "saved") {
      applyProject(result.project);
      setMember({ username: "", role: member.role });
      setAnnouncement(`${member.username.trim()} is a member of the project now.`);
      memberTrigger.current?.focus();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("The member could not be added. Check the name of the contributor.");
      memberRef.current?.focus();
      return;
    }

    report(result, "The member was added.");
  }

  async function removeMember(username: string) {
    if (memberCommand.kind === "saving") {
      return;
    }

    setMemberCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await removeProjectMember(project!.id, username, csrfToken);

    setRemoving(null);
    setMemberCommand(result);

    if (result.kind === "idle" || result.kind === "saved") {
      const refreshed = await loadProject(project!.id);

      if (refreshed.kind === "ready") {
        applyProject(refreshed.project);
      }

      setAnnouncement(`${username} is not a member of the project any more.`);
      memberTrigger.current?.focus();
      return;
    }

    report(result, "The member was not removed.");
  }

  async function addVersion(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (versionCommand.kind === "saving") {
      return;
    }

    setVersionCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await addProjectVersion(
      project!.id,
      {
        label: versionDraft.label,
        commit_reference: versionDraft.commitReference,
      },
      csrfToken,
    );

    setVersionCommand(result);

    if (result.kind === "saved") {
      applyProject(result.project);
      setVersionDraft({ label: "", commitReference: "" });
      setAnnouncement(`The version ${versionDraft.label} is recorded.`);
      versionTrigger.current?.focus();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("The version could not be recorded. Check the label.");
      versionRef.current?.focus();
      return;
    }

    report(result, "The version is recorded.");
  }

  const detailErrors = detailsCommand.kind === "invalid" ? detailsCommand.fieldErrors : {};
  const visibilityErrors = visibilityCommand.kind === "invalid" ? visibilityCommand.fieldErrors : {};
  const memberErrors = memberCommand.kind === "invalid" ? memberCommand.fieldErrors : {};
  const versionErrors = versionCommand.kind === "invalid" ? versionCommand.fieldErrors : {};

  return (
    <section aria-labelledby="project-settings-heading" className="reading-width">
      <h1 id="project-settings-heading" className="text-title font-semibold">
        Project settings
      </h1>

      <div className="mt-2 flex flex-wrap items-center gap-2">
        <span className="text-large font-semibold">{project.name}</span>
        <StatusBadge status={project.status} />
        <VisibilityBadge visibility={project.visibility} />
      </div>

      <p className="mt-2 text-muted-foreground">
        Version {projectVersion}. Every command carries the version that was read, so a change of another
        device is never overwritten.
      </p>

      {!mayEdit ? (
        <p role="status" className="mt-4 rounded-md border border-border bg-surface p-3 text-small">
          {isViewer
            ? "You are a viewer of this project. You can read it and apply for its work, but you cannot change it."
            : "Only the owner and the editors of this project can change it."}
        </p>
      ) : null}

      {mayEdit ? (
        <div className="mt-6 flex flex-col gap-6">
          <Card>
            <CardHeader>
              <CardTitle>Details</CardTitle>
              <CardDescription>The name, the summary, the description and the repository.</CardDescription>
            </CardHeader>
            <CardContent>
              <form className="flex flex-col gap-5" onSubmit={saveDetails} noValidate>
                <Field label="Name" error={detailErrors.name} hint="At most 100 characters.">
                  {({ id, describedBy, invalid }) => (
                    <TextInput
                      id={id}
                      name="name"
                      value={details.name}
                      maxLength={100}
                      aria-describedby={describedBy}
                      aria-invalid={invalid}
                      ref={(element) => {
                        detailRefs.current.name = element;
                      }}
                      onChange={(event) => setDetails({ ...details, name: event.target.value })}
                    />
                  )}
                </Field>

                <Field label="Summary" error={detailErrors.summary} hint="At most 500 characters.">
                  {({ id, describedBy, invalid }) => (
                    <TextArea
                      id={id}
                      name="summary"
                      rows={3}
                      value={details.summary}
                      aria-describedby={describedBy}
                      aria-invalid={invalid}
                      ref={(element) => {
                        detailRefs.current.summary = element;
                      }}
                      onChange={(event) => setDetails({ ...details, summary: event.target.value })}
                    />
                  )}
                </Field>

                <Field label="Description" error={detailErrors.description} hint="At most 10000 characters.">
                  {({ id, describedBy, invalid }) => (
                    <TextArea
                      id={id}
                      name="description"
                      rows={6}
                      value={details.description}
                      aria-describedby={describedBy}
                      aria-invalid={invalid}
                      ref={(element) => {
                        detailRefs.current.description = element;
                      }}
                      onChange={(event) => setDetails({ ...details, description: event.target.value })}
                    />
                  )}
                </Field>

                <Field
                  label="Repository address"
                  error={detailErrors.repository_url}
                  hint="An absolute HTTPS address."
                >
                  {({ id, describedBy, invalid }) => (
                    <TextInput
                      id={id}
                      name="repository_url"
                      type="url"
                      inputMode="url"
                      value={details.repositoryUrl}
                      aria-describedby={describedBy}
                      aria-invalid={invalid}
                      ref={(element) => {
                        detailRefs.current.repository_url = element;
                      }}
                      onChange={(event) =>
                        setDetails({ ...details, repositoryUrl: event.target.value })
                      }
                    />
                  )}
                </Field>

                <div className="flex flex-wrap items-center gap-3">
                  <Button
                    ref={detailsTrigger}
                    type="submit"
                    disabled={detailsCommand.kind === "saving"}
                    aria-busy={detailsCommand.kind === "saving"}
                  >
                    {detailsCommand.kind === "saving" ? "Saving the details" : "Save the details"}
                  </Button>

                  {detailsCommand.kind === "conflict" ? (
                    <Button type="button" variant="secondary" onClick={() => void load()}>
                      Load the current project
                    </Button>
                  ) : null}
                </div>

                {detailsCommand.kind === "conflict" ? (
                  <p className="text-small text-warning">{detailsCommand.message}</p>
                ) : null}
              </form>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Visibility</CardTitle>
              <CardDescription>Who may read the project.</CardDescription>
            </CardHeader>
            <CardContent>
              {isOwner ? (
                <form className="flex flex-col gap-5" onSubmit={saveVisibility} noValidate>
                  <Field
                    label="Visibility"
                    error={visibilityErrors.visibility}
                    hint="Private is visible for the team only, public for everybody, matched for the candidates of the marketplace."
                  >
                    {({ id, describedBy, invalid }) => (
                      <Select
                        id={id}
                        name="visibility"
                        value={visibility}
                        aria-describedby={describedBy}
                        aria-invalid={invalid}
                        onChange={(event) => setVisibility(event.target.value)}
                      >
                        <option value="private">Private</option>
                        <option value="matched">Matched</option>
                        <option value="public">Public</option>
                      </Select>
                    )}
                  </Field>

                  <div className="flex flex-wrap items-center gap-3">
                    <Button
                      ref={visibilityTrigger}
                      type="submit"
                      disabled={visibilityCommand.kind === "saving"}
                      aria-busy={visibilityCommand.kind === "saving"}
                    >
                      {visibilityCommand.kind === "saving" ? "Saving the visibility" : "Save the visibility"}
                    </Button>

                    {visibilityCommand.kind === "conflict" ? (
                      <Button type="button" variant="secondary" onClick={() => void load()}>
                        Load the current project
                      </Button>
                    ) : null}
                  </div>

                  {visibilityCommand.kind === "conflict" ? (
                    <p className="text-small text-warning">{visibilityCommand.message}</p>
                  ) : null}
                </form>
              ) : (
                <p className="text-small text-muted-foreground">
                  Only the owner of the project changes the visibility.
                </p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Lifecycle</CardTitle>
              <CardDescription>
                The state of the project. An archived project keeps its activity and accepts no new
                work.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="flex flex-wrap items-center gap-3">
                {project.status === "draft" ? (
                  <Button
                    ref={lifecycleTrigger}
                    type="button"
                    disabled={lifecycleCommand.kind === "saving"}
                    onClick={() => void applyTransition("active", lifecycleTrigger)}
                  >
                    Publish the project
                  </Button>
                ) : null}

                {project.status === "active" && isOwner ? (
                  <Button
                    ref={lifecycleTrigger}
                    type="button"
                    variant="secondary"
                    disabled={lifecycleCommand.kind === "saving"}
                    onClick={() => void applyTransition("paused", lifecycleTrigger)}
                  >
                    Pause the project
                  </Button>
                ) : null}

                {project.status === "paused" ? (
                  <Button
                    ref={lifecycleTrigger}
                    type="button"
                    disabled={lifecycleCommand.kind === "saving"}
                    onClick={() => void applyTransition("active", lifecycleTrigger)}
                  >
                    Publish the project again
                  </Button>
                ) : null}

                {project.status !== "archived" && isOwner ? (
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={lifecycleCommand.kind === "saving"}
                    onClick={() => setArchiving(true)}
                  >
                    Archive the project
                  </Button>
                ) : null}

                {project.status === "archived" ? (
                  <p className="text-small text-muted-foreground">
                    The project is archived. It keeps its activity and accepts no new work.
                  </p>
                ) : null}
              </div>

              {archiving ? (
                <div className="mt-4 rounded-md border border-warning bg-surface p-3">
                  <p className="text-small">
                    The project is archived. It keeps its versions and its history, but it accepts no
                    new work and it cannot be published again.
                  </p>
                  <p className="mt-3 flex flex-wrap gap-3">
                    <Button
                      type="button"
                      disabled={lifecycleCommand.kind === "saving"}
                      onClick={() => {
                        setArchiving(false);
                        void applyTransition("archived", lifecycleTrigger);
                      }}
                    >
                      Archive the project
                    </Button>
                    <Button type="button" variant="ghost" onClick={() => setArchiving(false)}>
                      Keep the project
                    </Button>
                  </p>
                </div>
              ) : null}

              {lifecycleCommand.kind === "refused" || lifecycleCommand.kind === "conflict" ? (
                <p className="mt-3 text-small text-warning">{lifecycleCommand.message}</p>
              ) : null}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Team</CardTitle>
              <CardDescription>
                The members of the project. A member may read the project and apply for its work.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {isOwner ? (
                <div className="flex flex-col gap-5">
                  <form className="flex flex-col gap-5" onSubmit={addMember} noValidate>
                    <Field
                      label="Contributor"
                      error={memberErrors.username}
                      hint="The name of an existing contributor, for example bob."
                    >
                      {({ id, describedBy, invalid }) => (
                        <TextInput
                          id={id}
                          name="username"
                          value={member.username}
                          autoComplete="off"
                          aria-describedby={describedBy}
                          aria-invalid={invalid}
                          ref={memberRef}
                          onChange={(event) => setMember({ ...member, username: event.target.value })}
                        />
                      )}
                    </Field>

                    <Field label="Role" error={memberErrors.role} hint="A viewer reads, an editor changes the content.">
                      {({ id, describedBy, invalid }) => (
                        <Select
                          id={id}
                          name="role"
                          value={member.role}
                          aria-describedby={describedBy}
                          aria-invalid={invalid}
                          onChange={(event) => setMember({ ...member, role: event.target.value })}
                        >
                          <option value="viewer">Viewer</option>
                          <option value="editor">Editor</option>
                        </Select>
                      )}
                    </Field>

                    <div>
                      <Button
                        ref={memberTrigger}
                        type="submit"
                        disabled={memberCommand.kind === "saving"}
                        aria-busy={memberCommand.kind === "saving"}
                      >
                        {memberCommand.kind === "saving" ? "Adding the member" : "Add the member"}
                      </Button>
                    </div>
                  </form>

                  {project.members.length === 0 ? (
                    <p className="text-small text-muted-foreground">
                      The project has no other member yet.
                    </p>
                  ) : (
                    <ul className="flex flex-col gap-3">
                      {project.members.map((item) => (
                        <li
                          key={item.user_id}
                          className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border p-3"
                        >
                          <span className="text-small">
                            <span className="font-medium">{item.username}</span> is a {item.role}.
                          </span>

                          {removing === item.username ? (
                            <span className="flex flex-wrap items-center gap-2">
                              <span className="text-small text-warning">
                                The member loses the access to the project.
                              </span>
                              <Button
                                type="button"
                                size="small"
                                disabled={memberCommand.kind === "saving"}
                                onClick={() => void removeMember(item.username)}
                              >
                                Remove the member
                              </Button>
                              <Button
                                type="button"
                                size="small"
                                variant="ghost"
                                onClick={() => setRemoving(null)}
                              >
                                Keep the member
                              </Button>
                            </span>
                          ) : (
                            <Button
                              type="button"
                              size="small"
                              variant="secondary"
                              onClick={() => setRemoving(item.username)}
                            >
                              Remove
                            </Button>
                          )}
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              ) : (
                <p className="text-small text-muted-foreground">
                  Only the owner of the project manages the members.
                </p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Versions</CardTitle>
              <CardDescription>The released versions of the project.</CardDescription>
            </CardHeader>
            <CardContent>
              <form className="flex flex-col gap-5" onSubmit={addVersion} noValidate>
                <Field label="Version" error={versionErrors.label} hint="At most 100 characters, for example v1.0.0.">
                  {({ id, describedBy, invalid }) => (
                    <TextInput
                      id={id}
                      name="label"
                      value={versionDraft.label}
                      maxLength={100}
                      aria-describedby={describedBy}
                      aria-invalid={invalid}
                      ref={versionRef}
                      onChange={(event) => setVersionDraft({ ...versionDraft, label: event.target.value })}
                    />
                  )}
                </Field>

                <Field
                  label="Commit reference"
                  error={versionErrors.commit_reference}
                  hint="Optional. The commit or the tag of the version."
                >
                  {({ id, describedBy, invalid }) => (
                    <TextInput
                      id={id}
                      name="commit_reference"
                      value={versionDraft.commitReference}
                      aria-describedby={describedBy}
                      aria-invalid={invalid}
                      onChange={(event) =>
                        setVersionDraft({ ...versionDraft, commitReference: event.target.value })
                      }
                    />
                  )}
                </Field>

                <div>
                  <Button
                    ref={versionTrigger}
                    type="submit"
                    disabled={versionCommand.kind === "saving"}
                    aria-busy={versionCommand.kind === "saving"}
                  >
                    {versionCommand.kind === "saving" ? "Recording the version" : "Record the version"}
                  </Button>
                </div>
              </form>

              {project.versions.length === 0 ? (
                <p className="mt-4 text-small text-muted-foreground">
                  No version was released yet.
                </p>
              ) : (
                <ul className="mt-4 flex flex-col gap-2">
                  {project.versions.map((item) => (
                    <li key={item.id} className="text-small">
                      <span className="font-medium">{item.label}</span>
                      {item.commit_reference ? (
                        <span className="text-muted-foreground"> ({item.commit_reference})</span>
                      ) : null}
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </div>
      ) : null}

      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>
    </section>
  );
}
