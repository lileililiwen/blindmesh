"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";

import { useSession } from "@/components/session-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, TextArea } from "@/components/ui/field";
import {
  appealDispute,
  loadDispute,
  newModerationKey,
  type DisputeCommandState,
  type DisputeState,
} from "@/lib/moderation";

/**
 * One case of an arbitration. The parties of the case and the moderation staff
 * read the same page: the state, the stake of the seat, the decision with its
 * reason, the move of the credits and the appeal. Nobody else finds the case.
 */
export function DisputeDetail({ disputeId }: { disputeId: string }) {
  const { csrfToken } = useSession();
  const [state, setState] = useState<DisputeState>({ kind: "loading" });
  const [command, setCommand] = useState<DisputeCommandState>({ kind: "idle" });
  const [reason, setReason] = useState("");
  const [announcement, setAnnouncement] = useState("");
  const reasonRef = useRef<HTMLTextAreaElement>(null);
  const appealRef = useRef<HTMLButtonElement>(null);

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadDispute(disputeId);

    setState(next);
  }, [disputeId]);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadDispute(disputeId);

      if (!cancelled) {
        setState(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [disputeId]);

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="case-heading" aria-busy="true" className="reading-width">
        <h1 id="case-heading" className="text-title font-semibold">
          The case of the arbitration
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the case.</p>
        <div className="mt-4 h-32 w-full rounded bg-muted" aria-hidden="true" />
      </section>
    );
  }

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="case-heading" className="reading-width">
        <h1 id="case-heading" className="text-title font-semibold">
          The case of the arbitration
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          Sign in to read the case. The sign in keeps you on this page.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <a href={`/sign-in?return=${encodeURIComponent(`/disputes/${disputeId}`)}`}>
              Sign in to continue
            </a>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "not-found") {
    return (
      <section aria-labelledby="case-heading" className="reading-width">
        <h1 id="case-heading" className="text-title font-semibold">
          The case of the arbitration
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          This case does not exist.
        </p>
        <p className="mt-4">
          <Button asChild variant="secondary">
            <Link href="/admin/disputes">Back to the cases</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "forbidden" || state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="case-heading" className="reading-width">
        <h1 id="case-heading" className="text-title font-semibold">
          The case of the arbitration
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

  const dispute = state.dispute;
  const fieldErrors = command.kind === "invalid" ? command.fieldErrors : {};
  const mayAppeal = dispute.state === "resolved" && !dispute.appeal_reason;

  return (
    <section aria-labelledby="case-heading" className="reading-width">
      <h1 id="case-heading" className="text-title font-semibold">
        The case of the arbitration
      </h1>

      <p className="mt-2 text-small text-muted-foreground">
        The case of the seat <span className="break-all">{dispute.submission_id}</span>. Only the
        parties of the case and the moderation staff read this page.
      </p>

      <Card className="mt-6">
        <CardHeader>
          <div className="flex flex-wrap items-center gap-2">
            <CardTitle>The state of the case</CardTitle>
            <Badge tone={toneOf(dispute.state)}>{dispute.state}</Badge>
          </div>
          <CardDescription>
            The case was opened {formatMoment(dispute.created_at)}. The credits of the seat wait for
            the decision.
          </CardDescription>
        </CardHeader>

        <CardContent>
          <dl className="flex flex-col gap-2 text-small">
            <Row term="The stake of the seat">
              {dispute.base_reward} credits
            </Row>
            <Row term="The award">{awardOf(dispute.award_to_contributor)}</Row>
            {dispute.awarded_amount ? (
              <Row term="The awarded credits">{dispute.awarded_amount} credits</Row>
            ) : null}
            <Row term="The reason of the case">{dispute.reason}</Row>
            <Row term="The task">
              <span className="break-all">{dispute.task_id}</span>
            </Row>
            <Row term="The seat">
              <span className="break-all">{dispute.participation_id}</span>
            </Row>
          </dl>
        </CardContent>
      </Card>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The decision</CardTitle>
          <CardDescription>
            The reason of the decision and the move of the credits of the seat.
          </CardDescription>
        </CardHeader>

        <CardContent>
          {dispute.resolution_reason ? (
            <p className="text-small">{dispute.resolution_reason}</p>
          ) : (
            <p className="text-small text-muted-foreground">
              The case waits for the decision of a moderator.
            </p>
          )}

          {dispute.resolved_at ? (
            <p className="mt-2 text-small text-muted-foreground">
              The decision was written {formatMoment(dispute.resolved_at)}.
            </p>
          ) : null}

          {dispute.ledger_transaction_kind ? (
            <p className="mt-2 text-small text-muted-foreground">
              The credits moved as a {dispute.ledger_transaction_kind} of{" "}
              {dispute.ledger_amount ?? 0} credits. The ledger row of the move is immutable.
            </p>
          ) : null}

          {dispute.evidence_note ? (
            <p className="mt-3 text-small">
              The moderator asked the parties for more evidence. The note of the request is:{" "}
              {dispute.evidence_note}
            </p>
          ) : dispute.state === "evidence_requested" ? (
            <p className="mt-3 text-small">
              The moderator asked the parties for more evidence. The note of the request stays in the
              trail of the case and is not part of the answer of the API.
            </p>
          ) : null}
        </CardContent>
      </Card>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The appeal</CardTitle>
          <CardDescription>
            A party of the case may appeal the decision once, within seven days of the decision. The
            appeal reaches another moderator and it never changes the credits.
          </CardDescription>
        </CardHeader>

        <CardContent>
          {dispute.appeal_reason ? (
            <div className="flex flex-col gap-2">
              <p className="text-small">{dispute.appeal_reason}</p>
              {dispute.appealed_at ? (
                <p className="text-small text-muted-foreground">
                  The appeal was written {formatMoment(dispute.appealed_at)} and another moderator
                  received the case.
                </p>
              ) : null}
            </div>
          ) : mayAppeal ? (
            <div className="flex flex-col gap-3">
              <Field label="The reason of the appeal" error={fieldErrors.reason}>
                {({ id, describedBy, invalid }) => (
                  <TextArea
                    id={id}
                    ref={reasonRef}
                    rows={3}
                    aria-describedby={describedBy}
                    aria-invalid={invalid}
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                  />
                )}
              </Field>

              <div>
                <Button
                  ref={appealRef}
                  type="button"
                  variant="secondary"
                  disabled={command.kind === "saving"}
                  onClick={() => void appeal()}
                >
                  {command.kind === "saving" ? "Writing the appeal" : "Appeal the decision"}
                </Button>
              </div>
            </div>
          ) : (
            <p className="text-small text-muted-foreground">
              The case carries no appeal. A decision may be appealed once, within seven days.
            </p>
          )}
        </CardContent>
      </Card>

      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>

      {command.kind === "invalid" ? (
        <p className="mt-4 text-small text-warning">
          The appeal was not written. The field above names what the server refused.
        </p>
      ) : null}

      {command.kind === "refused" || command.kind === "conflict" || command.kind === "forbidden" ? (
        <p className="mt-4 text-small text-warning">{command.message}</p>
      ) : null}

      {command.kind === "unauthorized" ? (
        <p className="mt-4 text-small text-warning">
          The session ended. Sign in again to appeal the decision.
        </p>
      ) : null}

      {command.kind === "unavailable" || command.kind === "error" ? (
        <p className="mt-4 text-small text-danger">{command.message}</p>
      ) : null}

      {command.kind === "not-found" ? (
        <p className="mt-4 text-small text-warning">This case does not exist.</p>
      ) : null}
    </section>
  );

  async function appeal() {
    setCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await appealDispute(
      disputeId,
      { reason, expected_version: dispute.version },
      csrfToken,
      newModerationKey(),
    );

    setCommand(result);

    if (result.kind === "saved") {
      setState({ kind: "ready", dispute: result.dispute });
      setReason("");
      setAnnouncement("The appeal is written and another moderator received the case.");
      appealRef.current?.focus();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("The reason of the appeal is not valid.");
      reasonRef.current?.focus();
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to appeal the decision.");
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The appeal was not written.");
  }
}

function Row({ term, children }: { term: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-wrap gap-2">
      <dt className="font-medium">{term}</dt>
      <dd>{children}</dd>
    </div>
  );
}

function awardOf(award: boolean | null | undefined): string {
  if (award === null || award === undefined) {
    return "The base of the seat waits for the decision of a moderator.";
  }

  return award
    ? "The base of the seat goes to the contributor."
    : "The base of the seat goes back to the publisher.";
}

function toneOf(state: string): "neutral" | "success" | "warning" | "danger" {
  switch (state) {
    case "resolved":
      return "success";
    case "evidence_requested":
      return "warning";
    case "closed":
      return "neutral";
    default:
      return "neutral";
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
