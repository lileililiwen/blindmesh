"use client";

import { useCallback, useEffect, useRef, useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  announcementFor,
  loadAvailabilityBrowser,
  type AvailabilityState,
} from "@/lib/availability";

const dependencyLabels: Record<string, string> = {
  postgres: "Database",
  "object-storage": "Object storage",
  mail: "Mail sink",
};

function dependencyLabel(name: string): string {
  return dependencyLabels[name] ?? name;
}

/**
 * The availability of the runtime. The state is read through the same origin
 * path, so the browser and the API always use the published contract.
 */
export function AvailabilityPanel() {
  const [state, setState] = useState<AvailabilityState>({ kind: "loading" });
  const [pending, setPending] = useState(false);
  const [announcement, setAnnouncement] = useState("");
  const triggerRef = useRef<HTMLButtonElement>(null);
  const shouldFocus = useRef(false);

  const load = useCallback(async (announce: boolean) => {
    const next = await loadAvailabilityBrowser();

    setState(next);

    if (announce) {
      setAnnouncement(announcementFor(next));
      shouldFocus.current = true;
    }
  }, []);

  // The focus returns to the button that was pressed. The button is disabled
  // while the check runs, so the focus is restored after it is enabled again.
  useEffect(() => {
    if (!pending && shouldFocus.current) {
      shouldFocus.current = false;
      triggerRef.current?.focus();
    }
  }, [pending]);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadAvailabilityBrowser();

      if (!cancelled) {
        setState(next);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  if (state.kind === "loading") {
    return <AvailabilitySkeleton pending={pending} />;
  }

  const tone =
    state.kind === "ready"
      ? "success"
      : state.kind === "unavailable"
        ? "danger"
        : state.kind === "forbidden"
          ? "warning"
          : "neutral";

  const label =
    state.kind === "ready"
      ? "Available"
      : state.kind === "unavailable"
        ? "Unavailable"
        : state.kind === "forbidden"
          ? "Not permitted"
          : state.kind === "empty"
            ? "No data"
            : "Unreachable";

  const description =
    state.kind === "ready"
      ? "Every required dependency answered the last readiness check."
      : state.kind === "unavailable"
        ? state.message
        : state.kind === "error"
          ? state.message
          : state.kind === "forbidden"
            ? state.message
            : state.kind === "empty"
              ? state.message
              : "Checking the runtime.";

  const dependencies =
    state.kind === "ready" || state.kind === "unavailable"
      ? (state.availability?.dependencies ?? [])
      : [];

  const canRetry = state.kind !== "ready";

  return (
    <section
      aria-labelledby="availability-heading"
      className="reading-width rounded-lg border border-border bg-surface p-4 sm:p-6"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id="availability-heading" className="text-title font-semibold">
          Runtime availability
        </h2>
        <Badge tone={tone}>{label}</Badge>
      </div>

      <p className="mt-2 text-small text-muted-foreground">{description}</p>

      {dependencies.length > 0 ? (
        <ul className="mt-4 flex flex-col gap-2">
          {dependencies.map((dependency) => (
            <li
              key={dependency.name}
              className="flex flex-wrap items-center gap-x-3 gap-y-1"
            >
              <span className="font-medium">{dependencyLabel(dependency.name)}</span>
              <Badge tone={dependency.status === "up" ? "success" : "danger"}>
                {dependency.status === "up" ? "Up" : "Down"}
              </Badge>
              {dependency.detail ? (
                <span className="text-small text-muted-foreground">
                  {dependency.detail}
                </span>
              ) : null}
            </li>
          ))}
        </ul>
      ) : null}

      {canRetry ? (
        <div className="mt-4">
          <Button
            ref={triggerRef}
            onClick={() => {
              setPending(true);
              void load(true).finally(() => setPending(false));
            }}
            disabled={pending}
            aria-busy={pending}
            variant="secondary"
          >
            {pending ? "Checking the runtime" : "Retry the readiness check"}
          </Button>
        </div>
      ) : null}

      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>
    </section>
  );
}

export function AvailabilitySkeleton({ pending = false }: { pending?: boolean }) {
  return (
    <section
      aria-labelledby="availability-heading"
      aria-busy={pending}
      className="reading-width rounded-lg border border-border bg-surface p-4 sm:p-6"
    >
      <h2 id="availability-heading" className="text-title font-semibold">
        Runtime availability
      </h2>
      <p className="mt-2 text-small text-muted-foreground">Checking the runtime.</p>
      <div className="mt-4 flex flex-col gap-2" aria-hidden="true">
        <div className="h-5 w-48 rounded bg-muted" />
        <div className="h-5 w-40 rounded bg-muted" />
        <div className="h-5 w-56 rounded bg-muted" />
      </div>
    </section>
  );
}
