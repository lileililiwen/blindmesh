"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, TextArea, TextInput } from "@/components/ui/field";
import { useSession } from "@/components/session-provider";
import {
  acceptSubmission,
  disputeSubmission,
  loadProjectReviews,
  loadSubmission,
  newEvidenceKey,
  requestSubmissionChanges,
  type ReviewsState,
  type SubmissionCommandState,
  type SubmissionDetailState,
} from "@/lib/findings";

/**
 * The reviews of a project. The owner reads the evidence of every seat and
 * decides: the work is accepted with a bonus, the contributor is asked for the
 * one correction, or the disagreement opens the arbitration. The page shows the
 * committed result of the decision and it never hides the pay of a completed
 * scope behind a rejected finding.
 */
export function ReviewPanel({ projectId }: { projectId: string }) {
  const { state: session, csrfToken } = useSession();
  const [state, setState] = useState<ReviewsState>({ kind: "loading" });
  const [selected, setSelected] = useState<string | null>(null);
  const [detail, setDetail] = useState<SubmissionDetailState>({ kind: "loading" });
  const [command, setCommand] = useState<SubmissionCommandState>({ kind: "idle" });
  const [announcement, setAnnouncement] = useState("");
  const [bonus, setBonus] = useState("0");
  const [note, setNote] = useState("");
  const [reason, setReason] = useState("");
  const acceptRef = useRef<HTMLButtonElement>(null);
  const commandKeys = useRef<Record<string, string>>({});

  function keyFor(operation: string): string {
    const name = `${operation}:${selected ?? projectId}`;

    commandKeys.current[name] ??= newEvidenceKey();

    return commandKeys.current[name];
  }

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadProjectReviews(projectId);

    setState(next);
  }, [projectId]);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadProjectReviews(projectId);

      if (!cancelled) {
        setState(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [projectId]);

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="reviews-heading" aria-busy="true" className="reading-width">
        <h1 id="reviews-heading" className="text-title font-semibold">
          The reviews of your project
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the reviews.</p>
        <div className="mt-4 h-32 w-full rounded bg-muted" aria-hidden="true" />
      </section>
    );
  }

  if (state.kind === "not-found") {
    return (
      <section aria-labelledby="reviews-heading" className="reading-width">
        <h1 id="reviews-heading" className="text-title font-semibold">
          The reviews of your project
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          This project does not exist.
        </p>
        <p className="mt-4">
          <Button asChild variant="secondary">
            <Link href="/projects">Back to your projects</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="reviews-heading" className="reading-width">
        <h1 id="reviews-heading" className="text-title font-semibold">
          The reviews of your project
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          Sign in to read the reviews. The sign in keeps you on this page.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <a href={`/sign-in?return=${encodeURIComponent(`/projects/${projectId}/reviews`)}`}>
              Sign in to continue
            </a>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "forbidden" || state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="reviews-heading" className="reading-width">
        <h1 id="reviews-heading" className="text-title font-semibold">
          The reviews of your project
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
  const fieldErrors = command.kind === "invalid" ? command.fieldErrors : {};

  return (
    <section aria-labelledby="reviews-heading" className="reading-width">
      <h1 id="reviews-heading" className="text-title font-semibold">
        The reviews of your project
      </h1>

      <p className="mt-2 text-small text-muted-foreground">
        The evidence of every seat of your project. Accept the work, ask for the one correction or open
        the arbitration.
      </p>

      {state.reviews.length === 0 ? (
        <Card className="mt-6">
          <CardContent>
            <p className="text-small text-muted-foreground">
              No seat of this project sent its evidence yet.
            </p>
          </CardContent>
        </Card>
      ) : (
        <ul className="mt-6 flex flex-col gap-4">
          {state.reviews.map((review) => (
            <li key={review.id}>
              <Card>
                <CardHeader>
                  <div className="flex flex-wrap items-center gap-2">
                    <CardTitle>{review.task_title}</CardTitle>
                    <Badge tone={review.state === "submitted" ? "warning" : "neutral"}>{review.state}</Badge>
                  </div>
                  <CardDescription>
                    {review.contributor_username || "an unknown contributor"} sent the evidence.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <p className="text-small">{review.summary}</p>

                  <p className="mt-2 text-small text-muted-foreground">
                    The base of the seat is {review.base_reward} credits and the bonus of the slot is{" "}
                    {review.slot_bonus} credits.
                    {review.correction_deadline ? ` The correction is due ${formatMoment(review.correction_deadline)}.` : ""}
                  </p>

                  <div className="mt-3 flex flex-wrap items-center gap-3">
                    <Button
                      type="button"
                      variant="secondary"
                      size="small"
                      onClick={() => void open(review.id)}
                    >
                      Read the evidence
                    </Button>

                    {signedIn && review.state === "submitted" ? (
                      <Button
                        type="button"
                        variant="secondary"
                        size="small"
                        onClick={() => void open(review.id)}
                      >
                        Decide about this seat
                      </Button>
                    ) : null}
                  </div>
                </CardContent>
              </Card>
            </li>
          ))}
        </ul>
      )}

      {selected && detail.kind === "ready" ? (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>The evidence of {detail.submission.task_title}</CardTitle>
            <CardDescription>
              The revisions, the findings and the files of the seat. The decision moves the credits of
              the seat.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-small text-muted-foreground">
              The base of the seat is {detail.submission.base_reward} credits and the bonus of the slot
              is {detail.submission.slot_bonus} credits.
            </p>

            <div className="mt-3 flex flex-col gap-3">
              {detail.submission.revisions.map((revision) => (
                <div key={revision.id} className="rounded-md border border-border p-3">
                  <p className="text-small font-medium">Revision {revision.number}</p>
                  <p className="text-small">{revision.summary}</p>

                  {revision.steps.length > 0 ? (
                    <ul className="mt-2 flex flex-col gap-1 text-small">
                      {revision.steps.map((step) => (
                        <li key={step.position}>
                          {step.title}: {step.result || "no result"}
                        </li>
                      ))}
                    </ul>
                  ) : null}

                  {revision.tested_areas.length > 0 ? (
                    <p className="mt-2 text-small text-muted-foreground">
                      The covered areas: {revision.tested_areas.join(", ")}
                    </p>
                  ) : null}

                  {revision.attachments.length > 0 ? (
                    <ul className="mt-2 flex flex-col gap-1 text-small">
                      {revision.attachments.map((file) => (
                        <li key={file.id}>
                          {file.file_name} — {file.state}
                        </li>
                      ))}
                    </ul>
                  ) : null}
                </div>
              ))}
            </div>

            {detail.submission.findings.length > 0 ? (
              <ul className="mt-3 flex flex-col gap-2">
                {detail.submission.findings.map((finding) => (
                  <li key={finding.id} className="rounded-md border border-border p-3 text-small">
                    <p className="font-medium">
                      {finding.title} — {finding.category} / {finding.severity} / {finding.state}
                    </p>
                    <Link className="text-small underline" href={`/findings/${finding.id}`}>
                      Open the finding
                    </Link>
                  </li>
                ))}
              </ul>
            ) : null}

            {signedIn && detail.submission.can_review ? (
              <div className="mt-4 flex flex-col gap-4">
                <Field
                  label="The bonus of the seat"
                  error={fieldErrors.bonus}
                  hint={`At most ${detail.submission.slot_bonus} credits. The unused bonus goes back to you.`}
                >
                  {({ id, describedBy, invalid }) => (
                    <TextInput
                      id={id}
                      type="number"
                      min={0}
                      max={detail.submission.slot_bonus}
                      aria-describedby={describedBy}
                      aria-invalid={invalid}
                      value={bonus}
                      onChange={(event) => setBonus(event.target.value)}
                    />
                  )}
                </Field>

                <div className="flex flex-wrap items-center gap-3">
                  <Button
                    ref={acceptRef}
                    type="button"
                    variant="primary"
                    disabled={command.kind === "saving"}
                    onClick={() => void decide("accept")}
                  >
                    {command.kind === "saving" ? "Saving the decision" : "Accept the work"}
                  </Button>
                </div>

                <Field
                  label="The note of the one correction"
                  error={fieldErrors.note}
                  hint="The contributor has 48 hours to answer the correction."
                >
                  {({ id, describedBy, invalid }) => (
                    <TextArea
                      id={id}
                      rows={3}
                      aria-describedby={describedBy}
                      aria-invalid={invalid}
                      value={note}
                      onChange={(event) => setNote(event.target.value)}
                    />
                  )}
                </Field>

                <Button
                  type="button"
                  variant="secondary"
                  disabled={command.kind === "saving"}
                  onClick={() => void decide("changes")}
                >
                  Ask for the one correction
                </Button>

                <Field
                  label="The reason of the dispute"
                  error={fieldErrors.reason}
                  hint="A dispute holds the credits of the seat until the arbitration decides."
                >
                  {({ id, describedBy, invalid }) => (
                    <TextArea
                      id={id}
                      rows={3}
                      aria-describedby={describedBy}
                      aria-invalid={invalid}
                      value={reason}
                      onChange={(event) => setReason(event.target.value)}
                    />
                  )}
                </Field>

                <Button
                  type="button"
                  variant="secondary"
                  disabled={command.kind === "saving"}
                  onClick={() => void decide("dispute")}
                >
                  Open the dispute
                </Button>
              </div>
            ) : null}
          </CardContent>
        </Card>
      ) : null}

      {selected && detail.kind === "loading" ? (
        <Card className="mt-6" aria-busy="true">
          <CardContent>
            <p className="text-small text-muted-foreground">Loading the evidence.</p>
          </CardContent>
        </Card>
      ) : null}

      {selected && (detail.kind === "not-found" || detail.kind === "forbidden") ? (
        <Card className="mt-6">
          <CardContent>
            <p role="status" className="text-small text-muted-foreground">
              The evidence of this seat is not readable.
            </p>
          </CardContent>
        </Card>
      ) : null}

      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>

      {command.kind === "refused" || command.kind === "conflict" || command.kind === "forbidden" ? (
        <p className="mt-4 text-small text-warning">{command.message}</p>
      ) : null}

      {command.kind === "error" || command.kind === "unavailable" ? (
        <p className="mt-4 text-small text-danger">{command.message}</p>
      ) : null}
    </section>
  );

  async function open(id: string) {
    setSelected(id);
    setDetail({ kind: "loading" });

    const next = await loadSubmission(id);

    setDetail(next);
  }

  async function decide(action: "accept" | "changes" | "dispute") {
    if (!selected) {
      return;
    }

    setCommand({ kind: "saving" });
    setAnnouncement("");

    const result =
      action === "accept"
        ? await acceptSubmission(
            selected,
            { bonus: Number.parseInt(bonus, 10) || 0 },
            csrfToken,
            keyFor("accept"),
          )
        : action === "changes"
          ? await requestSubmissionChanges(selected, { note }, csrfToken, keyFor("changes"))
          : await disputeSubmission(selected, { reason }, csrfToken, keyFor("dispute"));

    setCommand(result);

    if (result.kind === "saved") {
      setDetail({ kind: "ready", submission: result.submission });
      setAnnouncement(
        action === "accept"
          ? "The work is accepted and the credits of the seat are settled."
          : action === "changes"
            ? "The contributor has 48 hours to answer the correction."
            : "The dispute is open. The credits of the seat wait for the arbitration.",
      );
      acceptRef.current?.focus();
      await load();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("Some fields of the decision are not valid.");
      return;
    }

    if (result.kind === "refused" || result.kind === "conflict" || result.kind === "forbidden") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to decide.");
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The decision was not saved.");
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
