"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

import { useSession } from "@/components/session-provider";
import { Button } from "@/components/ui/button";
import { CheckboxField } from "@/components/ui/field";
import {
  loadNotificationPreferences,
  saveNotificationPreferences,
  type NotificationPreferencesState,
} from "@/lib/notifications";

/**
 * The notification preferences of the signed in contributor. Every control is
 * a real checkbox with a visible label, so the keyboard and a screen reader
 * reach it. The saved state is announced only after the server confirmed it.
 */
export function NotificationPreferences() {
  const { csrfToken } = useSession();
  const [state, setState] = useState<NotificationPreferencesState>({ kind: "loading" });
  const [inApp, setInApp] = useState(false);
  const [email, setEmail] = useState(false);
  const [saved, setSaved] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadNotificationPreferences();

    if (next.kind === "ready") {
      setInApp(next.preferences.in_app_enabled);
      setEmail(next.preferences.email_enabled);
    }

    setState(next);
  }, []);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadNotificationPreferences();

      if (cancelled) {
        return;
      }

      if (next.kind === "ready") {
        setInApp(next.preferences.in_app_enabled);
        setEmail(next.preferences.email_enabled);
      }

      setState(next);
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const save = useCallback(
    async (change: { in_app_enabled?: boolean; email_enabled?: boolean }) => {
      setSaving(true);
      setSaved(null);

      const result = await saveNotificationPreferences(change, csrfToken);

      if (result.kind === "saved") {
        setInApp(result.preferences.in_app_enabled);
        setEmail(result.preferences.email_enabled);

        // The saved state is announced only after the server confirmed it.
        setSaved("The notification preferences are saved.");
      } else {
        setState({ kind: "error", message: result.message });
        setSaved(null);
      }

      setSaving(false);
    },
    [csrfToken],
  );

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="notification-preferences-heading" aria-busy="true" className="reading-width">
        <h1 id="notification-preferences-heading" className="text-title font-semibold">
          Notification preferences
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading your preferences.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-10 w-full rounded bg-muted" />
          <div className="h-10 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="notification-preferences-heading" className="reading-width">
        <h1 id="notification-preferences-heading" className="text-title font-semibold">
          Notification preferences
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          The preferences are private. Sign in to change them.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <Link href="/sign-in?return=%2Fsettings%2Fnotifications">Sign in</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "error") {
    return (
      <section aria-labelledby="notification-preferences-heading" className="reading-width">
        <h1 id="notification-preferences-heading" className="text-title font-semibold">
          Notification preferences
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
    <section aria-labelledby="notification-preferences-heading" className="reading-width">
      <h1 id="notification-preferences-heading" className="text-title font-semibold">
        Notification preferences
      </h1>
      <p className="mt-2 text-muted-foreground">
        Choose how the product tells you about the work. A change is saved on the server and is announced
        after the server confirmed it.
      </p>

      <div className="mt-6 flex flex-col gap-4">
        <CheckboxField
          id="notification-in-app"
          label="Show the notifications in the app"
          className="min-h-[var(--component-control-min-height)] items-center"
          checked={inApp}
          disabled={saving}
          onChange={(event) => {
            const next = event.target.checked;
            setInApp(next);
            void save({ in_app_enabled: next });
          }}
        />

        <CheckboxField
          id="notification-email"
          label="Send the notifications by email"
          className="min-h-[var(--component-control-min-height)] items-center"
          checked={email}
          disabled={saving}
          onChange={(event) => {
            const next = event.target.checked;
            setEmail(next);
            void save({ email_enabled: next });
          }}
        />
      </div>

      <p role="status" aria-live="polite" className="mt-4 text-small text-muted-foreground">
        {saved ?? ""}
      </p>

      <p className="mt-6 text-small text-muted-foreground">
        The email never holds the title of a project, the text of a finding or a link to a file. It names the
        kind of the event and links to the place of the work.
      </p>
    </section>
  );
}
