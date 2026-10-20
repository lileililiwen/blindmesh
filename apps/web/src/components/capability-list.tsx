"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

import {
  CapabilityStatusBadge,
  CapabilityVisibilityBadge,
  KindBadge,
} from "@/components/capability-badges";
import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { loadCapabilities, type CapabilityListState } from "@/lib/capabilities";
import type { CapabilityViewResponse } from "@blindmesh/api-client";

/**
 * The directory of the capabilities the visitor may see. The list holds the
 * public capabilities and the own capabilities of a signed in contributor; a
 * capability that may not be seen is never part of the answer.
 */
export function CapabilityList() {
  const { state: session } = useSession();
  const [state, setState] = useState<CapabilityListState>({ kind: "loading" });
  const [capabilities, setCapabilities] = useState<CapabilityViewResponse[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadCapabilities();

    if (next.kind === "ready") {
      setCapabilities(next.capabilities);
      setCursor(next.nextCursor);
    }

    setState(next);
  }, []);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      if (session.kind === "loading") {
        return;
      }

      const next = await loadCapabilities();

      if (cancelled) {
        return;
      }

      if (next.kind === "ready") {
        setCapabilities(next.capabilities);
        setCursor(next.nextCursor);
      }

      setState(next);
    })();

    return () => {
      cancelled = true;
    };
  }, [session.kind]);

  async function loadMore() {
    if (!cursor || loadingMore) {
      return;
    }

    setLoadingMore(true);

    const next = await loadCapabilities(20, cursor);

    if (next.kind === "ready") {
      setCapabilities((current) => [...current, ...next.capabilities]);
      setCursor(next.nextCursor);
    } else {
      setState(next);
    }

    setLoadingMore(false);
  }

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="capabilities-heading" aria-busy="true" className="reading-width">
        <h1 id="capabilities-heading" className="text-title font-semibold">
          Capabilities
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading the capabilities.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-20 w-full rounded bg-muted" />
          <div className="h-20 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (state.kind === "error" || state.kind === "unavailable") {
    return (
      <section aria-labelledby="capabilities-heading" className="reading-width">
        <h1 id="capabilities-heading" className="text-title font-semibold">
          Capabilities
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

  return (
    <section aria-labelledby="capabilities-heading" className="reading-width">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 id="capabilities-heading" className="text-title font-semibold">
            Capabilities
          </h1>
          <p className="mt-2 text-muted-foreground">
            {signedIn
              ? "The public capabilities of the marketplace and the capabilities you own."
              : "The public capabilities of the marketplace. Sign in to declare your own."}
          </p>
        </div>

        {signedIn ? (
          <Button asChild variant="primary">
            <Link href="/settings/capabilities">Declare a capability</Link>
          </Button>
        ) : null}
      </div>

      {capabilities.length === 0 ? (
        <div className="mt-6 rounded-lg border border-border bg-surface p-6">
          <h2 className="text-large font-semibold">No capability is visible yet</h2>
          <p className="mt-2 text-muted-foreground">
            {signedIn
              ? "Declare the first capability, so a task can be matched against your device, your region or your role."
              : "No capability is public right now. Sign in to declare the first one."}
          </p>
          <p className="mt-4 flex flex-wrap gap-3">
            {signedIn ? (
              <Button asChild variant="primary">
                <Link href="/settings/capabilities">Declare a capability</Link>
              </Button>
            ) : (
              <Button asChild variant="primary">
                <Link href="/sign-in?return=%2Fcapabilities">Sign in</Link>
              </Button>
            )}
          </p>
        </div>
      ) : (
        <>
          <ul className="mt-6 flex flex-col gap-4">
            {capabilities.map((capability) => (
              <li key={capability.id}>
                <article className="rounded-lg border border-border bg-surface p-4 sm:p-5">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="text-large font-semibold">{capability.label}</h2>
                    <KindBadge kind={capability.kind} />
                    <CapabilityVisibilityBadge visibility={capability.visibility} />
                    <CapabilityStatusBadge status={capability.status} />
                    {capability.available ? null : <span className="text-small text-muted-foreground">not available</span>}
                  </div>

                  {capability.tags.length > 0 ? (
                    <p className="mt-2 text-small text-muted-foreground">
                      Tags: {capability.tags.join(", ")}
                    </p>
                  ) : null}

                  {Object.keys(capability.metadata).length > 0 ? (
                    <p className="mt-1 text-small text-muted-foreground">
                      {Object.entries(capability.metadata)
                        .map(([key, value]) => `${key}: ${value}`)
                        .join(", ")}
                    </p>
                  ) : null}

                  <p className="mt-2 text-small text-muted-foreground">
                    Declared by {capability.owner_username || "an unknown contributor"}.
                    {capability.usable_for_matching
                      ? " It can be matched against a task."
                      : " It is not used for a match, because a match only uses public and available declarations."}
                  </p>
                </article>
              </li>
            ))}
          </ul>

          {cursor ? (
            <p className="mt-5">
              <Button type="button" variant="secondary" disabled={loadingMore} onClick={() => void loadMore()}>
                {loadingMore ? "Loading more capabilities" : "Load more capabilities"}
              </Button>
            </p>
          ) : null}
        </>
      )}
    </section>
  );
}
