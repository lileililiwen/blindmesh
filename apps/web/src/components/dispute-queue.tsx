"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";

import { useSession } from "@/components/session-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { CheckboxField, Field, TextArea, TextInput } from "@/components/ui/field";
import {
  bonusOf,
  closeDispute,
  loadDispute,
  loadDisputes,
  newModerationKey,
  requestDisputeEvidence,
  resolveDispute,
  type DisputeCommandState,
  type DisputeQueueState,
  type DisputeState,
} from "@/lib/moderation";

/**
 * The queue of the cases for the moderation staff. A moderator asks the parties
 * for more evidence, decides the case or closes it. Every action runs on the
 * committed state of the case, and the answer of the server is shown as it is.
 */
export function DisputeQueue() {
  const { csrfToken } = useSession();
  const [state, setState] = useState<DisputeQueueState>({ kind: "loading" });
  const [selected, setSelected] = useState<string | null>(null);
  const [detail, setDetail] = useState<DisputeState>({ kind: "loading" });
  const [command, setCommand] = useState<DisputeCommandState>({ kind: "idle" });
  const [note, setNote] = useState("");
  const [reason, setReason] = useState("");
  const [closeReason, setCloseReason] = useState("");
  const [awardBase, setAwardBase] = useState(true);
  const [bonus, setBonus] = useState("");
  const [announcement, setAnnouncement] = useState("");
  const decisionRef = useRef<HTMLButtonElement>(null);

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadDisputes();

    setState(next);
  }, []);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadDisputes();

      if (!cancelled) {
        setState(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="cases-heading" aria-busy="true" className="reading-width">
        <h1 id="cases-heading" className="text-title font-semibold">
          The cases of the moderation
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the cases.</p>
        <div className="mt-4 h-32 w-full rounded bg-muted" aria-hidden="true" />
      </section>
    );
  }

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="cases-heading" className="reading-width">
        <h1 id="cases-heading" className="text-title font-semibold">
          The cases of the moderation
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          Sign in to read the cases. The sign in keeps you on this page.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <a href={`/sign-in?return=${encodeURIComponent("/admin/disputes")}`}>Sign in to continue</a>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "forbidden") {
    return (
      <section aria-labelledby="cases-heading" className="reading-width">
        <h1 id="cases-heading" className="text-title font-semibold">
          The cases of the moderation
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {state.message}
        </p>
        <p className="mt-2 text-small text-muted-foreground">
          This area belongs to the moderation staff. A moderator and an administrator read the queue
          of the cases.
        </p>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="cases-heading" className="reading-width">
        <h1 id="cases-heading" className="text-title font-semibold">
          The cases of the moderation
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

  const fieldErrors = command.kind === "invalid" ? command.fieldErrors : {};
  const caseDetail = detail.kind === "ready" ? detail.dispute : null;

  return (
    <section aria-labelledby="cases-heading" className="reading-width">
      <h1 id="cases-heading" className="text-title font-semibold">
        The cases of the moderation
      </h1>

      <p className="mt-2 text-small text-muted-foreground">
        The cases of the credits of a seat. The decision moves the escrow of the seat and the ledger
        row of the move stays immutable.
      </p>

      {state.disputes.length === 0 ? (
        <Card className="mt-6">
          <CardContent>
            <p className="text-small text-muted-foreground">
              No case waits for a moderator right now.
            </p>
          </CardContent>
        </Card>
      ) : (
        <ul className="mt-6 flex flex-col gap-4">
          {state.disputes.map((dispute) => (
            <li key={dispute.id}>
              <Card>
                <CardHeader>
                  <div className="flex flex-wrap items-center gap-2">
                    <CardTitle>
                      The case of the seat <span className="break-all">{dispute.submission_id}</span>
                    </CardTitle>
                    <Badge tone={toneOf(dispute.state)}>{dispute.state}</Badge>
                  </div>
                  <CardDescription>
                    The stake of the seat is {dispute.base_reward} credits. The case was opened{" "}
                    {formatMoment(dispute.created_at)}.
                  </CardDescription>
                </CardHeader>

                <CardContent>
                  <div className="flex flex-wrap items-center gap-3">
                    <Button
                      type="button"
                      variant="secondary"
                      size="small"
                      onClick={() => void open(dispute.id)}
                    >
                      Work on this case
                    </Button>

                    <Link className="text-small underline" href={`/disputes/${dispute.id}`}>
                      Open the case
                    </Link>
                  </div>
                </CardContent>
              </Card>
            </li>
          ))}
        </ul>
      )}

      {selected ? (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle>The work on the case</CardTitle>
            <CardDescription>
              Ask the parties for more evidence, decide the case or close it. The answer of the
              server is shown as it is.
            </CardDescription>
          </CardHeader>

          <CardContent>
            {detail.kind === "loading" ? (
              <p className="text-small text-muted-foreground">Loading the case.</p>
            ) : null}

            {detail.kind === "not-found" || detail.kind === "forbidden" ? (
              <p role="status" className="text-small text-muted-foreground">
                The case is not readable.
              </p>
            ) : null}

            {detail.kind === "error" || detail.kind === "unavailable" ? (
              <p role="status" className="text-small text-danger">
                {detail.message}
              </p>
            ) : null}

            {caseDetail ? (
              <div className="flex flex-col gap-4">
                <p className="text-small text-muted-foreground">
                  The state of the case is {caseDetail.state} and the stake of the seat is{" "}
                  {caseDetail.base_reward} credits.
                </p>

                <Field
                  label="The note of the request of more evidence"
                  error={fieldErrors.note}
                  hint="The parties of the case read the note in the trail of the case."
                >
                  {({ id, describedBy, invalid }) => (
                    <TextArea
                      id={id}
                      rows={2}
                      aria-describedby={describedBy}
                      aria-invalid={invalid}
                      value={note}
                      onChange={(event) => setNote(event.target.value)}
                    />
                  )}
                </Field>

                <div>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={command.kind === "saving"}
                    onClick={() => void act("evidence")}
                  >
                    Ask for more evidence
                  </Button>
                </div>

                <Field
                  label="The reason of the decision"
                  error={fieldErrors.reason}
                  hint="The reason holds 20 to 5000 characters and it is read by the parties of the case."
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

                <CheckboxField
                  id="award-base"
                  label="The base of the seat goes to the contributor. Without it the base goes back to the publisher."
                  checked={awardBase}
                  onChange={(event) => setAwardBase(event.target.checked)}
                />

                {awardBase ? (
                  <Field
                    label="The bonus of the decision"
                    error={fieldErrors.bonus}
                    hint="The extra credits of the decision, in whole credits. The bonus may not exceed the funded escrow of the task, and it is only awarded together with the base of the seat."
                  >
                    {({ id, describedBy, invalid }) => (
                      <TextInput
                        id={id}
                        name="bonus"
                        type="number"
                        inputMode="numeric"
                        min={0}
                        step={1}
                        aria-describedby={describedBy}
                        aria-invalid={invalid}
                        value={bonus}
                        onChange={(event) => setBonus(event.target.value)}
                      />
                    )}
                  </Field>
                ) : null}

                {!awardBase && bonus.trim() !== "" ? (
                  <p className="text-small text-warning">
                    A bonus may only be awarded together with the base of the seat. The bonus stays in
                    the field and it is not sent with this decision.
                  </p>
                ) : null}

                <div>
                  <Button
                    ref={decisionRef}
                    type="button"
                    variant="primary"
                    disabled={command.kind === "saving"}
                    onClick={() => void act("resolve")}
                  >
                    Decide the case
                  </Button>
                </div>

                <Field
                  label="The reason of the closing"
                  error={fieldErrors.close_reason}
                  hint="A case may only be closed after the decision and after the window of the appeal."
                >
                  {({ id, describedBy, invalid }) => (
                    <TextArea
                      id={id}
                      rows={2}
                      aria-describedby={describedBy}
                      aria-invalid={invalid}
                      value={closeReason}
                      onChange={(event) => setCloseReason(event.target.value)}
                    />
                  )}
                </Field>

                <div>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={command.kind === "saving"}
                    onClick={() => void act("close")}
                  >
                    Close the case
                  </Button>
                </div>
              </div>
            ) : null}
          </CardContent>
        </Card>
      ) : null}

      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>

      {command.kind === "invalid" ? (
        <p className="mt-4 text-small text-warning">
          The command was not run. The fields above name what the server refused.
        </p>
      ) : null}

      {command.kind === "refused" || command.kind === "conflict" || command.kind === "forbidden" ? (
        <p className="mt-4 text-small text-warning">{command.message}</p>
      ) : null}

      {command.kind === "unauthorized" ? (
        <p className="mt-4 text-small text-warning">
          The session ended. Sign in again to work on the case.
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

  async function open(id: string) {
    setSelected(id);
    setDetail({ kind: "loading" });
    setCommand({ kind: "idle" });
    setAnnouncement("");

    const next = await loadDispute(id);

    setDetail(next);
  }

  async function act(action: "evidence" | "resolve" | "close") {
    if (!selected || !caseDetail) {
      return;
    }

    setCommand({ kind: "saving" });
    setAnnouncement("");

    const version = caseDetail.version;

    const result =
      action === "evidence"
        ? await requestDisputeEvidence(
            selected,
            { note, expected_version: version },
            csrfToken,
            newModerationKey(),
          )
        : action === "resolve"
          ? await resolveDispute(
              selected,
              {
                award_base: awardBase,
                reason,
                bonus: awardBase ? bonusOf(bonus) : 0,
                expected_version: version,
              },
              csrfToken,
              newModerationKey(),
            )
          : await closeDispute(
              selected,
              { reason: closeReason, expected_version: version },
              csrfToken,
              newModerationKey(),
            );

    setCommand(result);

    if (result.kind === "saved") {
      setDetail({ kind: "ready", dispute: result.dispute });
      setNote("");
      setReason("");
      setCloseReason("");
      setBonus("");
      setAnnouncement(
        action === "evidence"
          ? "The parties of the case received the request of more evidence."
          : action === "resolve"
            ? "The case is decided and the credits of the seat moved."
            : "The case is closed.",
      );
      decisionRef.current?.focus();
      await load();
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("Some fields of the command are not valid.");
      return;
    }

    if (result.kind === "conflict") {
      setAnnouncement(result.message);
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to work on the case.");
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The command was not run.");
  }
}

function toneOf(state: string): "neutral" | "success" | "warning" | "danger" {
  switch (state) {
    case "resolved":
      return "success";
    case "evidence_requested":
      return "warning";
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
