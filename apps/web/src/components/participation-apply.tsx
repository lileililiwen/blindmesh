"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";

import { TaskModeBadges, TaskStateBadge, TaskVisibilityBadge } from "@/components/task-badges";
import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { CheckboxField } from "@/components/ui/field";
import { credits } from "@/lib/credits";
import {
  applyForSlot,
  newSeatKey,
  type ParticipationCommandState,
} from "@/lib/participations";
import { loadTask, type TaskDetailState } from "@/lib/tasks";

/**
 * The page that applies for one slot of a published task. The page shows the
 * scope of the task, the deadline of the admission and the consent of the
 * scope. The consent is a statement and not a credential, so the page never
 * asks for a password of a resource.
 */
export function ParticipationApply({ taskId }: { taskId: string }) {
  const { state: session, csrfToken } = useSession();
  const [task, setTask] = useState<TaskDetailState>({ kind: "loading" });
  const [command, setCommand] = useState<ParticipationCommandState>({ kind: "idle" });
  const [consent, setConsent] = useState(false);
  const [announcement, setAnnouncement] = useState("");
  const consentRef = useRef<HTMLInputElement>(null);
  const submitRef = useRef<HTMLButtonElement>(null);
  const commandKey = useRef<string | null>(null);

  const load = useCallback(async () => {
    setTask({ kind: "loading" });

    const next = await loadTask(taskId);

    setTask(next);
  }, [taskId]);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadTask(taskId);

      if (!cancelled) {
        setTask(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [taskId]);

  if (task.kind === "loading") {
    return (
      <section aria-labelledby="apply-heading" aria-busy="true" className="reading-width">
        <h1 id="apply-heading" className="text-title font-semibold">
          Apply for a slot
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the task.</p>
        <div className="mt-4 h-32 w-full rounded bg-muted" aria-hidden="true" />
      </section>
    );
  }

  if (task.kind === "not-found") {
    return (
      <section aria-labelledby="apply-heading" className="reading-width">
        <h1 id="apply-heading" className="text-title font-semibold">
          Apply for a slot
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

  if (task.kind === "unauthorized" || task.kind === "forbidden") {
    return (
      <section aria-labelledby="apply-heading" className="reading-width">
        <h1 id="apply-heading" className="text-title font-semibold">
          Apply for a slot
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {task.kind === "unauthorized"
            ? "Sign in to apply for a slot. The sign in keeps you on this page."
            : task.message}
        </p>
        <p className="mt-4 flex flex-wrap gap-3">
          {task.kind === "unauthorized" ? (
            <Button asChild variant="primary">
              <a href={`/sign-in?return=${encodeURIComponent(`/tasks/${taskId}/apply`)}`}>
                Sign in to continue
              </a>
            </Button>
          ) : null}
          <Button asChild variant="secondary">
            <Link href="/tasks">Back to the tasks</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (task.kind === "error" || task.kind === "unavailable") {
    return (
      <section aria-labelledby="apply-heading" className="reading-width">
        <h1 id="apply-heading" className="text-title font-semibold">
          Apply for a slot
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {task.message}
        </p>
        <p className="mt-4">
          <Button type="button" variant="secondary" onClick={() => void load()}>
            Try again
          </Button>
        </p>
      </section>
    );
  }

  const detail = task.task;
  const signedIn = session.kind === "authenticated";
  const applied = command.kind === "saved";

  return (
    <section aria-labelledby="apply-heading" className="reading-width">
      <div className="flex flex-wrap items-center gap-2">
        <h1 id="apply-heading" className="text-title font-semibold">
          Apply for a slot
        </h1>
        <TaskStateBadge status={detail.status} />
        <TaskVisibilityBadge visibility={detail.visibility} />
        <TaskModeBadges modes={detail.modes} />
      </div>

      <p className="mt-2 text-small text-muted-foreground">
        {detail.project_name ? `In the project ${detail.project_name}. ` : ""}
        Published by {detail.owner_username || "an unknown contributor"}.
      </p>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>{detail.title}</CardTitle>
          <CardDescription>The scope, the budget and the deadline of the seat.</CardDescription>
        </CardHeader>
        <CardContent>
          {detail.scope ? (
            <p className="whitespace-pre-line text-small">{detail.scope}</p>
          ) : (
            <p className="text-small text-muted-foreground">
              The scope of this task is released to the contributors that hold the seat.
            </p>
          )}

          <dl className="mt-4 grid grid-cols-2 gap-x-4 gap-y-3 text-small sm:grid-cols-3">
            <div>
              <dt className="text-muted-foreground">Reward of a slot</dt>
              <dd>{credits(Number(detail.base_reward) + Number(detail.slot_bonus))} credits</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Slots</dt>
              <dd>{detail.slot_count}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Deadline of the task</dt>
              <dd>{detail.deadline ? formatMoment(detail.deadline) : "no deadline"}</dd>
            </div>
          </dl>

          {detail.requirements.length > 0 ? (
            <div className="mt-4">
              <p className="text-small font-medium">Hard requirements</p>
              <ul className="mt-1 flex flex-col gap-1 text-small">
                {detail.requirements.map((requirement) => (
                  <li key={`${requirement.kind}:${requirement.value}`}>
                    {requirement.kind}: {requirement.value}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </CardContent>
      </Card>

      {applied ? (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>Your application is committed</CardTitle>
            <CardDescription>
              The owner of the task answers the application. You find the seat in your work.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-small">
              The state of your seat is <strong>{command.kind === "saved" ? command.participation.state : ""}</strong>.
            </p>
            <p className="mt-4 flex flex-wrap gap-3">
              <Button asChild variant="primary">
                <Link href={`/work/${command.kind === "saved" ? command.participation.id : ""}`}>
                  Open your seat
                </Link>
              </Button>
              <Button asChild variant="secondary">
                <Link href="/work">Back to your work</Link>
              </Button>
            </p>
          </CardContent>
        </Card>
      ) : (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>Apply for this seat</CardTitle>
            <CardDescription>
              The admission of a task closes 48 hours after its publication. An accepted seat has to be
              started within 24 hours.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form
              className="flex flex-col gap-5"
              onSubmit={(event) => {
                event.preventDefault();
                void apply();
              }}
              noValidate
            >
              <CheckboxField
                id="apply-scope-consent"
                label="I accept the scope of this task and I will report what I find."
                checked={consent}
                onChange={(event) => setConsent(event.target.checked)}
              />

              <div className="flex flex-wrap items-center gap-3">
                <Button
                  ref={submitRef}
                  type="submit"
                  variant="primary"
                  disabled={!signedIn || command.kind === "saving"}
                  aria-busy={command.kind === "saving"}
                >
                  {command.kind === "saving" ? "Sending the application" : "Apply for the seat"}
                </Button>

                {!signedIn ? (
                  <Button asChild variant="secondary">
                    <a href={`/sign-in?return=${encodeURIComponent(`/tasks/${taskId}/apply`)}`}>
                      Sign in to apply
                    </a>
                  </Button>
                ) : null}
              </div>
            </form>

            <p role="status" aria-live="polite" className="sr-only">
              {announcement}
            </p>

            {command.kind === "invalid" ? (
              <ul className="mt-4 flex flex-col gap-1 text-small text-danger">
                {Object.entries(command.fieldErrors).map(([field, messages]) => (
                  <li key={field}>{messages.join(" ")}</li>
                ))}
              </ul>
            ) : null}

            {command.kind === "refused" || command.kind === "conflict" ? (
              <p className="mt-4 text-small text-warning">{command.message}</p>
            ) : null}

            {command.kind === "forbidden" ? (
              <p className="mt-4 text-small text-warning">{command.message}</p>
            ) : null}

            {command.kind === "error" || command.kind === "unavailable" ? (
              <p className="mt-4 text-small text-danger">{command.message}</p>
            ) : null}
          </CardContent>
        </Card>
      )}
    </section>
  );

  async function apply() {
    if (!consent) {
      setCommand({
        kind: "invalid",
        fieldErrors: {
          scope_consent: ["Accept the scope of the task before you apply for the seat."],
        },
      });
      setAnnouncement("The scope of the task has to be accepted before you apply.");
      consentRef.current?.focus();
      return;
    }

    commandKey.current ??= newSeatKey();

    setCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await applyForSlot(
      taskId,
      { scope_consent: true, expected_version: detail.version },
      csrfToken,
      commandKey.current,
    );

    setCommand(result);

    if (result.kind === "saved") {
      setAnnouncement("Your application is committed. The owner of the task answers it.");
      submitRef.current?.focus();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("The application is not valid yet.");
      return;
    }

    if (result.kind === "forbidden") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "refused" || result.kind === "conflict") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to apply for the seat.");
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The application was not sent.");
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
