"use client";

import { useCallback, useEffect, useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { loadPublicStatus, type PublicStatusState } from "@/lib/operations";

/**
 * The public status of the product. The page shows the real state of the
 * runtime and of its dependencies, the measured counts of the marketplace and
 * the honest statement of what is not implemented yet.
 *
 * The page holds no private field, no identity of a person and no amount of the
 * ledger of a person.
 */
export function StatusPanel() {
  const [state, setState] = useState<PublicStatusState>({ kind: "loading" });

  const load = useCallback(async () => {
    setState({ kind: "loading" });
    setState(await loadPublicStatus());
  }, []);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadPublicStatus();

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
      <section aria-labelledby="status-heading" aria-busy="true" className="reading-width">
        <h1 id="status-heading" className="text-title font-semibold">
          The status of Blindmesh
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the state of the product.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-24 w-full rounded bg-muted" />
          <div className="h-32 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (state.kind === "error") {
    return (
      <section aria-labelledby="status-heading" className="reading-width">
        <h1 id="status-heading" className="text-title font-semibold">
          The status of Blindmesh
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

  const status = state.status;
  const dependencies = status.dependencies ?? [];
  const marketplace = status.marketplace;

  return (
    <section aria-labelledby="status-heading" className="reading-width">
      <h1 id="status-heading" className="text-title font-semibold">
        The status of Blindmesh
      </h1>
      <p className="mt-2 text-muted-foreground">
        The state of the product right now. Every number was measured by the product itself.
      </p>

      <p role="status" className="sr-only">
        The status is loaded. The product is {status.status}.
      </p>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The runtime</CardTitle>
          <CardDescription>
            The product is {status.status}. It was checked at {formatDate(status.checked_at)}.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ul className="flex flex-col gap-3">
            {dependencies.map((dependency) => (
              <li key={dependency.name} className="flex flex-wrap items-center gap-2">
                <span className="text-body font-medium">{dependency.name}</span>
                <Badge tone={dependency.status === "up" ? "success" : "warning"}>
                  <span aria-hidden="true">{dependency.status}</span>
                  <span className="sr-only">
                    {dependency.status === "up" ? "The dependency is up." : "The dependency is down."}
                  </span>
                </Badge>
                {dependency.detail ? (
                  <span className="text-small text-muted-foreground">{dependency.detail}</span>
                ) : null}
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>The marketplace</CardTitle>
          <CardDescription>The measured counts of the product.</CardDescription>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-3 sm:grid-cols-2">
            <Count label="Published tasks" value={marketplace.published_tasks} />
            <Count label="Accepted contributions" value={marketplace.accepted_contributions} />
            <Count label="Open cases" value={marketplace.open_cases} />
            <Count label="Contributors" value={marketplace.contributors} />
          </dl>
        </CardContent>
      </Card>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>What is not implemented yet</CardTitle>
          <CardDescription>
            The honest list of the parts of the roadmap that the product does not have today.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ul className="flex flex-col gap-2">
            {(status.not_implemented ?? []).map((statement) => (
              <li key={statement} className="text-small">
                {statement}
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>
    </section>
  );
}

/** One measured count of the marketplace. */
function Count({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="rounded-lg border border-border bg-surface p-4">
      <dt className="text-small font-medium text-muted-foreground">{label}</dt>
      <dd className="text-title font-semibold">{value}</dd>
    </div>
  );
}

/** The date of a moment, written as a plain calendar day of UTC. */
function formatDate(value: string): string {
  return new Date(value).toISOString().slice(0, 16).replace("T", " ");
}
