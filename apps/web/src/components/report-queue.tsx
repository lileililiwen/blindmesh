"use client";

import { useCallback, useEffect, useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { loadReports, type ReportQueueState } from "@/lib/moderation";

/**
 * The queue of the reports for the moderation staff. The body of a report stays
 * with the staff: the reporter never reads the queue and nobody else reads a
 * report. An account without the role of the staff sees the honest answer of
 * the API.
 */
export function ReportQueue() {
  const [state, setState] = useState<ReportQueueState>({ kind: "loading" });

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadReports();

    setState(next);
  }, []);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadReports();

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
      <section aria-labelledby="reports-heading" aria-busy="true" className="reading-width">
        <h1 id="reports-heading" className="text-title font-semibold">
          The reports of the moderation
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the reports.</p>
        <div className="mt-4 h-32 w-full rounded bg-muted" aria-hidden="true" />
      </section>
    );
  }

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="reports-heading" className="reading-width">
        <h1 id="reports-heading" className="text-title font-semibold">
          The reports of the moderation
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          Sign in to read the reports. The sign in keeps you on this page.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <a href={`/sign-in?return=${encodeURIComponent("/admin/reports")}`}>Sign in to continue</a>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "forbidden") {
    return (
      <section aria-labelledby="reports-heading" className="reading-width">
        <h1 id="reports-heading" className="text-title font-semibold">
          The reports of the moderation
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {state.message}
        </p>
        <p className="mt-2 text-small text-muted-foreground">
          This area belongs to the moderation staff. A moderator and an administrator read the queue
          of the reports.
        </p>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="reports-heading" className="reading-width">
        <h1 id="reports-heading" className="text-title font-semibold">
          The reports of the moderation
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

  return (
    <section aria-labelledby="reports-heading" className="reading-width">
      <h1 id="reports-heading" className="text-title font-semibold">
        The reports of the moderation
      </h1>

      <p className="mt-2 text-small text-muted-foreground">
        The reports wait for a moderator. The name of the reporter never appears here and never
        leaves the staff.
      </p>

      {state.reports.length === 0 ? (
        <Card className="mt-6">
          <CardContent>
            <p className="text-small text-muted-foreground">
              No report waits for a moderator right now.
            </p>
          </CardContent>
        </Card>
      ) : (
        <ul className="mt-6 flex flex-col gap-4">
          {state.reports.map((report) => (
            <li key={report.id}>
              <Card>
                <CardHeader>
                  <div className="flex flex-wrap items-center gap-2">
                    <CardTitle>{report.receipt}</CardTitle>
                    <Badge tone={toneOf(report.state)}>{report.state}</Badge>
                  </div>
                  <CardDescription>
                    A {report.subject_type} was reported {formatMoment(report.created_at)}.
                  </CardDescription>
                </CardHeader>

                <CardContent>
                  <p className="text-small break-words">{report.reason}</p>

                  <dl className="mt-3 flex flex-col gap-2 text-small">
                    <div className="flex flex-wrap gap-2">
                      <dt className="font-medium">The subject</dt>
                      <dd className="break-all">{report.subject_id ?? "no identifier"}</dd>
                    </div>
                    {report.reviewed_at ? (
                      <div className="flex flex-wrap gap-2">
                        <dt className="font-medium">The review</dt>
                        <dd>{formatMoment(report.reviewed_at)}</dd>
                      </div>
                    ) : null}
                  </dl>

                  {report.resolution ? (
                    <p className="mt-3 text-small text-muted-foreground">
                      The resolution: {report.resolution}
                    </p>
                  ) : null}
                </CardContent>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function toneOf(state: string): "neutral" | "success" | "warning" | "danger" {
  switch (state) {
    case "resolved":
      return "success";
    case "dismissed":
      return "danger";
    case "under_review":
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
