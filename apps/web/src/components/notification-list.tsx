"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

import { useSession } from "@/components/session-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  loadNotifications,
  markNotificationRead,
  type NotificationListState,
} from "@/lib/notifications";

type NotificationItem = {
  id: string;
  category: string;
  label: string;
  summary: string;
  deep_link: string;
  is_read: boolean;
  created_at: string;
  read_at: string | null;
};

/**
 * The notifications of the signed in contributor. The list shows the safe
 * summary of every notification and a link to its source. A notification of
 * another account is never part of the list.
 */
export function NotificationList() {
  const { csrfToken } = useSession();
  const [state, setState] = useState<NotificationListState>({ kind: "loading" });
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [pending, setPending] = useState<string | null>(null);

  const load = useCallback(async () => {
    setState({ kind: "loading" });

    const next = await loadNotifications();

    if (next.kind === "ready") {
      setItems(next.items as NotificationItem[]);
      setNextCursor(next.nextCursor);
    }

    setState(next);
  }, []);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const next = await loadNotifications();

      if (cancelled) {
        return;
      }

      if (next.kind === "ready") {
        setItems(next.items as NotificationItem[]);
        setNextCursor(next.nextCursor);
      }

      setState(next);
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const loadMore = useCallback(async () => {
    if (!nextCursor) {
      return;
    }

    const next = await loadNotifications(nextCursor);

    if (next.kind === "ready") {
      setItems((current) => [...current, ...(next.items as NotificationItem[])]);
      setNextCursor(next.nextCursor);
    } else {
      setState(next);
    }
  }, [nextCursor]);

  const markRead = useCallback(
    async (id: string) => {
      setPending(id);

      const result = await markNotificationRead(id, csrfToken);

      if (result.kind === "read") {
        setItems((current) =>
          current.map((item) =>
            item.id === id
              ? {
                  ...item,
                  is_read: true,
                  read_at: result.notification.read_at ?? new Date().toISOString(),
                }
              : item,
          ),
        );
      } else if (result.kind === "not-found") {
        setItems((current) => current.filter((item) => item.id !== id));
      } else {
        setState({ kind: "error", message: result.message });
      }

      setPending(null);
    },
    [csrfToken],
  );

  if (state.kind === "loading") {
    return (
      <section aria-labelledby="notifications-heading" aria-busy="true" className="reading-width">
        <h1 id="notifications-heading" className="text-title font-semibold">
          Notifications
        </h1>
        <p className="mt-2 text-small text-muted-foreground">Loading your notifications.</p>
        <div className="mt-4 flex flex-col gap-3" aria-hidden="true">
          <div className="h-20 w-full rounded bg-muted" />
          <div className="h-20 w-full rounded bg-muted" />
          <div className="h-20 w-full rounded bg-muted" />
        </div>
      </section>
    );
  }

  if (state.kind === "unauthorized") {
    return (
      <section aria-labelledby="notifications-heading" className="reading-width">
        <h1 id="notifications-heading" className="text-title font-semibold">
          Notifications
        </h1>
        <p role="status" className="mt-3 text-muted-foreground">
          The notifications are private. Sign in to read them.
        </p>
        <p className="mt-4">
          <Button asChild variant="primary">
            <Link href="/sign-in?return=%2Fnotifications">Sign in</Link>
          </Button>
        </p>
      </section>
    );
  }

  if (state.kind === "error") {
    return (
      <section aria-labelledby="notifications-heading" className="reading-width">
        <h1 id="notifications-heading" className="text-title font-semibold">
          Notifications
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

  const unread = items.filter((item) => !item.is_read).length;

  return (
    <section aria-labelledby="notifications-heading" className="reading-width">
      <h1 id="notifications-heading" className="text-title font-semibold">
        Notifications
      </h1>
      <p className="mt-2 text-muted-foreground">
        Every notification names the kind of event and links to the place of the work. All dates are UTC.
      </p>

      <p role="status" className="sr-only">
        The notifications are loaded. The list holds {items.length}{" "}
        {items.length === 1 ? "notification" : "notifications"}, {unread}{" "}
        {unread === 1 ? "is" : "are"} not read yet.
      </p>

      {items.length === 0 ? (
        <div className="mt-6 rounded-lg border border-border bg-surface p-6">
          <h2 className="text-large font-semibold">No notification yet</h2>
          <p className="mt-2 text-muted-foreground">
            You are told here when a task receives your application, when the owner asks for a correction and
            when a case of arbitration changes.
          </p>
          <p className="mt-4">
            <Button asChild variant="primary">
              <Link href="/work">Open your work</Link>
            </Button>
          </p>
        </div>
      ) : (
        <>
          <ul className="mt-6 flex flex-col gap-3">
            {items.map((item) => (
              <li key={item.id}>
                <NotificationCard
                  item={item}
                  busy={pending === item.id}
                  onMarkRead={() => void markRead(item.id)}
                />
              </li>
            ))}
          </ul>

          {nextCursor ? (
            <p className="mt-4">
              <Button type="button" variant="secondary" onClick={() => void loadMore()}>
                Load more
              </Button>
            </p>
          ) : null}
        </>
      )}
    </section>
  );
}

/**
 * One notification. It shows the safe summary, the kind of the event and the
 * date. A notification that is not read yet carries a marker with a label, so
 * the state is never carried by the colour alone.
 */
function NotificationCard({
  item,
  busy,
  onMarkRead,
}: {
  item: NotificationItem;
  busy: boolean;
  onMarkRead: () => void;
}) {
  const href = safeHref(item.deep_link);

  return (
    <article
      className="rounded-lg border border-border bg-surface p-4"
      aria-labelledby={`notification-${item.id}-label`}
    >
      <div className="flex flex-wrap items-center gap-2">
        <h2 id={`notification-${item.id}-label`} className="text-body font-semibold">
          {item.label}
        </h2>

        {item.is_read ? null : (
          <Badge tone="success">
            <span aria-hidden="true">New</span>
            <span className="sr-only">Not read yet</span>
          </Badge>
        )}

        <span className="text-small text-muted-foreground">{formatDate(item.created_at)}</span>
      </div>

      <p className="mt-2 text-small">{item.summary}</p>

      <div className="mt-3 flex flex-wrap items-center gap-3">
        {href ? (
          <Button asChild variant="secondary" size="small">
            <Link href={href}>Open the source</Link>
          </Button>
        ) : null}

        {item.is_read ? null : (
          <Button type="button" variant="ghost" size="small" disabled={busy} onClick={onMarkRead}>
            {busy ? "Marking as read" : "Mark as read"}
          </Button>
        )}
      </div>
    </article>
  );
}

/**
 * The link of a notification. Only a path inside the product is kept, so a
 * value that points at another site never becomes a link of the interface.
 */
function safeHref(deepLink: string): string | null {
  if (!deepLink.startsWith("/") || deepLink.startsWith("//") || deepLink.length > 200) {
    return null;
  }

  return deepLink;
}

/** The date of a moment, written as a plain calendar day of UTC. */
function formatDate(value: string): string {
  return new Date(value).toISOString().slice(0, 16).replace("T", " ");
}
