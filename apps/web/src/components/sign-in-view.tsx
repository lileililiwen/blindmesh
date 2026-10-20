"use client";

import { useState } from "react";

import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { startSignIn, type SignInState } from "@/lib/identity";

/** The paths the sign in may return to. */
const allowedReturnPaths = ["/", "/onboarding", "/settings/profile", "/projects", "/projects/new"];

function resolveReturnPath(value: string | undefined): string | undefined {
  return value && allowedReturnPaths.includes(value) ? value : undefined;
}

/**
 * The sign in of the product. The page explains the next action, keeps the
 * state of the session visible and never announces a sign in before the
 * server accepted it.
 */
export function SignInView({ returnPath, error }: { returnPath?: string; error?: string }) {
  const { state, refresh } = useSession();
  const [status, setStatus] = useState<SignInState>({ kind: "idle" });
  const [announcement, setAnnouncement] = useState("");

  const explanations: Record<string, string> = {
    expired_state: "The sign in link was too old. Start the sign in again.",
    replayed_state: "The sign in link was already used. Start the sign in again.",
    mismatched_state: "The sign in did not match this browser. Start the sign in again.",
    invalid_state: "The sign in could not be verified. Start the sign in again.",
    missing_code: "The sign in provider did not return a code. Start the sign in again.",
    sign_in_failed: "The sign in could not be completed. Start the sign in again.",
  };

  const explanation = error ? explanations[error] : undefined;

  async function signIn() {
    setStatus({ kind: "starting" });
    setAnnouncement("");

    const result = await startSignIn(
      state.kind === "authenticated" || state.kind === "anonymous" ? state.csrfToken : "",
      resolveReturnPath(returnPath),
      readActor(),
    );

    if (result.kind === "ready") {
      window.location.assign(result.authorizeUrl);
      return;
    }

    setStatus(result);
    setAnnouncement(
      result.kind === "unavailable"
        ? "The sign in provider is not available right now."
        : result.kind === "forbidden"
          ? "This sign in is not allowed."
          : "The sign in could not be started.",
    );
  }

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="sign-in-heading" aria-busy="true" className="reading-width">
        <h1 id="sign-in-heading" className="text-title font-semibold">
          Sign in
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Checking your session.</p>
      </section>
    );
  }

  if (state.kind === "authenticated") {
    return (
      <section aria-labelledby="sign-in-heading" className="reading-width">
        <h1 id="sign-in-heading" className="text-title font-semibold">
          Sign in
        </h1>
        <p className="mt-3 text-muted-foreground">
          You are signed in as {state.user.display_name}. Your profile is ready.
        </p>
        <p className="mt-4 flex flex-wrap gap-3">
          <Button asChild variant="primary">
            <a href="/settings/profile">Open the profile settings</a>
          </Button>
          <Button asChild variant="secondary">
            <a href={`/people/${state.user.username}`}>Open the public profile</a>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "unavailable" || state.kind === "error") {
    return (
      <section aria-labelledby="sign-in-heading" className="reading-width">
        <h1 id="sign-in-heading" className="text-title font-semibold">
          Sign in
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          {state.message}
        </p>
        <p className="mt-4">
          <Button type="button" variant="secondary" onClick={() => void refresh()}>
            Try again
          </Button>
        </p>
      </section>
    );
  }

  const blocked = state.kind === "forbidden";

  return (
    <section aria-labelledby="sign-in-heading" className="reading-width">
      <h1 id="sign-in-heading" className="text-title font-semibold">
        Sign in
      </h1>
      <p className="mt-3 text-muted-foreground">
        Sign in with your GitHub account to publish a contributor profile and to join the
        peer testing marketplace.
      </p>

      {explanation ? (
        <p role="alert" className="mt-4 rounded-md border border-warning bg-surface p-3 text-small text-warning">
          {explanation}
        </p>
      ) : null}

      {blocked ? (
        <p role="status" className="mt-4 rounded-md border border-danger bg-surface p-3 text-small text-danger">
          {state.message}
        </p>
      ) : null}

      {status.kind === "unavailable" ? (
        <p role="status" className="mt-4 rounded-md border border-warning bg-surface p-3 text-small text-warning">
          {status.message}
        </p>
      ) : null}

      {status.kind === "error" ? (
        <p role="status" className="mt-4 rounded-md border border-danger bg-surface p-3 text-small text-danger">
          {status.message}
        </p>
      ) : null}

      <p className="mt-6">
        <Button
          type="button"
          variant="primary"
          disabled={status.kind === "starting" || blocked}
          aria-busy={status.kind === "starting"}
          onClick={() => void signIn()}
        >
          {status.kind === "starting" ? "Opening GitHub" : "Sign in with GitHub"}
        </Button>
      </p>

      <p className="mt-3 text-small text-muted-foreground">
        Blindmesh only asks GitHub for your name and your address. It never asks for a password
        of a resource account and it never stores the access token of the provider.
      </p>

      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>
    </section>
  );
}

/**
 * The seeded actor of the local OAuth fixture. The query is only used when
 * the fixture is enabled; a real provider ignores it.
 */
function readActor(): string | undefined {
  if (typeof window === "undefined") {
    return undefined;
  }

  const actor = new URLSearchParams(window.location.search).get("actor");

  return actor && /^[a-z]{1,20}$/.test(actor) ? actor : undefined;
}
