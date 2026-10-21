"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";

import { useSession } from "@/components/session-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, Select, TextArea, TextInput } from "@/components/ui/field";
import {
  createReport,
  loadAccountStanding,
  newModerationKey,
  type ReportCommandState,
  type StandingState,
} from "@/lib/moderation";

/**
 * The report of abusive content. The reporter writes the subject, the kind of
 * the subject and the reason, and the page shows the receipt of the report
 * after the server committed it. The identity of the reporter stays with the
 * moderation staff: nobody else reads the receipt.
 */
const subjects = [
  { value: "submission", label: "A seat of a task" },
  { value: "task", label: "A task" },
  { value: "user", label: "An account" },
];

export function ReportForm() {
  const { state: session, csrfToken, refresh } = useSession();
  const [command, setCommand] = useState<ReportCommandState>({ kind: "idle" });
  const [subjectType, setSubjectType] = useState("submission");
  const [subjectId, setSubjectId] = useState("");
  const [reason, setReason] = useState("");
  const [announcement, setAnnouncement] = useState("");
  const [standing, setStanding] = useState<StandingState | null>(null);
  const reasonRef = useRef<HTMLTextAreaElement>(null);
  const receiptRef = useRef<HTMLDivElement>(null);

  const signedIn = session.kind === "authenticated";
  const fieldErrors = command.kind === "invalid" ? command.fieldErrors : {};

  useEffect(() => {
    if (session.kind === "authenticated" || session.kind === "loading") {
      return;
    }

    let cancelled = false;

    void (async () => {
      const next = await loadAccountStanding();

      if (!cancelled) {
        setStanding(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [session.kind]);

  if (session.kind === "loading") {
    return (
      <section aria-labelledby="report-heading" aria-busy="true" className="reading-width">
        <h1 id="report-heading" className="text-title font-semibold">
          Report abusive content
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading your session.</p>
        <div className="mt-4 h-32 w-full rounded bg-muted" aria-hidden="true" />
      </section>
    );
  }

  if (session.kind === "anonymous") {
    return (
      <section aria-labelledby="report-heading" className="reading-width">
        <h1 id="report-heading" className="text-title font-semibold">
          Report abusive content
        </h1>

        <p role="status" className="mt-3 text-muted-foreground">
          Sign in to write a report. The sign in keeps you on this page.
        </p>

        <p className="mt-4">
          <Button asChild variant="primary">
            <a href={`/sign-in?return=${encodeURIComponent("/reports/new")}`}>Sign in to continue</a>
          </Button>
        </p>
      </section>
    );
  }

  if (!signedIn) {
    return (
      <section aria-labelledby="report-heading" className="reading-width">
        <h1 id="report-heading" className="text-title font-semibold">
          Report abusive content
        </h1>

        <p role="status" className="mt-3 text-muted-foreground">
          {standing?.kind === "banned"
            ? standing.message
            : session.kind === "forbidden" || session.kind === "error" || session.kind === "unavailable"
              ? session.message
              : "The session is not readable right now."}
        </p>

        <p className="mt-4">
          <Button type="button" variant="secondary" onClick={() => void refresh()}>
            Try again
          </Button>
        </p>
      </section>
    );
  }

  if (command.kind === "saved") {
    return (
      <section aria-labelledby="report-heading" className="reading-width">
        <h1 id="report-heading" className="text-title font-semibold">
          Report abusive content
        </h1>

        <div ref={receiptRef} tabIndex={-1}>
          <Card className="mt-6">
            <CardHeader>
              <div className="flex flex-wrap items-center gap-2">
                <CardTitle>Your report is written</CardTitle>
                <Badge tone="success">{command.report.state}</Badge>
              </div>
              <CardDescription>
                The receipt is the only answer you get. It names the report without naming you.
              </CardDescription>
            </CardHeader>

            <CardContent>
              <dl className="flex flex-col gap-2 text-small">
                <div className="flex flex-wrap gap-2">
                  <dt className="font-medium">The receipt</dt>
                  <dd>{command.report.receipt}</dd>
                </div>
                <div className="flex flex-wrap gap-2">
                  <dt className="font-medium">The state</dt>
                  <dd>{command.report.state}</dd>
                </div>
                <div className="flex flex-wrap gap-2">
                  <dt className="font-medium">The written report</dt>
                  <dd>{formatMoment(command.report.created_at)}</dd>
                </div>
              </dl>

              <p className="mt-3 text-small text-muted-foreground">
                A moderator reads the report. Nobody else learns who wrote it, and the receipt keeps
                no name of yours.
              </p>
            </CardContent>
          </Card>
        </div>

        <p role="status" aria-live="polite" className="sr-only">
          The report is written and the receipt is {command.report.receipt}.
        </p>

        <p className="mt-4">
          <Button
            type="button"
            variant="secondary"
            onClick={() => {
              setCommand({ kind: "idle" });
              setSubjectId("");
              setReason("");
              setAnnouncement("");
              receiptRef.current?.focus();
            }}
          >
            Write another report
          </Button>
        </p>
      </section>
    );
  }

  return (
    <section aria-labelledby="report-heading" className="reading-width">
      <h1 id="report-heading" className="text-title font-semibold">
        Report abusive content
      </h1>

      <p className="mt-2 text-small text-muted-foreground">
        Name the content you saw and tell the moderator what is wrong with it. The report stays with
        the moderation staff.
      </p>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The report</CardTitle>
          <CardDescription>
            The reason holds 20 to 5000 characters. It is the only thing a moderator reads.
          </CardDescription>
        </CardHeader>

        <CardContent>
          <form
            className="flex flex-col gap-4"
            onSubmit={(event) => {
              event.preventDefault();
              void submit();
            }}
          >
            <Field label="The kind of the subject" error={fieldErrors.subject_type}>
              {({ id, describedBy, invalid }) => (
                <Select
                  id={id}
                  aria-describedby={describedBy}
                  aria-invalid={invalid}
                  value={subjectType}
                  onChange={(event) => setSubjectType(event.target.value)}
                >
                  {subjects.map((subject) => (
                    <option key={subject.value} value={subject.value}>
                      {subject.label}
                    </option>
                  ))}
                </Select>
              )}
            </Field>

            <Field
              label="The identifier of the subject"
              error={fieldErrors.subject_id}
              hint="The identifier of the seat, of the task or of the account you report."
            >
              {({ id, describedBy, invalid }) => (
                <TextInput
                  id={id}
                  aria-describedby={describedBy}
                  aria-invalid={invalid}
                  value={subjectId}
                  onChange={(event) => setSubjectId(event.target.value)}
                />
              )}
            </Field>

            <Field label="The reason of the report" error={fieldErrors.reason}>
              {({ id, describedBy, invalid }) => (
                <TextArea
                  id={id}
                  ref={reasonRef}
                  rows={5}
                  aria-describedby={describedBy}
                  aria-invalid={invalid}
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                />
              )}
            </Field>

            <div>
              <Button type="submit" disabled={command.kind === "saving"}>
                {command.kind === "saving" ? "Writing the report" : "Write the report"}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>

      {command.kind === "invalid" ? (
        <p className="mt-4 text-small text-warning">
          The report was not written. The fields above name what the server refused.
        </p>
      ) : null}

      {command.kind === "refused" || command.kind === "forbidden" ? (
        <p className="mt-4 text-small text-warning">{command.message}</p>
      ) : null}

      {command.kind === "unauthorized" ? (
        <p className="mt-4 text-small text-warning">
          The session ended. Sign in again to write the report.
        </p>
      ) : null}

      {command.kind === "unavailable" || command.kind === "error" ? (
        <p className="mt-4 text-small text-danger">{command.message}</p>
      ) : null}

      {command.kind === "not-found" ? (
        <p className="mt-4 text-small text-warning">
          The report was not written. The subject of the report does not exist.
        </p>
      ) : null}

      <p className="mt-4 text-small text-muted-foreground">
        The report of a seat of a task belongs to the{" "}
        <Link className="underline" href="/work">
          work of your account
        </Link>
        .
      </p>
    </section>
  );

  async function submit() {
    setCommand({ kind: "saving" });
    setAnnouncement("");

    const result = await createReport(
      {
        reason,
        subject_type: subjectType,
        subject_id: subjectId.trim().length > 0 ? subjectId.trim() : null,
      },
      csrfToken,
      newModerationKey(),
    );

    setCommand(result);

    if (result.kind === "saved") {
      setAnnouncement(`The report is written and the receipt is ${result.report.receipt}.`);
      return;
    }

    if (result.kind === "invalid") {
      setAnnouncement("Some fields of the report are not valid.");
      reasonRef.current?.focus();
      return;
    }

    if (result.kind === "unauthorized") {
      setAnnouncement("The session ended. Sign in again to write the report.");
      return;
    }

    if (result.kind === "error" || result.kind === "unavailable") {
      setAnnouncement(result.message);
      return;
    }

    setAnnouncement("The report was not written.");
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
